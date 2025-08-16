using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using StackExchange.Redis;
using Wind.Server.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Wind.Server.Services;

/// <summary>
/// 分布式事务服务
/// 提供跨Redis和MongoDB的分布式事务支持，确保数据一致性
/// </summary>
public class DistributedTransactionService : IDisposable
{
    private readonly RedisConnectionManager _redisManager;
    private readonly MongoDbConnectionManager _mongoManager;
    private readonly RedisDistributedLockService _lockService;
    private readonly ILogger<DistributedTransactionService> _logger;
    private readonly ConcurrentDictionary<string, DistributedTransaction> _activeTransactions;
    private readonly Timer _cleanupTimer;
    private volatile bool _disposed = false;

    // 统计信息
    private long _transactionStartedCount = 0;
    private long _transactionCommittedCount = 0;
    private long _transactionRolledBackCount = 0;
    private long _transactionTimeoutCount = 0;

    public DistributedTransactionService(
        RedisConnectionManager redisManager,
        MongoDbConnectionManager mongoManager,
        RedisDistributedLockService lockService,
        ILogger<DistributedTransactionService> logger)
    {
        _redisManager = redisManager;
        _mongoManager = mongoManager;
        _lockService = lockService;
        _logger = logger;
        _activeTransactions = new ConcurrentDictionary<string, DistributedTransaction>();

        // 启动清理超时事务的定时器
        _cleanupTimer = new Timer(CleanupTimeoutTransactions, null, 
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger.LogInformation("分布式事务服务已初始化");
    }

    /// <summary>
    /// 开始分布式事务
    /// </summary>
    public async Task<IDistributedTransactionHandle> BeginTransactionAsync(
        IEnumerable<string> lockKeys,
        TimeSpan? timeout = null)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DistributedTransactionService));
        }

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionTimeout = timeout ?? TimeSpan.FromMinutes(5);
        var lockKeysList = lockKeys.ToList();

        try
        {
            _logger.LogDebug("开始分布式事务: {TransactionId}, 锁键数量: {LockCount}", 
                transactionId, lockKeysList.Count);

            // 获取所有需要的分布式锁
            var lockHandles = new List<IDistributedLockHandle>();
            foreach (var lockKey in lockKeysList.OrderBy(k => k)) // 按字典序排序避免死锁
            {
                var lockHandle = await _lockService.TryAcquireLockAsync(
                    lockKey, transactionTimeout, TimeSpan.FromSeconds(30));
                
                if (lockHandle == null)
                {
                    // 如果获取锁失败，释放已获取的锁
                    foreach (var existingHandle in lockHandles)
                    {
                        existingHandle.Dispose();
                    }
                    throw new InvalidOperationException($"无法获取分布式锁: {lockKey}");
                }
                
                lockHandles.Add(lockHandle);
            }

            // 开始MongoDB事务
            var mongoSession = await _mongoManager.GetClient().StartSessionAsync();
            mongoSession.StartTransaction();

            // 创建分布式事务对象
            var transaction = new DistributedTransaction
            {
                TransactionId = transactionId,
                LockHandles = lockHandles,
                MongoSession = mongoSession,
                RedisOperations = new List<RedisOperation>(),
                StartTime = DateTime.UtcNow,
                Timeout = transactionTimeout,
                Status = TransactionStatus.Active
            };

            _activeTransactions.TryAdd(transactionId, transaction);
            Interlocked.Increment(ref _transactionStartedCount);

            _logger.LogInformation("分布式事务已开始: {TransactionId}", transactionId);
            return new DistributedTransactionHandle(this, transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始分布式事务失败: {TransactionId}", transactionId);
            throw;
        }
    }

    /// <summary>
    /// 在事务中执行Redis操作
    /// </summary>
    internal void AddRedisOperation(DistributedTransaction transaction, RedisOperation operation)
    {
        if (transaction.Status != TransactionStatus.Active)
        {
            throw new InvalidOperationException($"事务状态无效: {transaction.Status}");
        }

        transaction.RedisOperations.Add(operation);
        _logger.LogDebug("添加Redis操作到事务: {TransactionId}, 操作类型: {OperationType}", 
            transaction.TransactionId, operation.Type);
    }

    /// <summary>
    /// 提交分布式事务
    /// </summary>
    internal async Task<bool> CommitTransactionAsync(DistributedTransaction transaction)
    {
        if (transaction.Status != TransactionStatus.Active)
        {
            throw new InvalidOperationException($"事务状态无效: {transaction.Status}");
        }

        try
        {
            transaction.Status = TransactionStatus.Committing;
            _logger.LogDebug("开始提交分布式事务: {TransactionId}", transaction.TransactionId);

            // 1. 先提交MongoDB事务
            await transaction.MongoSession.CommitTransactionAsync();
            _logger.LogDebug("MongoDB事务已提交: {TransactionId}", transaction.TransactionId);

            // 2. 执行所有Redis操作
            var database = _redisManager.GetDatabase();
            var batch = database.CreateBatch();
            var redisTasks = new List<Task<bool>>();

            foreach (var operation in transaction.RedisOperations)
            {
                switch (operation.Type)
                {
                    case RedisOperationType.Set:
                        redisTasks.Add(batch.StringSetAsync(operation.Key, operation.Value, operation.Expiry));
                        break;
                    case RedisOperationType.Delete:
                        redisTasks.Add(batch.KeyDeleteAsync(operation.Key).ContinueWith(t => t.Result));
                        break;
                    case RedisOperationType.HashSet:
                        redisTasks.Add(batch.HashSetAsync(operation.Key, operation.HashField!, operation.Value).ContinueWith(t => true));
                        break;
                    case RedisOperationType.HashDelete:
                        redisTasks.Add(batch.HashDeleteAsync(operation.Key, operation.HashField!).ContinueWith(t => t.Result));
                        break;
                }
            }

            batch.Execute();
            await Task.WhenAll(redisTasks);

            _logger.LogDebug("Redis操作已执行: {TransactionId}, 操作数量: {OperationCount}", 
                transaction.TransactionId, transaction.RedisOperations.Count);

            transaction.Status = TransactionStatus.Committed;
            _activeTransactions.TryRemove(transaction.TransactionId, out _);
            Interlocked.Increment(ref _transactionCommittedCount);

            _logger.LogInformation("分布式事务提交成功: {TransactionId}", transaction.TransactionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提交分布式事务失败: {TransactionId}", transaction.TransactionId);
            
            // 尝试回滚
            await RollbackTransactionAsync(transaction);
            return false;
        }
    }

    /// <summary>
    /// 回滚分布式事务
    /// </summary>
    internal async Task RollbackTransactionAsync(DistributedTransaction transaction)
    {
        try
        {
            transaction.Status = TransactionStatus.RollingBack;
            _logger.LogDebug("开始回滚分布式事务: {TransactionId}", transaction.TransactionId);

            // 回滚MongoDB事务
            if (transaction.MongoSession.IsInTransaction)
            {
                await transaction.MongoSession.AbortTransactionAsync();
                _logger.LogDebug("MongoDB事务已回滚: {TransactionId}", transaction.TransactionId);
            }

            // Redis操作回滚（执行反向操作）
            if (transaction.RedisOperations.Any())
            {
                await RollbackRedisOperations(transaction.RedisOperations);
                _logger.LogDebug("Redis操作已回滚: {TransactionId}", transaction.TransactionId);
            }

            transaction.Status = TransactionStatus.RolledBack;
            _activeTransactions.TryRemove(transaction.TransactionId, out _);
            Interlocked.Increment(ref _transactionRolledBackCount);

            _logger.LogInformation("分布式事务回滚成功: {TransactionId}", transaction.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "回滚分布式事务失败: {TransactionId}", transaction.TransactionId);
            transaction.Status = TransactionStatus.Failed;
        }
    }

    /// <summary>
    /// 回滚Redis操作
    /// </summary>
    private async Task RollbackRedisOperations(List<RedisOperation> operations)
    {
        var database = _redisManager.GetDatabase();
        var reversedOperations = operations.AsEnumerable().Reverse();

        foreach (var operation in reversedOperations)
        {
            try
            {
                switch (operation.Type)
                {
                    case RedisOperationType.Set:
                        // 如果之前有值则恢复，否则删除
                        if (operation.PreviousValue.HasValue)
                        {
                            await database.StringSetAsync(operation.Key, operation.PreviousValue, operation.PreviousExpiry);
                        }
                        else
                        {
                            await database.KeyDeleteAsync(operation.Key);
                        }
                        break;
                    case RedisOperationType.Delete:
                        // 恢复之前删除的值
                        if (operation.PreviousValue.HasValue)
                        {
                            await database.StringSetAsync(operation.Key, operation.PreviousValue, operation.PreviousExpiry);
                        }
                        break;
                    case RedisOperationType.HashSet:
                        // 恢复或删除Hash字段
                        if (operation.PreviousValue.HasValue)
                        {
                            await database.HashSetAsync(operation.Key, operation.HashField!, operation.PreviousValue);
                        }
                        else
                        {
                            await database.HashDeleteAsync(operation.Key, operation.HashField!);
                        }
                        break;
                    case RedisOperationType.HashDelete:
                        // 恢复之前删除的Hash字段
                        if (operation.PreviousValue.HasValue)
                        {
                            await database.HashSetAsync(operation.Key, operation.HashField!, operation.PreviousValue);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "回滚Redis操作失败: {Key}, 操作类型: {Type}", 
                    operation.Key, operation.Type);
            }
        }
    }

    /// <summary>
    /// 获取MongoDB集合（事务版本）
    /// </summary>
    public IMongoCollection<T> GetMongoCollection<T>(DistributedTransaction transaction, string collectionName)
    {
        return _mongoManager.GetDatabase().GetCollection<T>(collectionName);
    }

    /// <summary>
    /// 在事务中设置Redis字符串值
    /// </summary>
    public async Task SetRedisStringAsync(DistributedTransaction transaction, string key, string value, TimeSpan? expiry = null)
    {
        // 记录当前值用于回滚
        var database = _redisManager.GetDatabase();
        var currentValue = await database.StringGetAsync(key);
        var currentExpiry = currentValue.HasValue ? await database.KeyTimeToLiveAsync(key) : null;

        var operation = new RedisOperation
        {
            Type = RedisOperationType.Set,
            Key = key,
            Value = value,
            Expiry = expiry,
            PreviousValue = currentValue,
            PreviousExpiry = currentExpiry
        };

        AddRedisOperation(transaction, operation);
    }

    /// <summary>
    /// 在事务中删除Redis键
    /// </summary>
    public async Task DeleteRedisKeyAsync(DistributedTransaction transaction, string key)
    {
        // 记录当前值用于回滚
        var database = _redisManager.GetDatabase();
        var currentValue = await database.StringGetAsync(key);
        var currentExpiry = currentValue.HasValue ? await database.KeyTimeToLiveAsync(key) : null;

        var operation = new RedisOperation
        {
            Type = RedisOperationType.Delete,
            Key = key,
            PreviousValue = currentValue,
            PreviousExpiry = currentExpiry
        };

        AddRedisOperation(transaction, operation);
    }

    /// <summary>
    /// 清理超时事务
    /// </summary>
    private void CleanupTimeoutTransactions(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var timeoutTransactions = _activeTransactions.Values
                .Where(t => now - t.StartTime > t.Timeout)
                .ToList();

            foreach (var transaction in timeoutTransactions)
            {
                _logger.LogWarning("检测到超时事务，执行回滚: {TransactionId}", transaction.TransactionId);
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RollbackTransactionAsync(transaction);
                        Interlocked.Increment(ref _transactionTimeoutCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "清理超时事务失败: {TransactionId}", transaction.TransactionId);
                    }
                });
            }

            if (timeoutTransactions.Count > 0)
            {
                _logger.LogInformation("清理了 {Count} 个超时事务", timeoutTransactions.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理超时事务时发生错误");
        }
    }

    /// <summary>
    /// 获取事务统计信息
    /// </summary>
    public TransactionStatistics GetStatistics()
    {
        return new TransactionStatistics
        {
            TransactionStartedCount = _transactionStartedCount,
            TransactionCommittedCount = _transactionCommittedCount,
            TransactionRolledBackCount = _transactionRolledBackCount,
            TransactionTimeoutCount = _transactionTimeoutCount,
            ActiveTransactionCount = _activeTransactions.Count,
            SuccessRate = _transactionStartedCount > 0 
                ? (double)_transactionCommittedCount / _transactionStartedCount * 100 
                : 0
        };
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cleanupTimer?.Dispose();

        // 回滚所有活跃事务
        var rollbackTasks = _activeTransactions.Values.Select(RollbackTransactionAsync);
        try
        {
            Task.WhenAll(rollbackTasks).Wait(TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放资源时回滚活跃事务失败");
        }

        _activeTransactions.Clear();
        _logger.LogInformation("分布式事务服务已释放");
    }
}

/// <summary>
/// 分布式事务
/// </summary>
public class DistributedTransaction
{
    public string TransactionId { get; set; } = string.Empty;
    public List<IDistributedLockHandle> LockHandles { get; set; } = new();
    public IClientSessionHandle MongoSession { get; set; } = null!;
    public List<RedisOperation> RedisOperations { get; set; } = new();
    public DateTime StartTime { get; set; }
    public TimeSpan Timeout { get; set; }
    public TransactionStatus Status { get; set; }
}

/// <summary>
/// Redis操作
/// </summary>
public class RedisOperation
{
    public RedisOperationType Type { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? HashField { get; set; }
    public TimeSpan? Expiry { get; set; }
    public RedisValue PreviousValue { get; set; }
    public TimeSpan? PreviousExpiry { get; set; }
}

/// <summary>
/// Redis操作类型
/// </summary>
public enum RedisOperationType
{
    Set,
    Delete,
    HashSet,
    HashDelete
}

/// <summary>
/// 事务状态
/// </summary>
public enum TransactionStatus
{
    Active,
    Committing,
    Committed,
    RollingBack,
    RolledBack,
    Failed
}

/// <summary>
/// 分布式事务句柄接口
/// </summary>
public interface IDistributedTransactionHandle : IDisposable
{
    string TransactionId { get; }
    Task<bool> CommitAsync();
    Task RollbackAsync();
    Task SetRedisStringAsync(string key, string value, TimeSpan? expiry = null);
    Task DeleteRedisKeyAsync(string key);
    IMongoCollection<T> GetMongoCollection<T>(string collectionName);
    IClientSessionHandle MongoSession { get; }
}

/// <summary>
/// 分布式事务句柄实现
/// </summary>
public class DistributedTransactionHandle : IDistributedTransactionHandle
{
    private readonly DistributedTransactionService _service;
    private readonly DistributedTransaction _transaction;
    private volatile bool _disposed = false;

    public DistributedTransactionHandle(DistributedTransactionService service, DistributedTransaction transaction)
    {
        _service = service;
        _transaction = transaction;
    }

    public string TransactionId => _transaction.TransactionId;
    public IClientSessionHandle MongoSession => _transaction.MongoSession;

    public async Task<bool> CommitAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DistributedTransactionHandle));
        }

        return await _service.CommitTransactionAsync(_transaction);
    }

    public async Task RollbackAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DistributedTransactionHandle));
        }

        await _service.RollbackTransactionAsync(_transaction);
    }

    public async Task SetRedisStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DistributedTransactionHandle));
        }

        await _service.SetRedisStringAsync(_transaction, key, value, expiry);
    }

    public async Task DeleteRedisKeyAsync(string key)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DistributedTransactionHandle));
        }

        await _service.DeleteRedisKeyAsync(_transaction, key);
    }

    public IMongoCollection<T> GetMongoCollection<T>(string collectionName)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DistributedTransactionHandle));
        }

        return _service.GetMongoCollection<T>(_transaction, collectionName);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // 释放所有锁
        foreach (var lockHandle in _transaction.LockHandles)
        {
            try
            {
                lockHandle.Dispose();
            }
            catch (Exception)
            {
                // 忽略释放锁时的异常
            }
        }

        // 释放MongoDB会话
        try
        {
            _transaction.MongoSession?.Dispose();
        }
        catch (Exception)
        {
            // 忽略释放会话时的异常
        }
    }
}

/// <summary>
/// 事务统计信息
/// </summary>
public class TransactionStatistics
{
    public long TransactionStartedCount { get; set; }
    public long TransactionCommittedCount { get; set; }
    public long TransactionRolledBackCount { get; set; }
    public long TransactionTimeoutCount { get; set; }
    public int ActiveTransactionCount { get; set; }
    public double SuccessRate { get; set; }
}
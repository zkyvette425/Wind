using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wind.Server.Configuration;
using System.Collections.Concurrent;

namespace Wind.Server.Services;

/// <summary>
/// Redis分布式锁服务
/// 提供安全的分布式锁机制，防止数据竞争
/// </summary>
public class RedisDistributedLockService : IDisposable
{
    private readonly RedisConnectionManager _redisManager;
    private readonly DistributedLockOptions _lockOptions;
    private readonly ILogger<RedisDistributedLockService> _logger;
    private readonly ConcurrentDictionary<string, DistributedLock> _activeLocks;
    private readonly Timer? _renewalTimer;
    private volatile bool _disposed = false;

    // 统计信息
    private long _lockAcquiredCount = 0;
    private long _lockReleasedCount = 0;
    private long _lockTimeoutCount = 0;
    private long _lockRenewalCount = 0;

    public RedisDistributedLockService(
        RedisConnectionManager redisManager,
        IOptions<DistributedLockOptions> lockOptions,
        ILogger<RedisDistributedLockService> logger)
    {
        _redisManager = redisManager;
        _lockOptions = lockOptions.Value;
        _logger = logger;
        _activeLocks = new ConcurrentDictionary<string, DistributedLock>();

        // 验证配置
        _lockOptions.Validate();

        // 启动自动续约定时器
        if (_lockOptions.EnableAutoRenewal)
        {
            var renewalInterval = TimeSpan.FromSeconds(Math.Max(1, _lockOptions.DefaultExpiryMinutes * 60 * _lockOptions.AutoRenewalRatio / 2));
            _renewalTimer = new Timer(PerformAutoRenewal, null, renewalInterval, renewalInterval);
        }

        _logger.LogInformation("Redis分布式锁服务已初始化，配置: {Config}", _lockOptions.GetConfigurationSummary());
    }

    /// <summary>
    /// 尝试获取分布式锁
    /// </summary>
    public async Task<IDistributedLockHandle?> TryAcquireLockAsync(
        string resource,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null)
    {
        if (_disposed)
        {
            return null;
        }

        var lockKey = GetLockKey(resource);
        var lockValue = GenerateLockValue();
        var lockExpiry = expiry ?? TimeSpan.FromMinutes(_lockOptions.DefaultExpiryMinutes);
        var lockTimeout = timeout ?? TimeSpan.FromSeconds(_lockOptions.DefaultTimeoutSeconds);

        var endTime = DateTime.UtcNow.Add(lockTimeout);

        try
        {
            while (DateTime.UtcNow < endTime)
            {
                var database = _redisManager.GetDatabase();
                
                // 尝试获取锁（使用SET NX EX命令）
                var acquired = await database.StringSetAsync(lockKey, lockValue, lockExpiry, When.NotExists);

                if (acquired)
                {
                    var distributedLock = new DistributedLock
                    {
                        Key = lockKey,
                        Value = lockValue,
                        Resource = resource,
                        ExpiryTime = DateTime.UtcNow.Add(lockExpiry),
                        AcquiredAt = DateTime.UtcNow
                    };

                    _activeLocks.TryAdd(lockKey, distributedLock);
                    Interlocked.Increment(ref _lockAcquiredCount);

                    _logger.LogDebug("分布式锁获取成功: {Resource}, 过期时间: {Expiry}", resource, lockExpiry);
                    return new DistributedLockHandle(this, distributedLock);
                }

                // 等待后重试
                await Task.Delay(_lockOptions.RetryIntervalMs);
            }

            Interlocked.Increment(ref _lockTimeoutCount);
            _logger.LogWarning("分布式锁获取超时: {Resource}, 超时时间: {Timeout}", resource, lockTimeout);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分布式锁时发生错误: {Resource}", resource);
            return null;
        }
    }

    /// <summary>
    /// 释放分布式锁
    /// </summary>
    internal async Task<bool> ReleaseLockAsync(DistributedLock distributedLock)
    {
        if (_disposed)
        {
            return false;
        }

        try
        {
            var database = _redisManager.GetDatabase();
            
            // 使用Lua脚本确保只有锁的持有者才能释放锁
            const string script = @"
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('DEL', KEYS[1])
                else
                    return 0
                end";

            var result = await database.ScriptEvaluateAsync(script, new RedisKey[] { distributedLock.Key }, new RedisValue[] { distributedLock.Value });

            var released = result.ToString() == "1";
            
            if (released)
            {
                _activeLocks.TryRemove(distributedLock.Key, out _);
                Interlocked.Increment(ref _lockReleasedCount);
                _logger.LogDebug("分布式锁释放成功: {Resource}", distributedLock.Resource);
            }
            else
            {
                _logger.LogWarning("分布式锁释放失败，可能已过期: {Resource}", distributedLock.Resource);
            }

            return released;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放分布式锁时发生错误: {Resource}", distributedLock.Resource);
            return false;
        }
    }

    /// <summary>
    /// 续约分布式锁
    /// </summary>
    public async Task<bool> RenewLockAsync(string resource, TimeSpan? expiry = null)
    {
        if (_disposed)
        {
            return false;
        }

        var lockKey = GetLockKey(resource);
        if (!_activeLocks.TryGetValue(lockKey, out var distributedLock))
        {
            return false;
        }

        try
        {
            var database = _redisManager.GetDatabase();
            var lockExpiry = expiry ?? TimeSpan.FromMinutes(_lockOptions.DefaultExpiryMinutes);

            // 使用Lua脚本确保只有锁的持有者才能续约
            const string script = @"
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('EXPIRE', KEYS[1], ARGV[2])
                else
                    return 0
                end";

            var result = await database.ScriptEvaluateAsync(script,
                new RedisKey[] { distributedLock.Key },
                new RedisValue[] { distributedLock.Value, (int)lockExpiry.TotalSeconds });

            var renewed = result.ToString() == "1";

            if (renewed)
            {
                distributedLock.ExpiryTime = DateTime.UtcNow.Add(lockExpiry);
                Interlocked.Increment(ref _lockRenewalCount);
                _logger.LogDebug("分布式锁续约成功: {Resource}, 新过期时间: {Expiry}", resource, lockExpiry);
            }

            return renewed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "续约分布式锁时发生错误: {Resource}", resource);
            return false;
        }
    }

    /// <summary>
    /// 获取锁的统计信息
    /// </summary>
    public LockStatistics GetStatistics()
    {
        return new LockStatistics
        {
            LockAcquiredCount = _lockAcquiredCount,
            LockReleasedCount = _lockReleasedCount,
            LockTimeoutCount = _lockTimeoutCount,
            LockRenewalCount = _lockRenewalCount,
            ActiveLocksCount = _activeLocks.Count,
            ConfigurationSummary = _lockOptions.GetConfigurationSummary()
        };
    }

    /// <summary>
    /// 执行自动续约
    /// </summary>
    private void PerformAutoRenewal(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var locksToRenew = new List<DistributedLock>();

            foreach (var kvp in _activeLocks)
            {
                var distributedLock = kvp.Value;
                var timeToExpiry = distributedLock.ExpiryTime - now;
                var totalExpiry = distributedLock.ExpiryTime - distributedLock.AcquiredAt;
                var renewalThreshold = totalExpiry.TotalMilliseconds * _lockOptions.AutoRenewalRatio;

                if (timeToExpiry.TotalMilliseconds <= renewalThreshold)
                {
                    locksToRenew.Add(distributedLock);
                }
            }

            if (locksToRenew.Count > 0)
            {
                _ = Task.Run(async () =>
                {
                    foreach (var distributedLock in locksToRenew)
                    {
                        try
                        {
                            await RenewLockAsync(distributedLock.Resource);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "自动续约失败: {Resource}", distributedLock.Resource);
                        }
                    }
                });

                _logger.LogDebug("触发自动续约，锁数量: {Count}", locksToRenew.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行自动续约时发生错误");
        }
    }

    /// <summary>
    /// 生成锁键名
    /// </summary>
    private string GetLockKey(string resource)
    {
        return $"{_lockOptions.KeyPrefix}{resource}";
    }

    /// <summary>
    /// 生成锁值
    /// </summary>
    private static string GenerateLockValue()
    {
        return $"{Environment.MachineName}:{Environment.ProcessId}:{Thread.CurrentThread.ManagedThreadId}:{Guid.NewGuid():N}";
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
        _renewalTimer?.Dispose();

        // 释放所有活跃的锁
        var lockReleaseTask = Task.Run(async () =>
        {
            var releaseTasks = _activeLocks.Values.Select(ReleaseLockAsync);
            await Task.WhenAll(releaseTasks);
        });

        try
        {
            lockReleaseTask.Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放活跃锁时发生错误");
        }

        _activeLocks.Clear();
        _logger.LogInformation("Redis分布式锁服务已释放");
    }
}

/// <summary>
/// 分布式锁
/// </summary>
public class DistributedLock
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public DateTime ExpiryTime { get; set; }
    public DateTime AcquiredAt { get; set; }
}

/// <summary>
/// 分布式锁句柄
/// </summary>
public interface IDistributedLockHandle : IDisposable
{
    string Resource { get; }
    DateTime ExpiryTime { get; }
    bool IsValid { get; }
    Task<bool> RenewAsync(TimeSpan? expiry = null);
}

/// <summary>
/// 分布式锁句柄实现
/// </summary>
public class DistributedLockHandle : IDistributedLockHandle
{
    private readonly RedisDistributedLockService _lockService;
    private readonly DistributedLock _distributedLock;
    private volatile bool _disposed = false;

    public DistributedLockHandle(RedisDistributedLockService lockService, DistributedLock distributedLock)
    {
        _lockService = lockService;
        _distributedLock = distributedLock;
    }

    public string Resource => _distributedLock.Resource;
    public DateTime ExpiryTime => _distributedLock.ExpiryTime;
    public bool IsValid => !_disposed && DateTime.UtcNow < _distributedLock.ExpiryTime;

    public async Task<bool> RenewAsync(TimeSpan? expiry = null)
    {
        if (_disposed)
        {
            return false;
        }

        return await _lockService.RenewLockAsync(_distributedLock.Resource, expiry);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        
        _ = Task.Run(async () =>
        {
            try
            {
                await _lockService.ReleaseLockAsync(_distributedLock);
            }
            catch (Exception)
            {
                // 忽略释放时的异常，锁会自动过期
            }
        });
    }
}

/// <summary>
/// 锁统计信息
/// </summary>
public class LockStatistics
{
    public long LockAcquiredCount { get; set; }
    public long LockReleasedCount { get; set; }
    public long LockTimeoutCount { get; set; }
    public long LockRenewalCount { get; set; }
    public int ActiveLocksCount { get; set; }
    public string ConfigurationSummary { get; set; } = string.Empty;
}
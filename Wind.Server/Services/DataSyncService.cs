using Microsoft.Extensions.Options;
using StackExchange.Redis;
using MongoDB.Driver;
using System.Collections.Concurrent;
using System.Text.Json;
using Wind.Server.Configuration;
using Wind.Server.Models.Documents;
using Wind.Shared.Models;

namespace Wind.Server.Services;

/// <summary>
/// 数据同步服务实现
/// 提供Redis缓存与MongoDB持久化之间的多种同步策略
/// </summary>
public class DataSyncService : IDataSyncService, IDisposable
{
    private readonly RedisConnectionManager _redisManager;
    private readonly MongoDbConnectionManager _mongoManager;
    private readonly IPlayerPersistenceService _playerPersistence;
    private readonly IRoomPersistenceService _roomPersistence;
    private readonly IGameRecordPersistenceService _gameRecordPersistence;
    private readonly ILogger<DataSyncService> _logger;
    private readonly DataSyncOptions _options;
    
    // 统计信息
    private long _cacheHits = 0;
    private long _cacheMisses = 0;
    private long _writeThroughCount = 0;
    private long _writeBehindCount = 0;
    private long _syncFailureCount = 0;
    private DateTime _lastFlushTime = DateTime.UtcNow;
    
    // Write-Behind缓冲区
    private readonly ConcurrentQueue<WriteBehindItem> _writeBehindQueue = new();
    private readonly Timer _flushTimer;
    private volatile bool _disposed = false;

    public DataSyncService(
        RedisConnectionManager redisManager,
        MongoDbConnectionManager mongoManager,
        IPlayerPersistenceService playerPersistence,
        IRoomPersistenceService roomPersistence,
        IGameRecordPersistenceService gameRecordPersistence,
        IOptions<DataSyncOptions> options,
        ILogger<DataSyncService> logger)
    {
        _redisManager = redisManager;
        _mongoManager = mongoManager;
        _playerPersistence = playerPersistence;
        _roomPersistence = roomPersistence;
        _gameRecordPersistence = gameRecordPersistence;
        _options = options.Value;
        _logger = logger;

        // 启动定时刷新计时器
        _flushTimer = new Timer(
            async _ => await FlushPendingWrites(),
            null,
            TimeSpan.FromMilliseconds(_options.FlushIntervalMs),
            TimeSpan.FromMilliseconds(_options.FlushIntervalMs));

        _logger.LogInformation("数据同步服务已启动，刷新间隔: {FlushInterval}ms", _options.FlushIntervalMs);
    }

    /// <summary>
    /// Write-Through策略：同步写入缓存和持久化存储
    /// </summary>
    public async Task WriteThrough<T>(string key, T data, TimeSpan? expiry = null) where T : class
    {
        ThrowIfDisposed();
        
        try
        {
            var database = _redisManager.GetDatabase();
            var json = JsonSerializer.Serialize(data);

            // 同时写入Redis和MongoDB
            var redisTask = database.StringSetAsync(key, json, expiry);
            var mongoTask = UpsertToSpecializedPersistence(key, data);

            await Task.WhenAll(redisTask, mongoTask);

            Interlocked.Increment(ref _writeThroughCount);
            _logger.LogDebug("Write-Through完成: {Key}", key);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _syncFailureCount);
            _logger.LogError(ex, "Write-Through失败: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// 玩家数据专用Write-Through策略
    /// </summary>
    public async Task WriteThroughPlayerState(string playerId, PlayerState playerState, TimeSpan? expiry = null)
    {
        ThrowIfDisposed();
        
        try
        {
            var database = _redisManager.GetDatabase();
            var key = $"player:{playerId}";
            var json = JsonSerializer.Serialize(playerState);

            // 同时写入Redis和MongoDB
            var redisTask = database.StringSetAsync(key, json, expiry);
            var mongoTask = _playerPersistence.SavePlayerAsync(playerState);

            await Task.WhenAll(redisTask, mongoTask);

            Interlocked.Increment(ref _writeThroughCount);
            _logger.LogDebug("玩家数据Write-Through完成: {PlayerId}", playerId);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _syncFailureCount);
            _logger.LogError(ex, "玩家数据Write-Through失败: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// 房间数据专用Write-Through策略
    /// </summary>
    public async Task WriteThroughRoomState(string roomId, RoomState roomState, TimeSpan? expiry = null)
    {
        ThrowIfDisposed();
        
        try
        {
            var database = _redisManager.GetDatabase();
            var key = $"room:{roomId}";
            var json = JsonSerializer.Serialize(roomState);

            // 同时写入Redis和MongoDB
            var redisTask = database.StringSetAsync(key, json, expiry);
            var mongoTask = _roomPersistence.SaveRoomAsync(roomState);

            await Task.WhenAll(redisTask, mongoTask);

            Interlocked.Increment(ref _writeThroughCount);
            _logger.LogDebug("房间数据Write-Through完成: {RoomId}", roomId);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _syncFailureCount);
            _logger.LogError(ex, "房间数据Write-Through失败: {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// 游戏记录专用Write-Through策略
    /// </summary>
    public async Task WriteThroughGameRecord(string gameId, GameRecordDocument gameRecord, TimeSpan? expiry = null)
    {
        ThrowIfDisposed();
        
        try
        {
            var database = _redisManager.GetDatabase();
            var key = $"game:{gameId}";
            var json = JsonSerializer.Serialize(gameRecord);

            // 同时写入Redis和MongoDB
            var redisTask = database.StringSetAsync(key, json, expiry);
            var mongoTask = _gameRecordPersistence.SaveGameRecordAsync(gameRecord);

            await Task.WhenAll(redisTask, mongoTask);

            Interlocked.Increment(ref _writeThroughCount);
            _logger.LogDebug("游戏记录Write-Through完成: {GameId}", gameId);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _syncFailureCount);
            _logger.LogError(ex, "游戏记录Write-Through失败: {GameId}", gameId);
            throw;
        }
    }

    /// <summary>
    /// Write-Behind策略：立即写入缓存，异步批量持久化
    /// </summary>
    public async Task WriteBehind<T>(string key, T data, TimeSpan? expiry = null) where T : class
    {
        ThrowIfDisposed();
        
        try
        {
            // 立即写入Redis缓存
            var database = _redisManager.GetDatabase();
            var json = JsonSerializer.Serialize(data);
            await database.StringSetAsync(key, json, expiry);

            // 加入Write-Behind队列
            _writeBehindQueue.Enqueue(new WriteBehindItem
            {
                Key = key,
                Data = data,
                DataType = typeof(T),
                Timestamp = DateTime.UtcNow
            });

            Interlocked.Increment(ref _writeBehindCount);
            _logger.LogDebug("Write-Behind缓存完成: {Key}", key);

            // 如果队列过满，立即刷新
            if (_writeBehindQueue.Count >= _options.MaxPendingWrites)
            {
                _ = Task.Run(async () => await FlushPendingWrites());
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _syncFailureCount);
            _logger.LogError(ex, "Write-Behind失败: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Cache-Aside策略：读取时检查缓存，未命中时从持久化存储加载
    /// </summary>
    public async Task<T?> CacheAside<T>(string key, Func<Task<T?>> loadFromPersistence) where T : class
    {
        ThrowIfDisposed();
        
        try
        {
            var database = _redisManager.GetDatabase();
            
            // 首先检查Redis缓存
            var cachedValue = await database.StringGetAsync(key);
            if (cachedValue.HasValue)
            {
                Interlocked.Increment(ref _cacheHits);
                _logger.LogDebug("缓存命中: {Key}", key);
                return JsonSerializer.Deserialize<T>(cachedValue!);
            }

            Interlocked.Increment(ref _cacheMisses);
            _logger.LogDebug("缓存未命中: {Key}", key);

            // 从持久化存储加载
            var data = await loadFromPersistence();
            if (data != null)
            {
                // 写入缓存
                var json = JsonSerializer.Serialize(data);
                var expiry = TimeSpan.FromSeconds(_options.DefaultCacheExpirySeconds);
                await database.StringSetAsync(key, json, expiry);
                _logger.LogDebug("数据已缓存: {Key}", key);
            }

            return data;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _syncFailureCount);
            _logger.LogError(ex, "Cache-Aside失败: {Key}", key);
            
            // 发生错误时直接从持久化存储加载
            return await loadFromPersistence();
        }
    }

    /// <summary>
    /// 删除数据（同时从缓存和持久化存储删除）
    /// </summary>
    public async Task Delete(string key)
    {
        ThrowIfDisposed();
        
        try
        {
            var database = _redisManager.GetDatabase();
            var redisTask = database.KeyDeleteAsync(key);

            // 从所有可能的MongoDB集合中删除
            var mongoTasks = new List<Task>();
            foreach (var collectionName in _options.MongoCollections)
            {
                var collection = _mongoManager.GetCollection<object>(collectionName);
                mongoTasks.Add(collection.DeleteOneAsync(Builders<object>.Filter.Eq("_id", key)));
            }

            await Task.WhenAll([redisTask, .. mongoTasks]);
            _logger.LogDebug("数据删除完成: {Key}", key);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _syncFailureCount);
            _logger.LogError(ex, "删除数据失败: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// 批量同步待处理的Write-Behind数据到持久化存储
    /// </summary>
    public async Task FlushPendingWrites()
    {
        if (_disposed || _writeBehindQueue.IsEmpty)
        {
            return;
        }

        var batchSize = Math.Min(_options.FlushBatchSize, _writeBehindQueue.Count);
        var items = new List<WriteBehindItem>(batchSize);

        // 出队待处理项目
        for (int i = 0; i < batchSize && _writeBehindQueue.TryDequeue(out var item); i++)
        {
            items.Add(item);
        }

        if (items.Count == 0)
        {
            return;
        }

        try
        {
            _logger.LogDebug("开始批量刷新Write-Behind数据，数量: {Count}", items.Count);

            // 按数据类型分组
            var groupedItems = items.GroupBy(item => item.DataType);

            var tasks = new List<Task>();
            foreach (var group in groupedItems)
            {
                tasks.Add(FlushGroup(group.Key, group.ToList()));
            }

            await Task.WhenAll(tasks);
            _lastFlushTime = DateTime.UtcNow;

            _logger.LogDebug("批量刷新完成，处理项目数: {Count}", items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量刷新失败");
            
            // 重新入队失败的项目
            foreach (var item in items)
            {
                _writeBehindQueue.Enqueue(item);
            }
            
            Interlocked.Increment(ref _syncFailureCount);
        }
    }

    /// <summary>
    /// 获取同步统计信息
    /// </summary>
    public Task<DataSyncStats> GetSyncStats()
    {
        ThrowIfDisposed();
        
        return Task.FromResult(new DataSyncStats
        {
            CacheHits = Interlocked.Read(ref _cacheHits),
            CacheMisses = Interlocked.Read(ref _cacheMisses),
            WriteThroughCount = Interlocked.Read(ref _writeThroughCount),
            WriteBehindCount = Interlocked.Read(ref _writeBehindCount),
            PendingWriteBehindCount = _writeBehindQueue.Count,
            LastFlushTime = _lastFlushTime,
            SyncFailureCount = Interlocked.Read(ref _syncFailureCount)
        });
    }

    /// <summary>
    /// 刷新特定类型的数据组
    /// </summary>
    private async Task FlushGroup(Type dataType, List<WriteBehindItem> items)
    {
        try
        {
            // 使用反射获取泛型方法
            var method = typeof(DataSyncService).GetMethod(nameof(FlushGroupGeneric), 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var genericMethod = method!.MakeGenericMethod(dataType);
            
            var task = (Task)genericMethod.Invoke(this, [items])!;
            await task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新数据组失败，类型: {DataType}", dataType.Name);
            throw;
        }
    }

    /// <summary>
    /// 刷新特定类型的数据组（泛型版本）
    /// </summary>
    private async Task FlushGroupGeneric<T>(List<WriteBehindItem> items) where T : class
    {
        var collection = GetMongoCollection<T>();
        
        foreach (var item in items)
        {
            try
            {
                await UpsertToMongo(collection, item.Key, (T)item.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入MongoDB失败: {Key}, Type: {Type}", item.Key, typeof(T).Name);
                throw;
            }
        }
    }

    /// <summary>
    /// 获取MongoDB集合
    /// </summary>
    private IMongoCollection<T> GetMongoCollection<T>() where T : class
    {
        var collectionName = _options.GetCollectionName<T>();
        return _mongoManager.GetCollection<T>(collectionName);
    }

    /// <summary>
    /// 更新或插入到MongoDB
    /// </summary>
    private async Task UpsertToMongo<T>(IMongoCollection<T> collection, string key, T data) where T : class
    {
        var filter = Builders<T>.Filter.Eq("_id", key);
        var options = new ReplaceOptions { IsUpsert = true };
        await collection.ReplaceOneAsync(filter, data, options);
    }

    /// <summary>
    /// 根据数据类型选择专用的持久化服务
    /// </summary>
    private async Task UpsertToSpecializedPersistence<T>(string key, T data) where T : class
    {
        try
        {
            switch (data)
            {
                case PlayerState playerState:
                    await _playerPersistence.SavePlayerAsync(playerState);
                    break;
                case RoomState roomState:
                    await _roomPersistence.SaveRoomAsync(roomState);
                    break;
                case GameRecordDocument gameRecord:
                    await _gameRecordPersistence.SaveGameRecordAsync(gameRecord);
                    break;
                default:
                    // 对于其他类型，使用通用的MongoDB操作
                    var collection = GetMongoCollection<T>();
                    await UpsertToMongo(collection, key, data);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "专用持久化服务操作失败: {Key}, Type: {Type}", key, typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// 检查对象是否已释放
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DataSyncService));
        }
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

        // 在释放前最后一次刷新
        try
        {
            FlushPendingWrites().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "释放时最终刷新失败");
        }

        _flushTimer?.Dispose();
        _logger.LogInformation("数据同步服务已释放");
    }
}

/// <summary>
/// Write-Behind队列项目
/// </summary>
internal class WriteBehindItem
{
    public required string Key { get; set; }
    public required object Data { get; set; }
    public required Type DataType { get; set; }
    public DateTime Timestamp { get; set; }
}
using StackExchange.Redis;
using Microsoft.Extensions.Options;
using Wind.Server.Configuration;
using Microsoft.Extensions.Logging;
using Wind.Shared.Models;
using Wind.Shared.Services;
using MessagePack;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Wind.Server.Services;

/// <summary>
/// Redis缓存策略服务
/// 实现ICacheStrategy接口，提供LRU淘汰、缓存预热、命中率监控等高级功能
/// </summary>
public class RedisCacheStrategy : ICacheStrategy, IDisposable
{
    private readonly IDatabase _database;
    private readonly IServer _server;
    private readonly RedisOptions _redisOptions;
    private readonly LruCacheOptions _lruOptions;
    private readonly ILogger<RedisCacheStrategy> _logger;
    
    // 预定义的TTL策略
    private readonly Dictionary<string, TimeSpan> _ttlStrategies;
    
    // LRU和统计相关字段
    private readonly ConcurrentDictionary<string, DateTime> _accessTimes;
    private readonly CacheStatistics _statistics;
    private readonly Timer _cleanupTimer;
    private readonly object _statsLock = new();

    public RedisCacheStrategy(
        IConnectionMultiplexer redis, 
        IOptions<RedisOptions> redisOptions, 
        IOptions<LruCacheOptions> lruOptions,
        ILogger<RedisCacheStrategy> logger)
    {
        _database = redis.GetDatabase();
        _server = redis.GetServer(redis.GetEndPoints().First());
        _redisOptions = redisOptions.Value;
        _lruOptions = lruOptions.Value;
        _logger = logger;
        
        _accessTimes = new ConcurrentDictionary<string, DateTime>();
        _statistics = new CacheStatistics();
        
        // 初始化TTL策略映射 - 基于Orleans Redis存储优化
        _ttlStrategies = new Dictionary<string, TimeSpan>
        {
            // Orleans PlayerStorage (DB0) 相关缓存 - 适中TTL，频繁访问
            ["player_state"] = TimeSpan.FromMinutes(45), // 与Orleans存储同步更久
            ["player_info"] = TimeSpan.FromMinutes(30),
            ["player_position"] = TimeSpan.FromMinutes(20), // 位置更新频繁，适当延长
            ["player_session"] = TimeSpan.FromHours(2), // 会话数据保持较长
            
            // Orleans RoomStorage (DB1) 相关缓存 - 中等TTL，房间活跃时频繁访问
            ["room_state"] = TimeSpan.FromMinutes(25), // 房间状态保持更久
            ["room_info"] = TimeSpan.FromMinutes(20),
            ["room_players"] = TimeSpan.FromMinutes(15), // 玩家列表变化频繁
            ["room_config"] = TimeSpan.FromHours(1), // 房间配置相对稳定
            
            // Orleans MatchmakingStorage (DB2) 相关缓存 - 短TTL，快速变化
            ["matchmaking"] = TimeSpan.FromMinutes(8), // 匹配队列延长保持
            ["matchmaking_queue"] = TimeSpan.FromMinutes(5),
            ["matchmaking_stats"] = TimeSpan.FromMinutes(10), // 统计信息可以稍长
            
            // 消息和通信数据 - 基于实时性需求
            ["message"] = TimeSpan.FromMinutes(15), // 消息历史适当延长
            ["chat_history"] = TimeSpan.FromMinutes(30),
            ["notification"] = TimeSpan.FromMinutes(10),
            
            // 系统级缓存 - 长TTL，变化较少
            ["system_config"] = TimeSpan.FromHours(2),
            ["game_config"] = TimeSpan.FromHours(1),
            ["auth_token"] = TimeSpan.FromMinutes(15), // JWT相关缓存
            
            // 临时和验证数据 - 极短TTL
            ["temp"] = TimeSpan.FromMinutes(2), // 临时数据稍微延长
            ["verification"] = TimeSpan.FromMinutes(5),
            ["rate_limit"] = TimeSpan.FromMinutes(1), // 限流数据
            
            // 性能优化缓存 - 基于访问模式
            ["lookup"] = TimeSpan.FromMinutes(30), // 查找类数据
            ["aggregation"] = TimeSpan.FromMinutes(20), // 聚合统计数据
            ["hot_data"] = TimeSpan.FromMinutes(60) // 热点数据保持更久
        };
        
        // 启动清理定时器
        _cleanupTimer = new Timer(PerformCleanup, null, _lruOptions.CleanupInterval, _lruOptions.CleanupInterval);
        
        _logger.LogInformation("Redis缓存策略已启动，最大容量: {MaxCapacity}, 清理间隔: {CleanupInterval}分钟", 
            _lruOptions.MaxCapacity, _lruOptions.CleanupInterval.TotalMinutes);
    }

    #region ICacheStrategy接口实现

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = BuildCacheKey(key);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var value = await _database.StringGetAsync(fullKey);
            stopwatch.Stop();

            if (value.HasValue)
            {
                // 更新访问时间（用于LRU）
                _accessTimes.AddOrUpdate(fullKey, DateTime.UtcNow, (k, v) => DateTime.UtcNow);
                
                // 更新统计信息
                RecordCacheHit(stopwatch.Elapsed);
                
                _logger.LogDebug("缓存命中: {Key}, 响应时间: {ResponseTime}ms", fullKey, stopwatch.ElapsedMilliseconds);
                
                return JsonSerializer.Deserialize<T>(value!);
            }

            RecordCacheMiss(stopwatch.Elapsed);
            _logger.LogDebug("缓存未命中: {Key}", fullKey);
            return default(T);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordCacheMiss(stopwatch.Elapsed);
            _logger.LogError(ex, "获取缓存异常: {Key}", fullKey);
            return default(T);
        }
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var fullKey = BuildCacheKey(key);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 检查容量限制
            if (await ShouldEvict())
            {
                await EvictLruAsync(_lruOptions.EvictionBatchSize, cancellationToken);
            }

            var jsonValue = JsonSerializer.Serialize(value);
            var cacheExpiry = expiry ?? _lruOptions.DefaultExpiry;

            var success = await _database.StringSetAsync(fullKey, jsonValue, cacheExpiry);
            stopwatch.Stop();

            if (success)
            {
                _accessTimes.AddOrUpdate(fullKey, DateTime.UtcNow, (k, v) => DateTime.UtcNow);
                
                lock (_statsLock)
                {
                    _statistics.TotalKeys++;
                }

                _logger.LogDebug("缓存设置成功: {Key}, 过期时间: {Expiry}s, 响应时间: {ResponseTime}ms", 
                    fullKey, cacheExpiry.TotalSeconds, stopwatch.ElapsedMilliseconds);
            }

            return success;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "设置缓存异常: {Key}", fullKey);
            return false;
        }
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = BuildCacheKey(key);

        try
        {
            var deleted = await _database.KeyDeleteAsync(fullKey);
            if (deleted)
            {
                _accessTimes.TryRemove(fullKey, out _);
                
                lock (_statsLock)
                {
                    _statistics.TotalKeys = Math.Max(0, _statistics.TotalKeys - 1);
                }

                _logger.LogDebug("缓存删除成功: {Key}", fullKey);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除缓存异常: {Key}", fullKey);
            return false;
        }
    }

    public async Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var fullKeys = keys.Select(BuildCacheKey).ToArray();
        var result = new Dictionary<string, T?>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var redisKeys = fullKeys.Select(k => (RedisKey)k).ToArray();
            var values = await _database.StringGetAsync(redisKeys);
            stopwatch.Stop();

            for (int i = 0; i < fullKeys.Length; i++)
            {
                var originalKey = keys.ElementAt(i);
                var fullKey = fullKeys[i];

                if (values[i].HasValue)
                {
                    _accessTimes.AddOrUpdate(fullKey, DateTime.UtcNow, (k, v) => DateTime.UtcNow);
                    result[originalKey] = JsonSerializer.Deserialize<T>(values[i]!);
                    RecordCacheHit(stopwatch.Elapsed);
                }
                else
                {
                    result[originalKey] = default(T);
                    RecordCacheMiss(stopwatch.Elapsed);
                }
            }

            _logger.LogDebug("批量获取缓存完成: {KeyCount}个键, 命中: {HitCount}个, 响应时间: {ResponseTime}ms", 
                fullKeys.Length, result.Values.Count(v => v != null), stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "批量获取缓存异常: {KeyCount}个键", fullKeys.Length);
            
            // 返回全部miss的结果
            foreach (var key in keys)
            {
                result[key] = default(T);
                RecordCacheMiss(stopwatch.Elapsed);
            }

            return result;
        }
    }

    public async Task<bool> SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        if (!keyValues.Any()) return true;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 检查容量限制
            if (await ShouldEvict())
            {
                await EvictLruAsync(_lruOptions.EvictionBatchSize, cancellationToken);
            }

            var batch = _database.CreateBatch();
            var tasks = new List<Task<bool>>();
            var cacheExpiry = expiry ?? _lruOptions.DefaultExpiry;
            var now = DateTime.UtcNow;

            foreach (var kvp in keyValues)
            {
                var fullKey = BuildCacheKey(kvp.Key);
                var jsonValue = JsonSerializer.Serialize(kvp.Value);
                
                tasks.Add(batch.StringSetAsync(fullKey, jsonValue, cacheExpiry));
                _accessTimes.AddOrUpdate(fullKey, now, (k, v) => now);
            }

            batch.Execute();
            await Task.WhenAll(tasks);
            stopwatch.Stop();

            var successCount = tasks.Count(t => t.Result);
            
            lock (_statsLock)
            {
                _statistics.TotalKeys += successCount;
            }

            _logger.LogDebug("批量设置缓存完成: {KeyCount}个键, 成功: {SuccessCount}个, 响应时间: {ResponseTime}ms", 
                keyValues.Count, successCount, stopwatch.ElapsedMilliseconds);

            return successCount == keyValues.Count;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "批量设置缓存异常: {KeyCount}个键", keyValues.Count);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = BuildCacheKey(key);

        try
        {
            var exists = await _database.KeyExistsAsync(fullKey);
            if (exists)
            {
                _accessTimes.AddOrUpdate(fullKey, DateTime.UtcNow, (k, v) => DateTime.UtcNow);
            }
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查缓存存在性异常: {Key}", fullKey);
            return false;
        }
    }

    public async Task<bool> RefreshAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var fullKey = BuildCacheKey(key);

        try
        {
            var refreshed = await _database.KeyExpireAsync(fullKey, expiry);
            if (refreshed)
            {
                _accessTimes.AddOrUpdate(fullKey, DateTime.UtcNow, (k, v) => DateTime.UtcNow);
                _logger.LogDebug("缓存过期时间更新: {Key}, 新过期时间: {Expiry}s", fullKey, expiry.TotalSeconds);
            }
            return refreshed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新缓存过期时间异常: {Key}", fullKey);
            return false;
        }
    }

    public async Task<CacheWarmupResult> WarmupAsync(IEnumerable<CacheWarmupItem> items, CancellationToken cancellationToken = default)
    {
        var sortedItems = items.OrderByDescending(x => x.Priority).ToList();
        var result = new CacheWarmupResult
        {
            TotalItems = sortedItems.Count
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("开始缓存预热: {ItemCount}个项目", sortedItems.Count);

            var batch = _database.CreateBatch();
            var tasks = new Dictionary<string, Task<bool>>();
            var now = DateTime.UtcNow;

            foreach (var item in sortedItems)
            {
                try
                {
                    var fullKey = BuildCacheKey(item.Key);
                    var jsonValue = JsonSerializer.Serialize(item.Value);
                    var expiry = item.Expiry ?? _lruOptions.DefaultExpiry;

                    tasks[item.Key] = batch.StringSetAsync(fullKey, jsonValue, expiry);
                    _accessTimes.AddOrUpdate(fullKey, now, (k, v) => now);
                }
                catch (Exception ex)
                {
                    result.FailedItems++;
                    result.FailedKeys.Add(item.Key);
                    result.ErrorMessages[item.Key] = ex.Message;
                    _logger.LogWarning(ex, "缓存预热项目准备失败: {Key}", item.Key);
                }
            }

            batch.Execute();

            // 等待所有任务完成
            foreach (var task in tasks)
            {
                try
                {
                    var success = await task.Value;
                    if (success)
                    {
                        result.SuccessfulItems++;
                    }
                    else
                    {
                        result.FailedItems++;
                        result.FailedKeys.Add(task.Key);
                        result.ErrorMessages[task.Key] = "Redis设置操作失败";
                    }
                }
                catch (Exception ex)
                {
                    result.FailedItems++;
                    result.FailedKeys.Add(task.Key);
                    result.ErrorMessages[task.Key] = ex.Message;
                    _logger.LogWarning(ex, "缓存预热项目执行失败: {Key}", task.Key);
                }
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            lock (_statsLock)
            {
                _statistics.TotalKeys += result.SuccessfulItems;
            }

            _logger.LogInformation("缓存预热完成: 总数={TotalItems}, 成功={SuccessfulItems}, 失败={FailedItems}, 耗时={Duration}ms",
                result.TotalItems, result.SuccessfulItems, result.FailedItems, result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            _logger.LogError(ex, "缓存预热异常");
            return result;
        }
    }

    public async Task<CacheStatistics> GetStatisticsAsync()
    {
        try
        {
            var info = await _server.InfoAsync("memory");
            var memoryUsage = 0L;
            
            if (info != null)
            {
                var infoString = info.ToString();
                foreach (var line in infoString.Split('\n'))
                {
                    if (line.StartsWith("used_memory:"))
                    {
                        long.TryParse(line.Split(':')[1].Trim(), out memoryUsage);
                        break;
                    }
                }
            }

            lock (_statsLock)
            {
                _statistics.MemoryUsage = memoryUsage;
                _statistics.Timestamp = DateTime.UtcNow;
                
                // 计算平均响应时间（简化版本）
                if (_statistics.TotalRequests > 0)
                {
                    _statistics.AverageResponseTime = TimeSpan.FromMilliseconds(
                        _statistics.AverageResponseTime.TotalMilliseconds * 0.9 + 
                        10 * 0.1); // 简化的移动平均
                }

                return new CacheStatistics
                {
                    TotalRequests = _statistics.TotalRequests,
                    CacheHits = _statistics.CacheHits,
                    CacheMisses = _statistics.CacheMisses,
                    TotalKeys = _statistics.TotalKeys,
                    MemoryUsage = _statistics.MemoryUsage,
                    ExpiredKeys = _statistics.ExpiredKeys,
                    LastCleanupTime = _statistics.LastCleanupTime,
                    AverageResponseTime = _statistics.AverageResponseTime,
                    KeyTypeStats = new Dictionary<string, long>(_statistics.KeyTypeStats),
                    Timestamp = _statistics.Timestamp
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取缓存统计信息异常");
            return _statistics;
        }
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = $"{_lruOptions.KeyPrefix}*";
            var keys = _server.Keys(pattern: pattern).ToArray();
            var expiredCount = 0;
            var batch = _database.CreateBatch();
            var tasks = new List<Task<bool>>();

            foreach (var key in keys)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var ttl = await _database.KeyTimeToLiveAsync(key);
                if (!ttl.HasValue || ttl.Value.TotalSeconds <= 0)
                {
                    tasks.Add(batch.KeyDeleteAsync(key));
                    _accessTimes.TryRemove(key, out _);
                    expiredCount++;
                }
            }

            if (tasks.Any())
            {
                batch.Execute();
                await Task.WhenAll(tasks);
            }

            lock (_statsLock)
            {
                _statistics.ExpiredKeys += expiredCount;
                _statistics.TotalKeys = Math.Max(0, _statistics.TotalKeys - expiredCount);
                _statistics.LastCleanupTime = DateTime.UtcNow;
            }

            _logger.LogInformation("清理过期缓存完成: 清理了{ExpiredCount}个过期键", expiredCount);
            return expiredCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期缓存异常");
            return 0;
        }
    }

    public async Task<int> EvictLruAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取最少使用的键
            var keysToEvict = _accessTimes
                .OrderBy(kvp => kvp.Value)
                .Take(maxItems)
                .Select(kvp => kvp.Key)
                .ToList();

            if (!keysToEvict.Any())
            {
                return 0;
            }

            var batch = _database.CreateBatch();
            var tasks = keysToEvict.Select(key => batch.KeyDeleteAsync(key)).ToArray();

            batch.Execute();
            await Task.WhenAll(tasks);

            var evictedCount = tasks.Count(t => t.Result);

            // 从访问时间记录中移除
            foreach (var key in keysToEvict)
            {
                _accessTimes.TryRemove(key, out _);
            }

            lock (_statsLock)
            {
                _statistics.TotalKeys = Math.Max(0, _statistics.TotalKeys - evictedCount);
            }

            _logger.LogInformation("LRU淘汰完成: 淘汰了{EvictedCount}个键", evictedCount);
            return evictedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LRU淘汰异常");
            return 0;
        }
    }

    #endregion

    #region 原有的TTL策略方法（兼容性保留）

    /// <summary>
    /// 设置字符串值并应用智能TTL策略
    /// </summary>
    public async Task<bool> SetWithTtlAsync<T>(string key, T value, string dataType = "default")
    {
        try
        {
            var serializedValue = MessagePackSerializer.Serialize(value);
            var ttl = GetTtlForDataType(dataType);
            var fullKey = BuildKey(key, dataType);
            
            var result = await _database.StringSetAsync(fullKey, serializedValue, ttl);
            
            _logger.LogDebug("设置缓存成功: Key={Key}, DataType={DataType}, TTL={TTL}s", 
                fullKey, dataType, ttl.TotalSeconds);
                
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置缓存失败: Key={Key}, DataType={DataType}", key, dataType);
            return false;
        }
    }

    /// <summary>
    /// 获取值并重置TTL（如果数据存在）
    /// </summary>
    public async Task<T?> GetWithTtlRefreshAsync<T>(string key, string dataType = "default")
    {
        try
        {
            var fullKey = BuildKey(key, dataType);
            var value = await _database.StringGetAsync(fullKey);
            
            if (!value.HasValue)
            {
                _logger.LogDebug("缓存未命中: Key={Key}", fullKey);
                return default(T);
            }

            // 重置TTL - 使用EXPIRE命令的NX选项确保键存在时才设置
            var ttl = GetTtlForDataType(dataType);
            await _database.KeyExpireAsync(fullKey, ttl, ExpireWhen.HasExpiry);
            
            var deserializedValue = MessagePackSerializer.Deserialize<T>(value);
            
            _logger.LogDebug("缓存命中并刷新TTL: Key={Key}, TTL={TTL}s", 
                fullKey, ttl.TotalSeconds);
                
            return deserializedValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取缓存失败: Key={Key}, DataType={DataType}", key, dataType);
            return default(T);
        }
    }

    /// <summary>
    /// 获取值和剩余TTL信息
    /// </summary>
    public async Task<(T? value, TimeSpan? remainingTtl)> GetWithTtlInfoAsync<T>(string key, string dataType = "default")
    {
        try
        {
            var fullKey = BuildKey(key, dataType);
            
            // 使用Redis Pipeline同时获取值和TTL
            var batch = _database.CreateBatch();
            var valueTask = batch.StringGetAsync(fullKey);
            var ttlTask = batch.KeyTimeToLiveAsync(fullKey);
            batch.Execute();
            
            var value = await valueTask;
            var remainingTtl = await ttlTask;
            
            if (!value.HasValue)
            {
                return (default(T), null);
            }

            var deserializedValue = MessagePackSerializer.Deserialize<T>(value);
            
            _logger.LogDebug("获取缓存和TTL信息: Key={Key}, 剩余TTL={TTL}s", 
                fullKey, remainingTtl?.TotalSeconds ?? -1);
                
            return (deserializedValue, remainingTtl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取缓存TTL信息失败: Key={Key}, DataType={DataType}", key, dataType);
            return (default(T), null);
        }
    }

    /// <summary>
    /// 条件性设置缓存（仅当键不存在时）
    /// </summary>
    public async Task<bool> SetIfNotExistsAsync<T>(string key, T value, string dataType = "default")
    {
        try
        {
            var serializedValue = MessagePackSerializer.Serialize(value);
            var ttl = GetTtlForDataType(dataType);
            var fullKey = BuildKey(key, dataType);
            
            var result = await _database.StringSetAsync(fullKey, serializedValue, ttl, When.NotExists);
            
            if (result)
            {
                _logger.LogDebug("条件设置缓存成功: Key={Key}, DataType={DataType}", fullKey, dataType);
            }
            else
            {
                _logger.LogDebug("条件设置缓存跳过（键已存在）: Key={Key}", fullKey);
            }
                
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "条件设置缓存失败: Key={Key}, DataType={DataType}", key, dataType);
            return false;
        }
    }

    /// <summary>
    /// 延长TTL（仅当新TTL大于当前TTL时）
    /// </summary>
    public async Task<bool> ExtendTtlAsync(string key, string dataType = "default", TimeSpan? customTtl = null)
    {
        try
        {
            var fullKey = BuildKey(key, dataType);
            var newTtl = customTtl ?? GetTtlForDataType(dataType);
            
            // 使用GT选项：仅当新过期时间大于当前过期时间时才设置
            var result = await _database.KeyExpireAsync(fullKey, newTtl, ExpireWhen.GreaterThanCurrentExpiry);
            
            _logger.LogDebug("延长TTL: Key={Key}, 新TTL={TTL}s, 结果={Result}", 
                fullKey, newTtl.TotalSeconds, result);
                
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "延长TTL失败: Key={Key}, DataType={DataType}", key, dataType);
            return false;
        }
    }

    /// <summary>
    /// 批量设置缓存数据
    /// </summary>
    public async Task<bool> SetBatchAsync<T>(Dictionary<string, T> keyValuePairs, string dataType = "default")
    {
        try
        {
            var batch = _database.CreateBatch();
            var tasks = new List<Task<bool>>();
            var ttl = GetTtlForDataType(dataType);
            
            foreach (var kvp in keyValuePairs)
            {
                var serializedValue = MessagePackSerializer.Serialize(kvp.Value);
                var fullKey = BuildKey(kvp.Key, dataType);
                tasks.Add(batch.StringSetAsync(fullKey, serializedValue, ttl));
            }
            
            batch.Execute();
            var results = await Task.WhenAll(tasks);
            
            var successCount = results.Count(r => r);
            _logger.LogDebug("批量设置缓存完成: 成功={Success}/{Total}, DataType={DataType}", 
                successCount, keyValuePairs.Count, dataType);
                
            return successCount == keyValuePairs.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量设置缓存失败: Count={Count}, DataType={DataType}", 
                keyValuePairs.Count, dataType);
            return false;
        }
    }


    /// <summary>
    /// 清理过期键（手动触发）
    /// </summary>
    public async Task<long> CleanupExpiredKeysAsync(string dataTypePattern = "*")
    {
        try
        {
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
            var pattern = BuildKey(dataTypePattern, "*");
            
            var keys = server.Keys(pattern: pattern).ToArray();
            var expiredKeys = new List<RedisKey>();
            
            // 检查每个键的TTL
            foreach (var key in keys)
            {
                var ttl = await _database.KeyTimeToLiveAsync(key);
                if (ttl.HasValue && ttl.Value.TotalSeconds <= 0)
                {
                    expiredKeys.Add(key);
                }
            }
            
            // 删除过期键
            if (expiredKeys.Count > 0)
            {
                var deletedCount = await _database.KeyDeleteAsync(expiredKeys.ToArray());
                _logger.LogInformation("手动清理过期键: 删除={Deleted}个键", deletedCount);
                return deletedCount;
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期键失败");
            return 0;
        }
    }

    #region 私有辅助方法

    private string BuildCacheKey(string key)
    {
        return $"{_lruOptions.KeyPrefix}{key}";
    }

    private void RecordCacheHit(TimeSpan responseTime)
    {
        if (!_lruOptions.EnableStatistics) return;

        lock (_statsLock)
        {
            _statistics.TotalRequests++;
            _statistics.CacheHits++;
            UpdateAverageResponseTime(responseTime);
        }
    }

    private void RecordCacheMiss(TimeSpan responseTime)
    {
        if (!_lruOptions.EnableStatistics) return;

        lock (_statsLock)
        {
            _statistics.TotalRequests++;
            _statistics.CacheMisses++;
            UpdateAverageResponseTime(responseTime);
        }
    }

    private void UpdateAverageResponseTime(TimeSpan responseTime)
    {
        if (_statistics.TotalRequests == 1)
        {
            _statistics.AverageResponseTime = responseTime;
        }
        else
        {
            // 简化的移动平均计算
            var avgMs = _statistics.AverageResponseTime.TotalMilliseconds;
            var newAvgMs = (avgMs * 0.9) + (responseTime.TotalMilliseconds * 0.1);
            _statistics.AverageResponseTime = TimeSpan.FromMilliseconds(newAvgMs);
        }
    }

    private async Task<bool> ShouldEvict()
    {
        try
        {
            var currentKeyCount = _accessTimes.Count;
            var threshold = _lruOptions.MaxCapacity * _lruOptions.EvictionThreshold;
            return currentKeyCount >= threshold;
        }
        catch
        {
            return false;
        }
    }

    private async void PerformCleanup(object? state)
    {
        try
        {
            _logger.LogDebug("开始定期缓存清理");
            await CleanupExpiredAsync();

            // 检查是否需要LRU淘汰
            if (await ShouldEvict())
            {
                await EvictLruAsync(_lruOptions.EvictionBatchSize);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "定期缓存清理异常");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _accessTimes.Clear();
        _logger.LogInformation("Redis缓存策略已释放");
    }

    private TimeSpan GetTtlForDataType(string dataType)
    {
        if (_ttlStrategies.TryGetValue(dataType.ToLowerInvariant(), out var ttl))
        {
            return ttl;
        }
        
        // 默认TTL从配置读取
        return TimeSpan.FromSeconds(_redisOptions.DefaultTtlSeconds);
    }

    private string BuildKey(string key, string dataType)
    {
        var prefix = _redisOptions.KeyPrefix ?? "Wind:v1.2:";
        return $"{prefix}{dataType}:{key}";
    }


    #endregion
}

#endregion


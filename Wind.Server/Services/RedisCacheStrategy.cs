using StackExchange.Redis;
using Microsoft.Extensions.Options;
using Wind.Server.Configuration;
using Microsoft.Extensions.Logging;
using Wind.Shared.Models;
using MessagePack;

namespace Wind.Server.Services;

/// <summary>
/// Redis缓存策略服务
/// 管理不同数据类型的TTL过期策略和缓存性能优化
/// </summary>
public class RedisCacheStrategy
{
    private readonly IDatabase _database;
    private readonly RedisOptions _redisOptions;
    private readonly ILogger<RedisCacheStrategy> _logger;
    
    // 预定义的TTL策略
    private readonly Dictionary<string, TimeSpan> _ttlStrategies;

    public RedisCacheStrategy(IConnectionMultiplexer redis, IOptions<RedisOptions> redisOptions, ILogger<RedisCacheStrategy> logger)
    {
        _database = redis.GetDatabase();
        _redisOptions = redisOptions.Value;
        _logger = logger;
        
        // 初始化TTL策略映射
        _ttlStrategies = new Dictionary<string, TimeSpan>
        {
            // 用户会话数据 - 较长TTL (2小时)
            ["session"] = TimeSpan.FromHours(2),
            ["user_session"] = TimeSpan.FromHours(2),
            
            // 玩家状态数据 - 中等TTL (30分钟)
            ["player_state"] = TimeSpan.FromMinutes(30),
            ["player_info"] = TimeSpan.FromMinutes(30),
            ["player_position"] = TimeSpan.FromMinutes(15),
            
            // 房间数据 - 中等TTL (15分钟)
            ["room_state"] = TimeSpan.FromMinutes(15),
            ["room_info"] = TimeSpan.FromMinutes(15),
            
            // 匹配数据 - 短TTL (5分钟)
            ["matchmaking"] = TimeSpan.FromMinutes(5),
            ["queue_info"] = TimeSpan.FromMinutes(5),
            
            // 消息数据 - 短TTL (10分钟)
            ["message"] = TimeSpan.FromMinutes(10),
            ["chat_history"] = TimeSpan.FromMinutes(30),
            
            // 临时数据 - 极短TTL (1分钟)
            ["temp"] = TimeSpan.FromMinutes(1),
            ["verification"] = TimeSpan.FromMinutes(5),
            
            // 配置数据 - 长TTL (1小时)
            ["config"] = TimeSpan.FromHours(1),
            ["system_config"] = TimeSpan.FromHours(2)
        };
    }

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
    /// 获取缓存统计信息
    /// </summary>
    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        try
        {
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
            var info = await server.InfoAsync("memory");
            var keyspace = await server.InfoAsync("keyspace");
            
            var stats = new CacheStatistics
            {
                UsedMemory = GetInfoValue(info, "used_memory"),
                MaxMemory = GetInfoValue(info, "maxmemory"),
                TotalKeys = GetKeyspaceKeys(keyspace),
                ExpiredKeys = GetInfoValue(info, "expired_keys"),
                EvictedKeys = GetInfoValue(info, "evicted_keys"),
                HitRate = CalculateHitRate(info),
                Timestamp = DateTime.UtcNow
            };
            
            _logger.LogDebug("缓存统计: 内存使用={UsedMemory}MB, 总键数={TotalKeys}, 命中率={HitRate}%", 
                stats.UsedMemory / 1024 / 1024, stats.TotalKeys, stats.HitRate);
                
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取缓存统计失败");
            return new CacheStatistics { Timestamp = DateTime.UtcNow };
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

    private long GetInfoValue(IGrouping<string, KeyValuePair<string, string>>[] info, string key)
    {
        var section = info.FirstOrDefault();
        if (section != null)
        {
            var kvp = section.FirstOrDefault(kv => kv.Key == key);
            if (long.TryParse(kvp.Value, out var value))
            {
                return value;
            }
        }
        return 0;
    }

    private long GetKeyspaceKeys(IGrouping<string, KeyValuePair<string, string>>[] keyspace)
    {
        var db0 = keyspace.FirstOrDefault(g => g.Key == "db0");
        if (db0 != null)
        {
            var keysInfo = db0.FirstOrDefault(kv => kv.Key.StartsWith("keys="));
            if (!string.IsNullOrEmpty(keysInfo.Value))
            {
                var keysStr = keysInfo.Value.Split(',')[0].Replace("keys=", "");
                if (long.TryParse(keysStr, out var keys))
                {
                    return keys;
                }
            }
        }
        return 0;
    }

    private double CalculateHitRate(IGrouping<string, KeyValuePair<string, string>>[] info)
    {
        var hits = GetInfoValue(info, "keyspace_hits");
        var misses = GetInfoValue(info, "keyspace_misses");
        var total = hits + misses;
        
        if (total == 0) return 0.0;
        return (double)hits / total * 100.0;
    }

    #endregion
}

/// <summary>
/// 缓存统计信息
/// </summary>
public class CacheStatistics
{
    public long UsedMemory { get; set; }
    public long MaxMemory { get; set; }
    public long TotalKeys { get; set; }
    public long ExpiredKeys { get; set; }
    public long EvictedKeys { get; set; }
    public double HitRate { get; set; }
    public DateTime Timestamp { get; set; }
}
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wind.Server.Configuration;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Wind.Server.Services;

/// <summary>
/// Redis缓存策略服务
/// 提供TTL管理、LRU淘汰策略、内存优化功能
/// </summary>
public class RedisCacheStrategyService : IDisposable
{
    private readonly RedisConnectionManager _redisManager;
    private readonly LruCacheOptions _cacheOptions;
    private readonly ILogger<RedisCacheStrategyService> _logger;
    private readonly Timer? _cleanupTimer;
    private readonly ConcurrentDictionary<string, CacheEntry> _localCache;
    private readonly object _lockObject = new();
    private volatile bool _disposed = false;

    // 统计信息
    private long _hitCount = 0;
    private long _missCount = 0;
    private long _evictionCount = 0;
    private long _expiredCount = 0;

    public RedisCacheStrategyService(
        RedisConnectionManager redisManager,
        IOptions<LruCacheOptions> cacheOptions,
        ILogger<RedisCacheStrategyService> logger)
    {
        _redisManager = redisManager;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
        _localCache = new ConcurrentDictionary<string, CacheEntry>();

        // 验证配置
        _cacheOptions.Validate();

        // 启动定期清理任务
        if (_cacheOptions.EnableAutoCleanup)
        {
            var cleanupInterval = TimeSpan.FromMinutes(_cacheOptions.CleanupIntervalMinutes);
            _cleanupTimer = new Timer(PerformCleanup, null, cleanupInterval, cleanupInterval);
        }

        _logger.LogInformation("Redis缓存策略服务已初始化，最大容量: {MaxCapacity}, 目标命中率: {TargetHitRate}%",
            _cacheOptions.MaxCapacity, _cacheOptions.TargetHitRate);
    }

    /// <summary>
    /// 设置缓存项（带TTL和LRU策略）
    /// </summary>
    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        if (_disposed)
        {
            return false;
        }

        try
        {
            var database = _redisManager.GetDatabase();
            var prefixedKey = GetPrefixedKey(key);
            var expiryTime = expiry ?? TimeSpan.FromMinutes(_cacheOptions.DefaultExpiryMinutes);

            // 序列化数据
            var serializedValue = JsonSerializer.Serialize(value);
            
            // 设置Redis缓存
            var redisResult = await database.StringSetAsync(prefixedKey, serializedValue, expiryTime);

            if (redisResult)
            {
                // 更新本地LRU缓存
                UpdateLocalCache(key, new CacheEntry
                {
                    Value = serializedValue,
                    CreatedAt = DateTime.UtcNow,
                    ExpiryAt = DateTime.UtcNow.Add(expiryTime),
                    LastAccessAt = DateTime.UtcNow,
                    AccessCount = 1
                });

                // 检查是否需要清理
                if (_localCache.Count > _cacheOptions.MaxCapacity * _cacheOptions.EvictionThreshold)
                {
                    _ = Task.Run(PerformLruEviction);
                }

                _logger.LogDebug("缓存设置成功: {Key}, 过期时间: {Expiry}", key, expiryTime);
                return true;
            }

            _logger.LogWarning("Redis缓存设置失败: {Key}", key);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置缓存时发生错误: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// 获取缓存项（优先从本地LRU缓存获取）
    /// </summary>
    public async Task<T?> GetAsync<T>(string key)
    {
        if (_disposed)
        {
            return default(T);
        }

        try
        {
            // 首先尝试从本地LRU缓存获取
            if (_localCache.TryGetValue(key, out var localEntry))
            {
                if (localEntry.ExpiryAt > DateTime.UtcNow)
                {
                    // 更新访问信息
                    localEntry.LastAccessAt = DateTime.UtcNow;
                    localEntry.AccessCount++;
                    
                    Interlocked.Increment(ref _hitCount);
                    
                    var localResult = JsonSerializer.Deserialize<T>(localEntry.Value);
                    _logger.LogDebug("本地缓存命中: {Key}", key);
                    return localResult;
                }
                else
                {
                    // 本地缓存过期，移除
                    _localCache.TryRemove(key, out _);
                    Interlocked.Increment(ref _expiredCount);
                }
            }

            // 从Redis获取
            var database = _redisManager.GetDatabase();
            var prefixedKey = GetPrefixedKey(key);
            var redisValue = await database.StringGetAsync(prefixedKey);

            if (redisValue.HasValue)
            {
                // 更新本地缓存
                var ttl = await database.KeyTimeToLiveAsync(prefixedKey);
                var expiryTime = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : DateTime.UtcNow.AddMinutes(_cacheOptions.DefaultExpiryMinutes);
                
                UpdateLocalCache(key, new CacheEntry
                {
                    Value = redisValue,
                    CreatedAt = DateTime.UtcNow,
                    ExpiryAt = expiryTime,
                    LastAccessAt = DateTime.UtcNow,
                    AccessCount = 1
                });

                Interlocked.Increment(ref _hitCount);
                
                var result = JsonSerializer.Deserialize<T>(redisValue);
                _logger.LogDebug("Redis缓存命中: {Key}", key);
                return result;
            }

            Interlocked.Increment(ref _missCount);
            _logger.LogDebug("缓存未命中: {Key}", key);
            return default(T);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取缓存时发生错误: {Key}", key);
            Interlocked.Increment(ref _missCount);
            return default(T);
        }
    }

    /// <summary>
    /// 删除缓存项
    /// </summary>
    public async Task<bool> RemoveAsync(string key)
    {
        if (_disposed)
        {
            return false;
        }

        try
        {
            var database = _redisManager.GetDatabase();
            var prefixedKey = GetPrefixedKey(key);
            
            var redisResult = await database.KeyDeleteAsync(prefixedKey);
            _localCache.TryRemove(key, out _);

            _logger.LogDebug("缓存删除: {Key}, 结果: {Result}", key, redisResult);
            return redisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除缓存时发生错误: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// 批量删除缓存项
    /// </summary>
    public async Task<long> RemoveByPatternAsync(string pattern)
    {
        if (_disposed)
        {
            return 0;
        }

        try
        {
            var server = _redisManager.GetServer();
            var database = _redisManager.GetDatabase();
            var prefixedPattern = GetPrefixedKey(pattern);
            
            var keys = server.Keys(pattern: prefixedPattern).ToArray();
            
            if (keys.Length == 0)
            {
                return 0;
            }

            var deletedCount = await database.KeyDeleteAsync(keys);
            
            // 从本地缓存中移除匹配的键
            var localKeysToRemove = _localCache.Keys.Where(k => IsPatternMatch(k, pattern)).ToList();
            foreach (var localKey in localKeysToRemove)
            {
                _localCache.TryRemove(localKey, out _);
            }

            _logger.LogInformation("批量删除缓存完成，模式: {Pattern}, 删除数量: {Count}", pattern, deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量删除缓存时发生错误，模式: {Pattern}", pattern);
            return 0;
        }
    }

    /// <summary>
    /// 设置键的过期时间
    /// </summary>
    public async Task<bool> ExpireAsync(string key, TimeSpan expiry)
    {
        if (_disposed)
        {
            return false;
        }

        try
        {
            var database = _redisManager.GetDatabase();
            var prefixedKey = GetPrefixedKey(key);
            
            var result = await database.KeyExpireAsync(prefixedKey, expiry);
            
            // 更新本地缓存的过期时间
            if (_localCache.TryGetValue(key, out var entry))
            {
                entry.ExpiryAt = DateTime.UtcNow.Add(expiry);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置缓存过期时间时发生错误: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        var totalRequests = _hitCount + _missCount;
        var hitRate = totalRequests > 0 ? (double)_hitCount / totalRequests * 100 : 0;

        return new CacheStatistics
        {
            HitCount = _hitCount,
            MissCount = _missCount,
            HitRate = hitRate,
            EvictionCount = _evictionCount,
            ExpiredCount = _expiredCount,
            LocalCacheSize = _localCache.Count,
            TotalRequests = totalRequests,
            MaxCapacity = _cacheOptions.MaxCapacity,
            TargetHitRate = _cacheOptions.TargetHitRate
        };
    }

    /// <summary>
    /// 清理过期的本地缓存项
    /// </summary>
    private void PerformCleanup(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var expiredKeys = new List<string>();

            foreach (var kvp in _localCache)
            {
                if (kvp.Value.ExpiryAt <= now)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            var removedCount = 0;
            foreach (var key in expiredKeys)
            {
                if (_localCache.TryRemove(key, out _))
                {
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                Interlocked.Add(ref _expiredCount, removedCount);
                _logger.LogDebug("本地缓存清理完成，移除过期项: {Count}", removedCount);
            }

            // 检查缓存命中率，如果低于目标则触发优化
            var stats = GetStatistics();
            if (stats.HitRate < _cacheOptions.TargetHitRate && stats.TotalRequests > 100)
            {
                _logger.LogWarning("缓存命中率低于目标值: {CurrentRate}% < {TargetRate}%", 
                    stats.HitRate, _cacheOptions.TargetHitRate);
                
                // 可以在此处触发缓存预热或其他优化策略
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行缓存清理时发生错误");
        }
    }

    /// <summary>
    /// 执行LRU淘汰策略
    /// </summary>
    private void PerformLruEviction()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            lock (_lockObject)
            {
                if (_localCache.Count <= _cacheOptions.MaxCapacity)
                {
                    return;
                }

                var evictionCount = _cacheOptions.EvictionBatchSize;
                var candidatesForEviction = _localCache
                    .OrderBy(kvp => kvp.Value.LastAccessAt)
                    .ThenBy(kvp => kvp.Value.AccessCount)
                    .Take(evictionCount)
                    .Select(kvp => kvp.Key)
                    .ToList();

                var actualEvicted = 0;
                foreach (var key in candidatesForEviction)
                {
                    if (_localCache.TryRemove(key, out _))
                    {
                        actualEvicted++;
                    }
                }

                Interlocked.Add(ref _evictionCount, actualEvicted);
                _logger.LogDebug("LRU淘汰完成，移除项数: {Count}", actualEvicted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行LRU淘汰时发生错误");
        }
    }

    /// <summary>
    /// 更新本地缓存
    /// </summary>
    private void UpdateLocalCache(string key, CacheEntry entry)
    {
        _localCache.AddOrUpdate(key, entry, (k, oldEntry) =>
        {
            entry.AccessCount = oldEntry.AccessCount + 1;
            return entry;
        });
    }

    /// <summary>
    /// 获取带前缀的键
    /// </summary>
    private string GetPrefixedKey(string key)
    {
        return $"{_cacheOptions.KeyPrefix}{key}";
    }

    /// <summary>
    /// 检查键是否匹配模式
    /// </summary>
    private static bool IsPatternMatch(string key, string pattern)
    {
        // 简单的通配符匹配实现
        if (pattern.Contains("*"))
        {
            var regex = "^" + pattern.Replace("*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(key, regex);
        }
        return key.Equals(pattern, StringComparison.OrdinalIgnoreCase);
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
        _localCache.Clear();

        _logger.LogInformation("Redis缓存策略服务已释放");
    }
}

/// <summary>
/// 缓存项
/// </summary>
public class CacheEntry
{
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiryAt { get; set; }
    public DateTime LastAccessAt { get; set; }
    public long AccessCount { get; set; }
}

/// <summary>
/// 缓存统计信息
/// </summary>
public class CacheStatistics
{
    public long HitCount { get; set; }
    public long MissCount { get; set; }
    public double HitRate { get; set; }
    public long EvictionCount { get; set; }
    public long ExpiredCount { get; set; }
    public int LocalCacheSize { get; set; }
    public long TotalRequests { get; set; }
    public int MaxCapacity { get; set; }
    public double TargetHitRate { get; set; }
}
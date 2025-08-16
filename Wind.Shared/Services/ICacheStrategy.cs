using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wind.Shared.Services;

/// <summary>
/// 缓存策略接口
/// 提供LRU淘汰、缓存预热、命中率监控等功能
/// </summary>
public interface ICacheStrategy
{
    /// <summary>
    /// 获取缓存值
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置缓存值
    /// </summary>
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除缓存
    /// </summary>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取缓存
    /// </summary>
    Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量设置缓存
    /// </summary>
    Task<bool> SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查缓存是否存在
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新缓存过期时间
    /// </summary>
    Task<bool> RefreshAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 预热缓存
    /// </summary>
    Task<CacheWarmupResult> WarmupAsync(IEnumerable<CacheWarmupItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync();

    /// <summary>
    /// 清理过期缓存
    /// </summary>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行LRU淘汰
    /// </summary>
    Task<int> EvictLruAsync(int maxItems, CancellationToken cancellationToken = default);
}

/// <summary>
/// 缓存预热项目
/// </summary>
public class CacheWarmupItem
{
    public string Key { get; set; } = string.Empty;
    public object Value { get; set; } = default!;
    public TimeSpan? Expiry { get; set; }
    public int Priority { get; set; } = 0; // 优先级，数字越大优先级越高
}

/// <summary>
/// 缓存预热结果
/// </summary>
public class CacheWarmupResult
{
    public int TotalItems { get; set; }
    public int SuccessfulItems { get; set; }
    public int FailedItems { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> FailedKeys { get; set; } = new();
    public Dictionary<string, string> ErrorMessages { get; set; } = new();
}

/// <summary>
/// 缓存统计信息
/// </summary>
public class CacheStatistics
{
    public long TotalRequests { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests * 100 : 0;
    public long TotalKeys { get; set; }
    public long MemoryUsage { get; set; }
    public int ExpiredKeys { get; set; }
    public DateTime LastCleanupTime { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public Dictionary<string, long> KeyTypeStats { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// LRU缓存选项
/// </summary>
public class LruCacheOptions
{
    public int MaxCapacity { get; set; } = 10000;
    public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(15);
    public double EvictionThreshold { get; set; } = 0.8; // 达到80%容量时开始淘汰
    public int EvictionBatchSize { get; set; } = 100; // 每次淘汰的数量
    public bool EnableStatistics { get; set; } = true;
    public string KeyPrefix { get; set; } = "Wind:Cache:";
    
    // 配置属性（用于从appsettings.json读取）
    public int DefaultExpiryMinutes 
    { 
        get => (int)DefaultExpiry.TotalMinutes; 
        set => DefaultExpiry = TimeSpan.FromMinutes(value); 
    }
    
    public int CleanupIntervalMinutes 
    { 
        get => (int)CleanupInterval.TotalMinutes; 
        set => CleanupInterval = TimeSpan.FromMinutes(value); 
    }
    
    public bool EnableAutoCleanup { get; set; } = true;
    public double TargetHitRate { get; set; } = 90.0;
}
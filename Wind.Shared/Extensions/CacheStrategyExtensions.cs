using System;
using System.Threading;
using System.Threading.Tasks;
using Wind.Shared.Services;

namespace Wind.Shared.Extensions;

/// <summary>
/// 缓存策略扩展方法
/// 提供便捷的缓存操作方法
/// </summary>
public static class CacheStrategyExtensions
{
    /// <summary>
    /// 获取或设置缓存值
    /// 如果缓存中不存在，则执行工厂方法并设置缓存
    /// </summary>
    public static async Task<T> GetOrSetAsync<T>(
        this ICacheStrategy cacheStrategy,
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var cachedValue = await cacheStrategy.GetAsync<T>(key, cancellationToken);
        
        if (cachedValue != null && !cachedValue.Equals(default(T)))
        {
            return cachedValue;
        }

        var newValue = await factory();
        if (newValue != null && !newValue.Equals(default(T)))
        {
            await cacheStrategy.SetAsync(key, newValue, expiry, cancellationToken);
        }

        return newValue;
    }

    /// <summary>
    /// 玩家专用缓存键生成
    /// </summary>
    public static async Task<T?> GetPlayerCacheAsync<T>(
        this ICacheStrategy cacheStrategy,
        string playerId,
        string dataType,
        CancellationToken cancellationToken = default)
    {
        var key = $"player:{playerId}:{dataType}";
        return await cacheStrategy.GetAsync<T>(key, cancellationToken);
    }

    /// <summary>
    /// 玩家专用缓存设置
    /// </summary>
    public static async Task<bool> SetPlayerCacheAsync<T>(
        this ICacheStrategy cacheStrategy,
        string playerId,
        string dataType,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var key = $"player:{playerId}:{dataType}";
        return await cacheStrategy.SetAsync(key, value, expiry, cancellationToken);
    }

    /// <summary>
    /// 房间专用缓存键生成
    /// </summary>
    public static async Task<T?> GetRoomCacheAsync<T>(
        this ICacheStrategy cacheStrategy,
        string roomId,
        string dataType,
        CancellationToken cancellationToken = default)
    {
        var key = $"room:{roomId}:{dataType}";
        return await cacheStrategy.GetAsync<T>(key, cancellationToken);
    }

    /// <summary>
    /// 房间专用缓存设置
    /// </summary>
    public static async Task<bool> SetRoomCacheAsync<T>(
        this ICacheStrategy cacheStrategy,
        string roomId,
        string dataType,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var key = $"room:{roomId}:{dataType}";
        return await cacheStrategy.SetAsync(key, value, expiry, cancellationToken);
    }

    /// <summary>
    /// 清理特定模式的缓存
    /// </summary>
    public static async Task<int> CleanupByPatternAsync(
        this ICacheStrategy cacheStrategy,
        string pattern,
        CancellationToken cancellationToken = default)
    {
        // 这个方法需要Redis实现类支持模式匹配删除
        // 这里提供接口，具体实现在RedisCacheStrategy中
        return await cacheStrategy.CleanupExpiredAsync(cancellationToken);
    }

    /// <summary>
    /// 批量预热玩家缓存
    /// </summary>
    public static async Task<CacheWarmupResult> WarmupPlayerCacheAsync(
        this ICacheStrategy cacheStrategy,
        Dictionary<string, object> playerData,
        string dataType,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var items = playerData.Select(kvp => new CacheWarmupItem
        {
            Key = $"player:{kvp.Key}:{dataType}",
            Value = kvp.Value,
            Expiry = expiry,
            Priority = 1 // 默认优先级
        }).ToList();

        return await cacheStrategy.WarmupAsync(items, cancellationToken);
    }

    /// <summary>
    /// 检查缓存命中率是否达到目标
    /// </summary>
    public static async Task<bool> IsHitRateTargetMetAsync(
        this ICacheStrategy cacheStrategy,
        double targetHitRate = 90.0)
    {
        var stats = await cacheStrategy.GetStatisticsAsync();
        return stats.HitRate >= targetHitRate;
    }

    /// <summary>
    /// 获取缓存健康状态
    /// </summary>
    public static async Task<CacheHealthStatus> GetHealthStatusAsync(
        this ICacheStrategy cacheStrategy)
    {
        var stats = await cacheStrategy.GetStatisticsAsync();
        
        return new CacheHealthStatus
        {
            IsHealthy = stats.HitRate >= 90.0 && stats.TotalRequests > 0,
            HitRate = stats.HitRate,
            TotalRequests = stats.TotalRequests,
            AverageResponseTime = stats.AverageResponseTime,
            MemoryUsage = stats.MemoryUsage,
            Timestamp = stats.Timestamp,
            Recommendations = GenerateRecommendations(stats)
        };
    }

    private static List<string> GenerateRecommendations(CacheStatistics stats)
    {
        var recommendations = new List<string>();

        if (stats.HitRate < 90.0)
        {
            recommendations.Add("缓存命中率低于90%，建议检查缓存策略和预热机制");
        }

        if (stats.AverageResponseTime.TotalMilliseconds > 50)
        {
            recommendations.Add("平均响应时间超过50ms，建议优化Redis网络连接");
        }

        if (stats.TotalRequests < 100)
        {
            recommendations.Add("请求量较低，统计数据可能不够准确");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("缓存运行状况良好");
        }

        return recommendations;
    }
}

/// <summary>
/// 缓存健康状态
/// </summary>
public class CacheHealthStatus
{
    public bool IsHealthy { get; set; }
    public double HitRate { get; set; }
    public long TotalRequests { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public long MemoryUsage { get; set; }
    public DateTime Timestamp { get; set; }
    public List<string> Recommendations { get; set; } = new();
}
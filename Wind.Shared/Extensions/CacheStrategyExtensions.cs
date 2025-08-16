using System;
using System.Threading;
using System.Threading.Tasks;
using Wind.Shared.Services;
using Wind.Shared.Models;
using Wind.Shared.Protocols;

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
    /// 获取或设置玩家状态（带分布式锁保护）
    /// </summary>
    public static async Task<T?> GetOrSetPlayerWithLockAsync<T>(
        this ICacheStrategy cacheStrategy,
        IDistributedLock distributedLock,
        string playerId,
        string dataType,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var key = $"player:{playerId}:{dataType}";
        var lockKey = $"lock:player:{playerId}:{dataType}";
        
        // 先尝试从缓存获取，无需锁
        var cachedValue = await cacheStrategy.GetAsync<T>(key, cancellationToken);
        if (cachedValue != null && !cachedValue.Equals(default(T)))
        {
            return cachedValue;
        }

        // 缓存未命中，使用分布式锁保护
        var lockToken = await distributedLock.TryAcquireAsync(
            lockKey, 
            TimeSpan.FromSeconds(30), 
            TimeSpan.FromSeconds(5), 
            cancellationToken);

        if (lockToken == null)
        {
            // 获取锁失败，再次尝试读取缓存（可能其他进程已经设置）
            return await cacheStrategy.GetAsync<T>(key, cancellationToken);
        }

        try
        {
            // 双重检查
            cachedValue = await cacheStrategy.GetAsync<T>(key, cancellationToken);
            if (cachedValue != null && !cachedValue.Equals(default(T)))
            {
                return cachedValue;
            }

            // 执行工厂方法获取数据
            var newValue = await factory();
            if (newValue != null && !newValue.Equals(default(T)))
            {
                await cacheStrategy.SetAsync(key, newValue, expiry, cancellationToken);
            }

            return newValue;
        }
        finally
        {
            await lockToken.ReleaseAsync();
        }
    }

    /// <summary>
    /// 批量获取玩家状态缓存
    /// </summary>
    public static async Task<Dictionary<string, T?>> GetManyPlayersAsync<T>(
        this ICacheStrategy cacheStrategy,
        IEnumerable<string> playerIds,
        string dataType,
        CancellationToken cancellationToken = default)
    {
        var keys = playerIds.Select(id => $"player:{id}:{dataType}");
        var cacheResults = await cacheStrategy.GetManyAsync<T>(keys, cancellationToken);
        
        // 转换回以playerId为键的字典
        var result = new Dictionary<string, T?>();
        var playerIdArray = playerIds.ToArray();
        var keyArray = keys.ToArray();
        
        for (int i = 0; i < playerIdArray.Length; i++)
        {
            var playerId = playerIdArray[i];
            var key = keyArray[i];
            result[playerId] = cacheResults.TryGetValue(key, out var value) ? value : default(T);
        }
        
        return result;
    }

    /// <summary>
    /// 批量设置玩家状态缓存
    /// </summary>
    public static async Task<bool> SetManyPlayersAsync<T>(
        this ICacheStrategy cacheStrategy,
        Dictionary<string, T> playerData,
        string dataType,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var cacheData = playerData.ToDictionary(
            kvp => $"player:{kvp.Key}:{dataType}",
            kvp => kvp.Value
        );
        
        return await cacheStrategy.SetManyAsync(cacheData, expiry, cancellationToken);
    }

    /// <summary>
    /// 设置玩家会话缓存
    /// </summary>
    public static async Task<bool> SetPlayerSessionAsync(
        this ICacheStrategy cacheStrategy,
        string playerId,
        string sessionId,
        object sessionData,
        CancellationToken cancellationToken = default)
    {
        var key = $"player:{playerId}:session";
        var sessionInfo = new
        {
            SessionId = sessionId,
            Data = sessionData,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };
        
        // 会话数据使用2小时过期时间
        return await cacheStrategy.SetAsync(key, sessionInfo, TimeSpan.FromHours(2), cancellationToken);
    }

    /// <summary>
    /// 刷新玩家在线状态缓存
    /// </summary>
    public static async Task<bool> RefreshPlayerOnlineStatusAsync(
        this ICacheStrategy cacheStrategy,
        string playerId,
        PlayerOnlineStatus status,
        CancellationToken cancellationToken = default)
    {
        var key = $"player:{playerId}:online_status";
        var statusInfo = new
        {
            Status = status,
            UpdatedAt = DateTime.UtcNow
        };
        
        // 在线状态使用30分钟过期时间
        return await cacheStrategy.SetAsync(key, statusInfo, TimeSpan.FromMinutes(30), cancellationToken);
    }

    /// <summary>
    /// 设置临时验证数据（如验证码、一次性Token等）
    /// </summary>
    public static async Task<bool> SetTempVerificationAsync<T>(
        this ICacheStrategy cacheStrategy,
        string identifier,
        T data,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var key = $"verification:{identifier}";
        var actualExpiry = expiry ?? TimeSpan.FromMinutes(5);
        return await cacheStrategy.SetAsync(key, data, actualExpiry, cancellationToken);
    }

    /// <summary>
    /// 获取并删除临时验证数据（一次性使用）
    /// </summary>
    public static async Task<T?> GetAndRemoveTempVerificationAsync<T>(
        this ICacheStrategy cacheStrategy,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var key = $"verification:{identifier}";
        var value = await cacheStrategy.GetAsync<T>(key, cancellationToken);
        if (value != null)
        {
            await cacheStrategy.RemoveAsync(key, cancellationToken);
        }
        return value;
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
    /// 预热常用游戏数据
    /// </summary>
    public static async Task<CacheWarmupResult> WarmupGameDataAsync(
        this ICacheStrategy cacheStrategy,
        CancellationToken cancellationToken = default)
    {
        var items = new List<CacheWarmupItem>
        {
            new CacheWarmupItem
            {
                Key = "system:game_config",
                Value = new { Version = "1.0", MaxPlayers = 1000, DefaultRoomSize = 10 },
                Priority = 10,
                Expiry = TimeSpan.FromHours(2)
            },
            new CacheWarmupItem
            {
                Key = "system:rate_limits",
                Value = new { LoginPerMinute = 10, ApiPerSecond = 100 },
                Priority = 9,
                Expiry = TimeSpan.FromHours(1)
            }
        };

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
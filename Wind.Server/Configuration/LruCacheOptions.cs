using System.ComponentModel.DataAnnotations;

namespace Wind.Server.Configuration;

/// <summary>
/// LRU缓存配置选项
/// </summary>
public class LruCacheOptions
{
    /// <summary>
    /// 最大缓存容量
    /// </summary>
    [Range(100, 100000)]
    public int MaxCapacity { get; set; } = 10000;

    /// <summary>
    /// 默认过期时间（分钟）
    /// </summary>
    [Range(1, 1440)]
    public int DefaultExpiryMinutes { get; set; } = 30;

    /// <summary>
    /// 清理间隔（分钟）
    /// </summary>
    [Range(1, 60)]
    public int CleanupIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// 淘汰阈值（0.0-1.0）
    /// 当缓存使用率超过此值时触发淘汰
    /// </summary>
    [Range(0.5, 0.95)]
    public double EvictionThreshold { get; set; } = 0.8;

    /// <summary>
    /// 单次淘汰批次大小
    /// </summary>
    [Range(10, 1000)]
    public int EvictionBatchSize { get; set; } = 100;

    /// <summary>
    /// 是否启用统计信息
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// 缓存键前缀
    /// </summary>
    [Required]
    public string KeyPrefix { get; set; } = "Wind:Cache:";

    /// <summary>
    /// 是否启用自动清理
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;

    /// <summary>
    /// 目标命中率（百分比）
    /// </summary>
    [Range(50.0, 99.0)]
    public double TargetHitRate { get; set; } = 85.0;

    /// <summary>
    /// 验证配置的有效性
    /// </summary>
    public void Validate()
    {
        if (MaxCapacity <= 0)
        {
            throw new ArgumentException("MaxCapacity must be greater than 0");
        }

        if (DefaultExpiryMinutes <= 0)
        {
            throw new ArgumentException("DefaultExpiryMinutes must be greater than 0");
        }

        if (CleanupIntervalMinutes <= 0)
        {
            throw new ArgumentException("CleanupIntervalMinutes must be greater than 0");
        }

        if (EvictionThreshold <= 0.0 || EvictionThreshold >= 1.0)
        {
            throw new ArgumentException("EvictionThreshold must be between 0.0 and 1.0");
        }

        if (EvictionBatchSize <= 0)
        {
            throw new ArgumentException("EvictionBatchSize must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(KeyPrefix))
        {
            throw new ArgumentException("KeyPrefix cannot be null or empty");
        }

        if (TargetHitRate <= 0.0 || TargetHitRate >= 100.0)
        {
            throw new ArgumentException("TargetHitRate must be between 0.0 and 100.0");
        }
    }

    /// <summary>
    /// 获取配置摘要
    /// </summary>
    public string GetConfigurationSummary()
    {
        return $"LruCache[Capacity={MaxCapacity}, Expiry={DefaultExpiryMinutes}min, " +
               $"Threshold={EvictionThreshold:P}, Target={TargetHitRate}%]";
    }
}
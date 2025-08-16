using System.ComponentModel.DataAnnotations;

namespace Wind.Server.Configuration;

/// <summary>
/// 分布式锁配置选项
/// </summary>
public class DistributedLockOptions
{
    /// <summary>
    /// 默认锁过期时间（分钟）
    /// </summary>
    [Range(1, 60)]
    public int DefaultExpiryMinutes { get; set; } = 5;

    /// <summary>
    /// 默认获取锁超时时间（秒）
    /// </summary>
    [Range(1, 300)]
    public int DefaultTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    [Range(10, 5000)]
    public int RetryIntervalMs { get; set; } = 50;

    /// <summary>
    /// 锁键前缀
    /// </summary>
    [Required]
    public string KeyPrefix { get; set; } = "Lock:Wind:";

    /// <summary>
    /// 是否启用自动续约
    /// </summary>
    public bool EnableAutoRenewal { get; set; } = true;

    /// <summary>
    /// 自动续约比例（0.0-1.0）
    /// 当锁剩余时间小于过期时间的此比例时触发续约
    /// </summary>
    [Range(0.1, 0.9)]
    public double AutoRenewalRatio { get; set; } = 0.6;

    /// <summary>
    /// 是否启用统计信息
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    [Range(1, 1000)]
    public int MaxRetries { get; set; } = 100;

    /// <summary>
    /// 验证配置的有效性
    /// </summary>
    public void Validate()
    {
        if (DefaultExpiryMinutes <= 0)
        {
            throw new ArgumentException("DefaultExpiryMinutes must be greater than 0");
        }

        if (DefaultTimeoutSeconds <= 0)
        {
            throw new ArgumentException("DefaultTimeoutSeconds must be greater than 0");
        }

        if (RetryIntervalMs <= 0)
        {
            throw new ArgumentException("RetryIntervalMs must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(KeyPrefix))
        {
            throw new ArgumentException("KeyPrefix cannot be null or empty");
        }

        if (AutoRenewalRatio <= 0.0 || AutoRenewalRatio >= 1.0)
        {
            throw new ArgumentException("AutoRenewalRatio must be between 0.0 and 1.0");
        }

        if (MaxRetries <= 0)
        {
            throw new ArgumentException("MaxRetries must be greater than 0");
        }
    }

    /// <summary>
    /// 获取配置摘要
    /// </summary>
    public string GetConfigurationSummary()
    {
        return $"DistributedLock[Expiry={DefaultExpiryMinutes}min, Timeout={DefaultTimeoutSeconds}s, " +
               $"Retry={RetryIntervalMs}ms, MaxRetries={MaxRetries}, AutoRenewal={EnableAutoRenewal}]";
    }
}
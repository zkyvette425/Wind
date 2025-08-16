using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wind.Server.Services;

/// <summary>
/// 分布式锁接口
/// 提供基于Redis的分布式锁功能，确保并发操作的数据安全
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// 尝试获取分布式锁
    /// </summary>
    /// <param name="lockKey">锁的唯一标识</param>
    /// <param name="expiry">锁的过期时间</param>
    /// <param name="timeout">获取锁的超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果获取成功返回锁令牌，失败返回null</returns>
    Task<ILockToken?> TryAcquireAsync(string lockKey, TimeSpan expiry, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取分布式锁（阻塞直到获取成功或超时）
    /// </summary>
    /// <param name="lockKey">锁的唯一标识</param>
    /// <param name="expiry">锁的过期时间</param>
    /// <param name="timeout">获取锁的超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>锁令牌</returns>
    /// <exception cref="TimeoutException">获取锁超时</exception>
    Task<ILockToken> AcquireAsync(string lockKey, TimeSpan expiry, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// 释放分布式锁
    /// </summary>
    /// <param name="lockToken">锁令牌</param>
    /// <returns>释放是否成功</returns>
    Task<bool> ReleaseAsync(ILockToken lockToken);

    /// <summary>
    /// 续期分布式锁
    /// </summary>
    /// <param name="lockToken">锁令牌</param>
    /// <param name="expiry">新的过期时间</param>
    /// <returns>续期是否成功</returns>
    Task<bool> RenewAsync(ILockToken lockToken, TimeSpan expiry);

    /// <summary>
    /// 检查锁是否仍然有效
    /// </summary>
    /// <param name="lockToken">锁令牌</param>
    /// <returns>锁是否有效</returns>
    Task<bool> IsValidAsync(ILockToken lockToken);
}

/// <summary>
/// 分布式锁令牌接口
/// 表示一个有效的分布式锁实例
/// </summary>
public interface ILockToken : IDisposable
{
    /// <summary>
    /// 锁的唯一标识
    /// </summary>
    string LockKey { get; }

    /// <summary>
    /// 锁的值（用于安全释放）
    /// </summary>
    string LockValue { get; }

    /// <summary>
    /// 锁的创建时间
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// 锁的过期时间
    /// </summary>
    DateTime ExpiresAt { get; }

    /// <summary>
    /// 锁是否已被释放
    /// </summary>
    bool IsReleased { get; }

    /// <summary>
    /// 异步释放锁
    /// </summary>
    Task ReleaseAsync();
}

/// <summary>
/// 分布式锁统计信息
/// </summary>
public class DistributedLockStatistics
{
    /// <summary>
    /// 当前活跃锁数量
    /// </summary>
    public int ActiveLocks { get; set; }

    /// <summary>
    /// 锁获取成功次数
    /// </summary>
    public long SuccessfulAcquisitions { get; set; }

    /// <summary>
    /// 锁获取失败次数
    /// </summary>
    public long FailedAcquisitions { get; set; }

    /// <summary>
    /// 锁获取超时次数
    /// </summary>
    public long TimeoutAcquisitions { get; set; }

    /// <summary>
    /// 平均锁持有时间（毫秒）
    /// </summary>
    public double AverageHoldTimeMs { get; set; }

    /// <summary>
    /// 平均锁等待时间（毫秒）
    /// </summary>
    public double AverageWaitTimeMs { get; set; }

    /// <summary>
    /// 锁成功率
    /// </summary>
    public double SuccessRate => 
        SuccessfulAcquisitions + FailedAcquisitions + TimeoutAcquisitions > 0 
            ? (double)SuccessfulAcquisitions / (SuccessfulAcquisitions + FailedAcquisitions + TimeoutAcquisitions) * 100 
            : 0;

    /// <summary>
    /// 统计时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 分布式锁选项
/// </summary>
public class DistributedLockOptions
{
    /// <summary>
    /// 默认锁过期时间
    /// </summary>
    public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 默认获取锁超时时间
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 锁重试间隔
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 锁键前缀
    /// </summary>
    public string KeyPrefix { get; set; } = "Lock:";

    /// <summary>
    /// 是否启用自动续期
    /// </summary>
    public bool EnableAutoRenewal { get; set; } = true;

    /// <summary>
    /// 自动续期间隔（续期时机为过期时间的百分比）
    /// </summary>
    public double AutoRenewalRatio { get; set; } = 0.7; // 在70%过期时间时续期

    /// <summary>
    /// 是否启用统计
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 100;

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (DefaultExpiry <= TimeSpan.Zero)
            throw new ArgumentException("默认锁过期时间必须大于0", nameof(DefaultExpiry));

        if (DefaultTimeout <= TimeSpan.Zero)
            throw new ArgumentException("默认获取锁超时时间必须大于0", nameof(DefaultTimeout));

        if (RetryInterval <= TimeSpan.Zero)
            throw new ArgumentException("锁重试间隔必须大于0", nameof(RetryInterval));

        if (AutoRenewalRatio <= 0 || AutoRenewalRatio >= 1)
            throw new ArgumentException("自动续期比例必须在0和1之间", nameof(AutoRenewalRatio));

        if (MaxRetries <= 0)
            throw new ArgumentException("最大重试次数必须大于0", nameof(MaxRetries));

        if (string.IsNullOrWhiteSpace(KeyPrefix))
            throw new ArgumentException("锁键前缀不能为空", nameof(KeyPrefix));
    }
}
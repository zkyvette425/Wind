using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wind.Shared.Services;

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
using System;
using System.Threading;
using System.Threading.Tasks;
using Wind.Shared.Services;

namespace Wind.Shared.Extensions;

/// <summary>
/// 分布式锁Grain扩展方法
/// 为Orleans Grain提供便捷的分布式锁使用方式
/// </summary>
public static class DistributedLockGrainExtensions
{
    /// <summary>
    /// 使用分布式锁执行操作（异步，使用using语法）
    /// </summary>
    /// <param name="distributedLock">分布式锁服务</param>
    /// <param name="lockKey">锁键</param>
    /// <param name="operation">要执行的操作</param>
    /// <param name="expiry">锁过期时间</param>
    /// <param name="timeout">获取锁超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task WithLockAsync(
        this IDistributedLock distributedLock,
        string lockKey,
        Func<Task> operation,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var lockToken = await distributedLock.AcquireAsync(
            lockKey,
            expiry ?? TimeSpan.FromMinutes(5),
            timeout ?? TimeSpan.FromSeconds(30),
            cancellationToken);

        await operation();
    }

    /// <summary>
    /// 使用分布式锁执行操作并返回结果（异步，使用using语法）
    /// </summary>
    public static async Task<T> WithLockAsync<T>(
        this IDistributedLock distributedLock,
        string lockKey,
        Func<Task<T>> operation,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var lockToken = await distributedLock.AcquireAsync(
            lockKey,
            expiry ?? TimeSpan.FromMinutes(5),
            timeout ?? TimeSpan.FromSeconds(30),
            cancellationToken);

        return await operation();
    }

    /// <summary>
    /// 尝试使用分布式锁执行操作（不阻塞）
    /// </summary>
    public static async Task<bool> TryWithLockAsync(
        this IDistributedLock distributedLock,
        string lockKey,
        Func<Task> operation,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var lockToken = await distributedLock.TryAcquireAsync(
            lockKey,
            expiry ?? TimeSpan.FromMinutes(5),
            timeout ?? TimeSpan.FromSeconds(30),
            cancellationToken);

        if (lockToken == null)
        {
            return false;
        }

        try
        {
            await operation();
            return true;
        }
        finally
        {
            await lockToken.ReleaseAsync();
        }
    }

    /// <summary>
    /// 尝试使用分布式锁执行操作并返回结果（不阻塞）
    /// </summary>
    public static async Task<(bool Success, T? Result)> TryWithLockAsync<T>(
        this IDistributedLock distributedLock,
        string lockKey,
        Func<Task<T>> operation,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var lockToken = await distributedLock.TryAcquireAsync(
            lockKey,
            expiry ?? TimeSpan.FromMinutes(5),
            timeout ?? TimeSpan.FromSeconds(30),
            cancellationToken);

        if (lockToken == null)
        {
            return (false, default(T));
        }

        try
        {
            var result = await operation();
            return (true, result);
        }
        finally
        {
            await lockToken.ReleaseAsync();
        }
    }

    /// <summary>
    /// 使用玩家锁执行操作
    /// </summary>
    public static Task WithPlayerLockAsync(
        this IDistributedLock distributedLock,
        string playerId,
        Func<Task> operation,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return distributedLock.WithLockAsync(
            $"Player:{playerId}",
            operation,
            expiry,
            timeout,
            cancellationToken);
    }

    /// <summary>
    /// 使用玩家锁执行操作并返回结果
    /// </summary>
    public static Task<T> WithPlayerLockAsync<T>(
        this IDistributedLock distributedLock,
        string playerId,
        Func<Task<T>> operation,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return distributedLock.WithLockAsync(
            $"Player:{playerId}",
            operation,
            expiry,
            timeout,
            cancellationToken);
    }

    /// <summary>
    /// 使用房间锁执行操作
    /// </summary>
    public static Task WithRoomLockAsync(
        this IDistributedLock distributedLock,
        string roomId,
        Func<Task> operation,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return distributedLock.WithLockAsync(
            $"Room:{roomId}",
            operation,
            expiry,
            timeout,
            cancellationToken);
    }

    /// <summary>
    /// 使用房间锁执行操作并返回结果
    /// </summary>
    public static Task<T> WithRoomLockAsync<T>(
        this IDistributedLock distributedLock,
        string roomId,
        Func<Task<T>> operation,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return distributedLock.WithLockAsync(
            $"Room:{roomId}",
            operation,
            expiry,
            timeout,
            cancellationToken);
    }
}
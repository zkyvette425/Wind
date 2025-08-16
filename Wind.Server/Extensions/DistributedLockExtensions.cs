using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Wind.Server.Services;
using Wind.Server.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wind.Server.Extensions;

/// <summary>
/// 分布式锁扩展方法
/// 提供便捷的分布式锁使用方式和依赖注入配置
/// </summary>
public static class DistributedLockExtensions
{
    /// <summary>
    /// 注册分布式锁服务
    /// </summary>
    public static IServiceCollection AddDistributedLock(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置分布式锁选项
        services.Configure<DistributedLockOptions>(configuration.GetSection("DistributedLock"));
        
        // 注册分布式锁服务
        services.AddSingleton<RedisDistributedLockService>();
        
        return services;
    }

    /// <summary>
    /// 注册分布式锁服务（使用委托配置）
    /// </summary>
    public static IServiceCollection AddDistributedLock(this IServiceCollection services, Action<DistributedLockOptions> configure)
    {
        services.Configure<DistributedLockOptions>(options =>
        {
            // 设置默认值
            options.DefaultExpiryMinutes = 5;
            options.DefaultTimeoutSeconds = 30;
            options.RetryIntervalMs = 100;
            options.KeyPrefix = "Wind:Lock:";
            options.EnableAutoRenewal = true;
            options.AutoRenewalRatio = 0.7;
            options.EnableStatistics = true;
            options.MaxRetries = 100;
            
            // 应用自定义配置
            configure?.Invoke(options);
        });
        
        services.AddSingleton<RedisDistributedLockService>();
        
        return services;
    }

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

    /// <summary>
    /// 使用匹配系统锁执行操作
    /// </summary>
    public static Task WithMatchmakingLockAsync(
        this IDistributedLock distributedLock,
        string queueId,
        Func<Task> operation,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return distributedLock.WithLockAsync(
            $"Matchmaking:{queueId}",
            operation,
            expiry,
            timeout,
            cancellationToken);
    }

    /// <summary>
    /// 使用全局操作锁执行操作
    /// </summary>
    public static Task WithGlobalLockAsync(
        this IDistributedLock distributedLock,
        string operationName,
        Func<Task> operation,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return distributedLock.WithLockAsync(
            $"Global:{operationName}",
            operation,
            expiry,
            timeout,
            cancellationToken);
    }

    /// <summary>
    /// 批量获取锁（按顺序获取，避免死锁）
    /// </summary>
    public static async Task<IDisposable> AcquireMultipleLocks(
        this IDistributedLock distributedLock,
        string[] lockKeys,
        TimeSpan? expiry = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        // 按字典序排序，避免死锁
        var sortedKeys = lockKeys.OrderBy(k => k).ToArray();
        var tokens = new List<ILockToken>();

        try
        {
            foreach (var key in sortedKeys)
            {
                var token = await distributedLock.AcquireAsync(
                    key,
                    expiry ?? TimeSpan.FromMinutes(5),
                    timeout ?? TimeSpan.FromSeconds(30),
                    cancellationToken);
                tokens.Add(token);
            }

            return new MultiLockDisposable(tokens);
        }
        catch
        {
            // 如果获取锁失败，释放已获取的锁
            foreach (var token in tokens)
            {
                try
                {
                    await token.ReleaseAsync();
                }
                catch
                {
                    // 忽略释放异常
                }
            }
            throw;
        }
    }

    /// <summary>
    /// 多锁释放包装器
    /// </summary>
    private class MultiLockDisposable : IDisposable
    {
        private readonly List<ILockToken> _tokens;
        private bool _disposed;

        public MultiLockDisposable(List<ILockToken> tokens)
        {
            _tokens = tokens;
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var token in _tokens)
            {
                try
                {
                    token.ReleaseAsync().Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // 忽略释放异常
                }
            }

            _disposed = true;
        }
    }
}
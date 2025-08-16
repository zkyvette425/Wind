using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Wind.Server.Services;

/// <summary>
/// 基于Redis的分布式锁实现
/// 使用Redis SET EX NX命令确保原子性锁操作
/// </summary>
public class RedisDistributedLock : IDistributedLock
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisDistributedLock> _logger;
    private readonly DistributedLockOptions _options;
    private readonly ConcurrentDictionary<string, RedisLockToken> _activeLocks;
    private readonly DistributedLockStatistics _statistics;
    private readonly Timer _renewalTimer;

    public RedisDistributedLock(
        IConnectionMultiplexer redis, 
        IOptions<DistributedLockOptions> options,
        ILogger<RedisDistributedLock> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
        _options = options.Value;
        _options.Validate();
        
        _activeLocks = new ConcurrentDictionary<string, RedisLockToken>();
        _statistics = new DistributedLockStatistics();

        // 启用自动续期定时器
        if (_options.EnableAutoRenewal)
        {
            var renewalInterval = TimeSpan.FromMilliseconds(_options.DefaultExpiry.TotalMilliseconds * _options.AutoRenewalRatio / 2);
            _renewalTimer = new Timer(AutoRenewLocks, null, renewalInterval, renewalInterval);
            _logger.LogDebug("分布式锁自动续期已启用，间隔: {Interval}ms", renewalInterval.TotalMilliseconds);
        }
    }

    /// <summary>
    /// 尝试获取分布式锁
    /// </summary>
    public async Task<ILockToken?> TryAcquireAsync(string lockKey, TimeSpan expiry, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var fullKey = BuildLockKey(lockKey);
        var lockValue = GenerateLockValue();
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogDebug("尝试获取分布式锁: {LockKey}, 过期时间: {Expiry}s", fullKey, expiry.TotalSeconds);

            // 使用SET EX NX命令原子性获取锁
            var acquired = await _database.StringSetAsync(fullKey, lockValue, expiry, When.NotExists);

            if (acquired)
            {
                var lockToken = new RedisLockToken(fullKey, lockValue, expiry, this);
                _activeLocks.TryAdd(fullKey, lockToken);

                var waitTime = DateTime.UtcNow - startTime;
                RecordStatistics(true, waitTime, TimeSpan.Zero);

                _logger.LogDebug("成功获取分布式锁: {LockKey}, 等待时间: {WaitTime}ms", fullKey, waitTime.TotalMilliseconds);
                return lockToken;
            }

            RecordStatistics(false, DateTime.UtcNow - startTime, TimeSpan.Zero);
            _logger.LogDebug("获取分布式锁失败（已被占用）: {LockKey}", fullKey);
            return null;
        }
        catch (Exception ex)
        {
            RecordStatistics(false, DateTime.UtcNow - startTime, TimeSpan.Zero);
            _logger.LogError(ex, "获取分布式锁异常: {LockKey}", fullKey);
            return null;
        }
    }

    /// <summary>
    /// 获取分布式锁（阻塞直到获取成功或超时）
    /// </summary>
    public async Task<ILockToken> AcquireAsync(string lockKey, TimeSpan expiry, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var fullKey = BuildLockKey(lockKey);
        var lockValue = GenerateLockValue();
        var startTime = DateTime.UtcNow;
        var retryCount = 0;

        _logger.LogDebug("等待获取分布式锁: {LockKey}, 超时时间: {Timeout}s", fullKey, timeout.TotalSeconds);

        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 尝试获取锁
                var acquired = await _database.StringSetAsync(fullKey, lockValue, expiry, When.NotExists);

                if (acquired)
                {
                    var lockToken = new RedisLockToken(fullKey, lockValue, expiry, this);
                    _activeLocks.TryAdd(fullKey, lockToken);

                    var waitTime = DateTime.UtcNow - startTime;
                    RecordStatistics(true, waitTime, DateTime.UtcNow.AddMilliseconds(expiry.TotalMilliseconds) - DateTime.UtcNow);

                    _logger.LogDebug("成功获取分布式锁: {LockKey}, 等待时间: {WaitTime}ms, 重试次数: {RetryCount}", 
                        fullKey, waitTime.TotalMilliseconds, retryCount);
                    return lockToken;
                }

                // 等待后重试
                retryCount++;
                if (retryCount > _options.MaxRetries)
                {
                    break;
                }

                await Task.Delay(_options.RetryInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("获取分布式锁被取消: {LockKey}", fullKey);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取分布式锁重试失败: {LockKey}, 重试次数: {RetryCount}", fullKey, retryCount);
                await Task.Delay(_options.RetryInterval, cancellationToken);
            }
        }

        var totalWaitTime = DateTime.UtcNow - startTime;
        RecordTimeoutStatistics(totalWaitTime);

        _logger.LogWarning("获取分布式锁超时: {LockKey}, 总等待时间: {TotalWaitTime}ms, 重试次数: {RetryCount}", 
            fullKey, totalWaitTime.TotalMilliseconds, retryCount);

        throw new TimeoutException($"获取分布式锁超时: {fullKey}，等待时间: {totalWaitTime.TotalSeconds:F2}秒");
    }

    /// <summary>
    /// 释放分布式锁
    /// </summary>
    public async Task<bool> ReleaseAsync(ILockToken lockToken)
    {
        if (lockToken is not RedisLockToken redisToken)
        {
            _logger.LogError("无效的锁令牌类型: {TokenType}", lockToken.GetType().Name);
            return false;
        }

        if (redisToken.IsReleased)
        {
            _logger.LogDebug("锁已被释放: {LockKey}", redisToken.LockKey);
            return true;
        }

        try
        {
            // Lua脚本确保只有锁的拥有者才能释放锁
            const string script = @"
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('DEL', KEYS[1])
                else
                    return 0
                end";

            var result = await _database.ScriptEvaluateAsync(script, new RedisKey[] { redisToken.LockKey }, new RedisValue[] { redisToken.LockValue });

            var released = result.ToString() == "1";
            if (released)
            {
                redisToken.MarkAsReleased();
                _activeLocks.TryRemove(redisToken.LockKey, out _);
                
                _logger.LogDebug("成功释放分布式锁: {LockKey}", redisToken.LockKey);
            }
            else
            {
                _logger.LogWarning("释放分布式锁失败（可能已过期或被其他进程获取）: {LockKey}", redisToken.LockKey);
            }

            return released;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放分布式锁异常: {LockKey}", redisToken.LockKey);
            return false;
        }
    }

    /// <summary>
    /// 续期分布式锁
    /// </summary>
    public async Task<bool> RenewAsync(ILockToken lockToken, TimeSpan expiry)
    {
        if (lockToken is not RedisLockToken redisToken)
        {
            _logger.LogError("无效的锁令牌类型: {TokenType}", lockToken.GetType().Name);
            return false;
        }

        if (redisToken.IsReleased)
        {
            _logger.LogDebug("锁已被释放，无法续期: {LockKey}", redisToken.LockKey);
            return false;
        }

        try
        {
            // Lua脚本确保只有锁的拥有者才能续期
            const string script = @"
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('EXPIRE', KEYS[1], ARGV[2])
                else
                    return 0
                end";

            var result = await _database.ScriptEvaluateAsync(script, 
                new RedisKey[] { redisToken.LockKey }, 
                new RedisValue[] { redisToken.LockValue, (int)expiry.TotalSeconds });

            var renewed = result.ToString() == "1";
            if (renewed)
            {
                redisToken.UpdateExpiry(expiry);
                _logger.LogDebug("成功续期分布式锁: {LockKey}, 新过期时间: {Expiry}s", redisToken.LockKey, expiry.TotalSeconds);
            }
            else
            {
                _logger.LogWarning("续期分布式锁失败（可能已过期或被其他进程获取）: {LockKey}", redisToken.LockKey);
            }

            return renewed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "续期分布式锁异常: {LockKey}", redisToken.LockKey);
            return false;
        }
    }

    /// <summary>
    /// 检查锁是否仍然有效
    /// </summary>
    public async Task<bool> IsValidAsync(ILockToken lockToken)
    {
        if (lockToken is not RedisLockToken redisToken)
        {
            return false;
        }

        if (redisToken.IsReleased)
        {
            return false;
        }

        try
        {
            var value = await _database.StringGetAsync(redisToken.LockKey);
            return value.HasValue && value == redisToken.LockValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查分布式锁有效性异常: {LockKey}", redisToken.LockKey);
            return false;
        }
    }

    /// <summary>
    /// 获取分布式锁统计信息
    /// </summary>
    public DistributedLockStatistics GetStatistics()
    {
        var stats = new DistributedLockStatistics
        {
            ActiveLocks = _activeLocks.Count,
            SuccessfulAcquisitions = _statistics.SuccessfulAcquisitions,
            FailedAcquisitions = _statistics.FailedAcquisitions,
            TimeoutAcquisitions = _statistics.TimeoutAcquisitions,
            AverageHoldTimeMs = _statistics.AverageHoldTimeMs,
            AverageWaitTimeMs = _statistics.AverageWaitTimeMs,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogDebug("分布式锁统计: 活跃锁={ActiveLocks}, 成功率={SuccessRate:F2}%, 平均等待={AvgWait:F2}ms",
            stats.ActiveLocks, stats.SuccessRate, stats.AverageWaitTimeMs);

        return stats;
    }

    #region 私有方法

    private string BuildLockKey(string lockKey)
    {
        return $"{_options.KeyPrefix}{lockKey}";
    }

    private string GenerateLockValue()
    {
        return $"{Environment.MachineName}:{Environment.ProcessId}:{Thread.CurrentThread.ManagedThreadId}:{Guid.NewGuid():N}";
    }

    private void RecordStatistics(bool success, TimeSpan waitTime, TimeSpan holdTime)
    {
        if (!_options.EnableStatistics) return;

        if (success)
        {
            _statistics.SuccessfulAcquisitions++;
        }
        else
        {
            _statistics.FailedAcquisitions++;
        }

        // 更新平均等待时间（简化计算）
        _statistics.AverageWaitTimeMs = (_statistics.AverageWaitTimeMs + waitTime.TotalMilliseconds) / 2;
        
        if (holdTime > TimeSpan.Zero)
        {
            _statistics.AverageHoldTimeMs = (_statistics.AverageHoldTimeMs + holdTime.TotalMilliseconds) / 2;
        }
    }

    private void RecordTimeoutStatistics(TimeSpan waitTime)
    {
        if (!_options.EnableStatistics) return;

        _statistics.TimeoutAcquisitions++;
        _statistics.AverageWaitTimeMs = (_statistics.AverageWaitTimeMs + waitTime.TotalMilliseconds) / 2;
    }

    private async void AutoRenewLocks(object? state)
    {
        if (!_options.EnableAutoRenewal) return;

        var locksToRenew = new List<RedisLockToken>();

        // 找出需要续期的锁
        foreach (var lockPair in _activeLocks)
        {
            var lockToken = lockPair.Value;
            if (lockToken.IsReleased) continue;

            var remainingTime = lockToken.ExpiresAt - DateTime.UtcNow;
            var totalTime = lockToken.ExpiresAt - lockToken.CreatedAt;
            var ratio = remainingTime.TotalMilliseconds / totalTime.TotalMilliseconds;

            if (ratio < _options.AutoRenewalRatio)
            {
                locksToRenew.Add(lockToken);
            }
        }

        // 续期锁
        foreach (var lockToken in locksToRenew)
        {
            try
            {
                await RenewAsync(lockToken, _options.DefaultExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "自动续期锁失败: {LockKey}", lockToken.LockKey);
                
                // 移除无法续期的锁
                _activeLocks.TryRemove(lockToken.LockKey, out _);
                lockToken.MarkAsReleased();
            }
        }
    }

    public void Dispose()
    {
        _renewalTimer?.Dispose();
        
        // 释放所有活跃锁
        foreach (var lockPair in _activeLocks)
        {
            try
            {
                ReleaseAsync(lockPair.Value).Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放锁失败: {LockKey}", lockPair.Key);
            }
        }
        
        _activeLocks.Clear();
    }

    #endregion
}

/// <summary>
/// Redis分布式锁令牌实现
/// </summary>
internal class RedisLockToken : ILockToken
{
    private readonly RedisDistributedLock _distributedLock;
    private volatile bool _isReleased;

    public RedisLockToken(string lockKey, string lockValue, TimeSpan expiry, RedisDistributedLock distributedLock)
    {
        LockKey = lockKey;
        LockValue = lockValue;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt.Add(expiry);
        _distributedLock = distributedLock;
        _isReleased = false;
    }

    public string LockKey { get; }
    public string LockValue { get; }
    public DateTime CreatedAt { get; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsReleased => _isReleased;

    public async Task ReleaseAsync()
    {
        if (!_isReleased)
        {
            await _distributedLock.ReleaseAsync(this);
        }
    }

    public void UpdateExpiry(TimeSpan newExpiry)
    {
        ExpiresAt = DateTime.UtcNow.Add(newExpiry);
    }

    public void MarkAsReleased()
    {
        _isReleased = true;
    }

    public void Dispose()
    {
        if (!_isReleased)
        {
            // 同步释放锁
            try
            {
                ReleaseAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // 忽略释放异常
            }
        }
    }
}
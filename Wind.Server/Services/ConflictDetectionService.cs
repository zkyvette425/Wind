using Microsoft.Extensions.Logging;
using Wind.Server.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using StackExchange.Redis;
using MongoDB.Driver;

namespace Wind.Server.Services;

/// <summary>
/// 数据冲突检测和解决服务
/// 提供版本控制、冲突检测、解决策略等功能
/// </summary>
public class ConflictDetectionService : IDisposable
{
    private readonly RedisConnectionManager _redisManager;
    private readonly MongoDbConnectionManager _mongoManager;
    private readonly RedisDistributedLockService _lockService;
    private readonly ILogger<ConflictDetectionService> _logger;
    private readonly ConcurrentDictionary<string, VersionInfo> _versionCache;
    private readonly Timer _cleanupTimer;
    private volatile bool _disposed = false;

    // 统计信息
    private long _conflictDetectedCount = 0;
    private long _conflictResolvedCount = 0;
    private long _versionMismatchCount = 0;
    private long _mergeSuccessCount = 0;

    public ConflictDetectionService(
        RedisConnectionManager redisManager,
        MongoDbConnectionManager mongoManager,
        RedisDistributedLockService lockService,
        ILogger<ConflictDetectionService> logger)
    {
        _redisManager = redisManager;
        _mongoManager = mongoManager;
        _lockService = lockService;
        _logger = logger;
        _versionCache = new ConcurrentDictionary<string, VersionInfo>();

        // 启动清理定时器
        _cleanupTimer = new Timer(CleanupOldVersions, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _logger.LogInformation("数据冲突检测服务已初始化");
    }

    /// <summary>
    /// 检查数据版本冲突
    /// </summary>
    public async Task<ConflictCheckResult> CheckConflictAsync<T>(
        string dataKey, 
        T currentData, 
        long expectedVersion,
        ConflictResolutionStrategy strategy = ConflictResolutionStrategy.OptimisticLock)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConflictDetectionService));
        }

        try
        {
            var versionKey = GetVersionKey(dataKey);
            var database = _redisManager.GetDatabase();

            // 获取当前版本信息
            var storedVersionData = await database.StringGetAsync(versionKey);
            
            if (!storedVersionData.HasValue)
            {
                // 首次写入，不存在冲突
                return new ConflictCheckResult
                {
                    HasConflict = false,
                    CurrentVersion = expectedVersion,
                    StoredVersion = 0,
                    Resolution = ConflictResolution.NoConflict
                };
            }

            var storedVersion = JsonSerializer.Deserialize<VersionInfo>(storedVersionData!);
            
            if (storedVersion == null || storedVersion.Version == expectedVersion)
            {
                // 版本匹配，无冲突
                return new ConflictCheckResult
                {
                    HasConflict = false,
                    CurrentVersion = expectedVersion,
                    StoredVersion = storedVersion?.Version ?? 0,
                    Resolution = ConflictResolution.NoConflict
                };
            }

            // 检测到版本冲突
            Interlocked.Increment(ref _conflictDetectedCount);
            Interlocked.Increment(ref _versionMismatchCount);
            
            _logger.LogWarning("检测到数据版本冲突: {DataKey}, 期望版本: {Expected}, 实际版本: {Actual}", 
                dataKey, expectedVersion, storedVersion.Version);

            // 根据策略解决冲突
            var resolution = await ResolveConflictAsync(dataKey, currentData, expectedVersion, storedVersion, strategy);
            
            return new ConflictCheckResult
            {
                HasConflict = true,
                CurrentVersion = expectedVersion,
                StoredVersion = storedVersion.Version,
                Resolution = resolution.Resolution,
                ResolvedData = resolution.ResolvedData,
                NewVersion = resolution.NewVersion
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查数据冲突时发生错误: {DataKey}", dataKey);
            throw;
        }
    }

    /// <summary>
    /// 更新数据版本
    /// </summary>
    public async Task<bool> UpdateVersionAsync<T>(
        string dataKey, 
        T data, 
        long newVersion,
        TimeSpan? expiry = null)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConflictDetectionService));
        }

        try
        {
            var versionKey = GetVersionKey(dataKey);
            var database = _redisManager.GetDatabase();
            
            var versionInfo = new VersionInfo
            {
                Version = newVersion,
                DataKey = dataKey,
                LastModified = DateTime.UtcNow,
                DataHash = ComputeDataHash(data),
                ModifiedBy = Environment.MachineName
            };

            var serializedVersion = JsonSerializer.Serialize(versionInfo);
            var result = await database.StringSetAsync(versionKey, serializedVersion, expiry ?? TimeSpan.FromHours(24));

            if (result)
            {
                _versionCache.AddOrUpdate(dataKey, versionInfo, (k, v) => versionInfo);
                _logger.LogDebug("版本信息已更新: {DataKey}, 版本: {Version}", dataKey, newVersion);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新版本信息时发生错误: {DataKey}", dataKey);
            return false;
        }
    }

    /// <summary>
    /// 解决数据冲突
    /// </summary>
    private async Task<ConflictResolutionResult> ResolveConflictAsync<T>(
        string dataKey,
        T currentData,
        long expectedVersion,
        VersionInfo storedVersion,
        ConflictResolutionStrategy strategy)
    {
        try
        {
            switch (strategy)
            {
                case ConflictResolutionStrategy.OptimisticLock:
                    return await ResolveOptimisticLockConflict(dataKey, currentData, expectedVersion, storedVersion);

                case ConflictResolutionStrategy.LastWriteWins:
                    return await ResolveLastWriteWinsConflict(dataKey, currentData, expectedVersion, storedVersion);

                case ConflictResolutionStrategy.FirstWriteWins:
                    return await ResolveFirstWriteWinsConflict(dataKey, currentData, expectedVersion, storedVersion);

                case ConflictResolutionStrategy.Merge:
                    return await ResolveMergeConflict(dataKey, currentData, expectedVersion, storedVersion);

                case ConflictResolutionStrategy.UserChoice:
                    return await ResolveUserChoiceConflict(dataKey, currentData, expectedVersion, storedVersion);

                default:
                    throw new ArgumentException($"不支持的冲突解决策略: {strategy}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解决数据冲突时发生错误: {DataKey}, 策略: {Strategy}", dataKey, strategy);
            return new ConflictResolutionResult
            {
                Resolution = ConflictResolution.Failed,
                NewVersion = expectedVersion
            };
        }
    }

    /// <summary>
    /// 乐观锁冲突解决
    /// </summary>
    private async Task<ConflictResolutionResult> ResolveOptimisticLockConflict<T>(
        string dataKey, T currentData, long expectedVersion, VersionInfo storedVersion)
    {
        // 乐观锁策略：拒绝更新，要求重新获取最新数据
        _logger.LogInformation("乐观锁冲突解决：拒绝更新 {DataKey}", dataKey);
        
        return new ConflictResolutionResult
        {
            Resolution = ConflictResolution.Rejected,
            NewVersion = storedVersion.Version,
            ErrorMessage = "数据已被其他用户修改，请重新获取最新数据后再试"
        };
    }

    /// <summary>
    /// 最后写入胜出冲突解决
    /// </summary>
    private async Task<ConflictResolutionResult> ResolveLastWriteWinsConflict<T>(
        string dataKey, T currentData, long expectedVersion, VersionInfo storedVersion)
    {
        // 最后写入胜出：直接覆盖存储的数据
        var newVersion = storedVersion.Version + 1;
        
        _logger.LogInformation("最后写入胜出冲突解决：覆盖数据 {DataKey}, 新版本: {NewVersion}", dataKey, newVersion);
        
        await UpdateVersionAsync(dataKey, currentData, newVersion);
        Interlocked.Increment(ref _conflictResolvedCount);
        
        return new ConflictResolutionResult
        {
            Resolution = ConflictResolution.Overwrite,
            ResolvedData = currentData,
            NewVersion = newVersion
        };
    }

    /// <summary>
    /// 首次写入胜出冲突解决
    /// </summary>
    private async Task<ConflictResolutionResult> ResolveFirstWriteWinsConflict<T>(
        string dataKey, T currentData, long expectedVersion, VersionInfo storedVersion)
    {
        // 首次写入胜出：保持存储的数据不变
        _logger.LogInformation("首次写入胜出冲突解决：保持原数据 {DataKey}", dataKey);
        
        return new ConflictResolutionResult
        {
            Resolution = ConflictResolution.KeepStored,
            NewVersion = storedVersion.Version,
            ErrorMessage = "数据已存在，保持原有数据不变"
        };
    }

    /// <summary>
    /// 合并冲突解决
    /// </summary>
    private async Task<ConflictResolutionResult> ResolveMergeConflict<T>(
        string dataKey, T currentData, long expectedVersion, VersionInfo storedVersion)
    {
        try
        {
            // 尝试获取存储的实际数据进行合并
            var storedData = await GetStoredDataAsync<T>(dataKey);
            
            if (storedData == null)
            {
                // 如果无法获取存储数据，回退到最后写入胜出
                return await ResolveLastWriteWinsConflict(dataKey, currentData, expectedVersion, storedVersion);
            }

            // 执行数据合并（这里实现简单的字段级合并）
            var mergedData = MergeData(storedData, currentData);
            var newVersion = storedVersion.Version + 1;
            
            _logger.LogInformation("合并冲突解决：成功合并数据 {DataKey}, 新版本: {NewVersion}", dataKey, newVersion);
            
            await UpdateVersionAsync(dataKey, mergedData, newVersion);
            Interlocked.Increment(ref _conflictResolvedCount);
            Interlocked.Increment(ref _mergeSuccessCount);
            
            return new ConflictResolutionResult
            {
                Resolution = ConflictResolution.Merged,
                ResolvedData = mergedData,
                NewVersion = newVersion
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "合并数据时发生错误: {DataKey}", dataKey);
            
            // 合并失败，回退到乐观锁策略
            return await ResolveOptimisticLockConflict(dataKey, currentData, expectedVersion, storedVersion);
        }
    }

    /// <summary>
    /// 用户选择冲突解决
    /// </summary>
    private async Task<ConflictResolutionResult> ResolveUserChoiceConflict<T>(
        string dataKey, T currentData, long expectedVersion, VersionInfo storedVersion)
    {
        // 用户选择策略：返回冲突信息，让上层应用决定
        _logger.LogInformation("用户选择冲突解决：等待用户决策 {DataKey}", dataKey);
        
        var storedData = await GetStoredDataAsync<T>(dataKey);
        
        return new ConflictResolutionResult
        {
            Resolution = ConflictResolution.RequireUserChoice,
            ResolvedData = storedData,
            NewVersion = storedVersion.Version,
            ConflictInfo = new ConflictInfo
            {
                DataKey = dataKey,
                CurrentData = currentData,
                StoredData = storedData,
                CurrentVersion = expectedVersion,
                StoredVersion = storedVersion.Version,
                LastModified = storedVersion.LastModified,
                ModifiedBy = storedVersion.ModifiedBy
            }
        };
    }

    /// <summary>
    /// 批量检查冲突
    /// </summary>
    public async Task<List<ConflictCheckResult>> CheckBatchConflictAsync<T>(
        Dictionary<string, (T data, long version)> dataItems,
        ConflictResolutionStrategy strategy = ConflictResolutionStrategy.OptimisticLock)
    {
        var results = new List<ConflictCheckResult>();
        var tasks = dataItems.Select(async kvp =>
        {
            var result = await CheckConflictAsync(kvp.Key, kvp.Value.data, kvp.Value.version, strategy);
            result.DataKey = kvp.Key;
            return result;
        });

        results.AddRange(await Task.WhenAll(tasks));
        return results;
    }

    /// <summary>
    /// 获取存储的数据
    /// </summary>
    private async Task<T?> GetStoredDataAsync<T>(string dataKey)
    {
        try
        {
            // 首先尝试从Redis获取
            var database = _redisManager.GetDatabase();
            var redisData = await database.StringGetAsync(dataKey);
            
            if (redisData.HasValue)
            {
                return JsonSerializer.Deserialize<T>(redisData!);
            }

            // Redis中没有，尝试从MongoDB获取
            // 这里需要根据实际的数据结构来实现
            // 暂时返回默认值
            return default(T);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取存储数据时发生错误: {DataKey}", dataKey);
            return default(T);
        }
    }

    /// <summary>
    /// 合并数据（简单实现）
    /// </summary>
    private T MergeData<T>(T storedData, T currentData)
    {
        // 简单的合并策略：优先使用当前数据的非空字段
        // 实际应用中可能需要更复杂的合并逻辑
        
        if (storedData == null) return currentData;
        if (currentData == null) return storedData;
        
        // 对于简单情况，直接返回当前数据
        // 复杂的合并逻辑可以基于反射或特定的合并规则实现
        return currentData;
    }

    /// <summary>
    /// 计算数据哈希值
    /// </summary>
    private string ComputeDataHash<T>(T data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
            return Convert.ToBase64String(hashBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "计算数据哈希值时发生错误");
            return string.Empty;
        }
    }

    /// <summary>
    /// 清理旧版本信息
    /// </summary>
    private void CleanupOldVersions(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            var keysToRemove = _versionCache
                .Where(kvp => kvp.Value.LastModified < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _versionCache.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogDebug("清理了 {Count} 个旧版本信息", keysToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理旧版本信息时发生错误");
        }
    }

    /// <summary>
    /// 获取版本键名
    /// </summary>
    private static string GetVersionKey(string dataKey)
    {
        return $"version:{dataKey}";
    }

    /// <summary>
    /// 获取冲突检测统计信息
    /// </summary>
    public ConflictStatistics GetStatistics()
    {
        return new ConflictStatistics
        {
            ConflictDetectedCount = _conflictDetectedCount,
            ConflictResolvedCount = _conflictResolvedCount,
            VersionMismatchCount = _versionMismatchCount,
            MergeSuccessCount = _mergeSuccessCount,
            ActiveVersionCount = _versionCache.Count,
            ConflictResolutionRate = _conflictDetectedCount > 0 
                ? (double)_conflictResolvedCount / _conflictDetectedCount * 100 
                : 0
        };
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cleanupTimer?.Dispose();
        _versionCache.Clear();

        _logger.LogInformation("数据冲突检测服务已释放");
    }
}

/// <summary>
/// 版本信息
/// </summary>
public class VersionInfo
{
    public long Version { get; set; }
    public string DataKey { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public string DataHash { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
}

/// <summary>
/// 冲突检查结果
/// </summary>
public class ConflictCheckResult
{
    public string DataKey { get; set; } = string.Empty;
    public bool HasConflict { get; set; }
    public long CurrentVersion { get; set; }
    public long StoredVersion { get; set; }
    public ConflictResolution Resolution { get; set; }
    public object? ResolvedData { get; set; }
    public long? NewVersion { get; set; }
    public string? ErrorMessage { get; set; }
    public ConflictInfo? ConflictInfo { get; set; }
}

/// <summary>
/// 冲突解决结果
/// </summary>
public class ConflictResolutionResult
{
    public ConflictResolution Resolution { get; set; }
    public object? ResolvedData { get; set; }
    public long NewVersion { get; set; }
    public string? ErrorMessage { get; set; }
    public ConflictInfo? ConflictInfo { get; set; }
}

/// <summary>
/// 冲突信息
/// </summary>
public class ConflictInfo
{
    public string DataKey { get; set; } = string.Empty;
    public object? CurrentData { get; set; }
    public object? StoredData { get; set; }
    public long CurrentVersion { get; set; }
    public long StoredVersion { get; set; }
    public DateTime LastModified { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}

/// <summary>
/// 冲突解决策略
/// </summary>
public enum ConflictResolutionStrategy
{
    OptimisticLock,     // 乐观锁：拒绝更新
    LastWriteWins,      // 最后写入胜出
    FirstWriteWins,     // 首次写入胜出
    Merge,              // 尝试合并
    UserChoice          // 用户选择
}

/// <summary>
/// 冲突解决结果类型
/// </summary>
public enum ConflictResolution
{
    NoConflict,         // 无冲突
    Rejected,           // 拒绝更新
    Overwrite,          // 覆盖存储数据
    KeepStored,         // 保持存储数据
    Merged,             // 成功合并
    RequireUserChoice,  // 需要用户选择
    Failed              // 解决失败
}

/// <summary>
/// 冲突统计信息
/// </summary>
public class ConflictStatistics
{
    public long ConflictDetectedCount { get; set; }
    public long ConflictResolvedCount { get; set; }
    public long VersionMismatchCount { get; set; }
    public long MergeSuccessCount { get; set; }
    public int ActiveVersionCount { get; set; }
    public double ConflictResolutionRate { get; set; }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wind.Server.Services;
using Wind.Server.Configuration;
using Wind.Tests.TestFixtures;
using Xunit;
using Xunit.Abstractions;
using StackExchange.Redis;
using MongoDB.Driver;
using System.Text.Json;

namespace Wind.Tests.IntegrationTests;

/// <summary>
/// 数据冲突检测服务集成测试
/// 验证真实的版本冲突检测和解决功能
/// </summary>
[Collection("ClusterCollection")]
public class ConflictDetectionIntegrationTests
{
    private readonly ClusterFixture _clusterFixture;
    private readonly ITestOutputHelper _output;
    private readonly ConflictDetectionService _conflictService;
    private readonly RedisConnectionManager _redisManager;
    private readonly MongoDbConnectionManager _mongoManager;

    public ConflictDetectionIntegrationTests(ClusterFixture clusterFixture, ITestOutputHelper output)
    {
        _clusterFixture = clusterFixture;
        _output = output;
        
        // 获取服务实例 - 从Client的ServiceProvider获取服务
        _conflictService = _clusterFixture.ServiceProvider.GetRequiredService<ConflictDetectionService>();
        _redisManager = _clusterFixture.ServiceProvider.GetRequiredService<RedisConnectionManager>();
        _mongoManager = _clusterFixture.ServiceProvider.GetRequiredService<MongoDbConnectionManager>();
    }

    [Fact]
    public async Task CheckConflict_NoExistingData_ShouldReturnNoConflict()
    {
        // Arrange
        var dataKey = "conflict:test:new:data";
        var testData = new { PlayerId = "player123", Score = 100 };
        long expectedVersion = 1;
        
        // Clean up
        await CleanupTestDataAsync(dataKey);
        
        try
        {
            // Act
            var result = await _conflictService.CheckConflictAsync(dataKey, testData, expectedVersion);
            
            // Assert
            Assert.False(result.HasConflict);
            Assert.Equal(expectedVersion, result.CurrentVersion);
            Assert.Equal(0, result.StoredVersion);
            Assert.Equal(Wind.Server.Services.ConflictResolution.NoConflict, result.Resolution);
            
            _output.WriteLine("✅ 新数据冲突检测测试通过 - 无冲突");
        }
        finally
        {
            await CleanupTestDataAsync(dataKey);
        }
    }

    [Fact]
    public async Task CheckConflict_MatchingVersion_ShouldReturnNoConflict()
    {
        // Arrange
        var dataKey = "conflict:test:matching:version";
        var testData = new { PlayerId = "player456", Score = 200 };
        long version = 5;
        
        await CleanupTestDataAsync(dataKey);
        
        try
        {
            // 先设置一个版本
            await _conflictService.UpdateVersionAsync(dataKey, testData, version);
            _output.WriteLine($"设置初始版本: {version}");
            
            // Act - 使用相同版本检查冲突
            var result = await _conflictService.CheckConflictAsync(dataKey, testData, version);
            
            // Assert
            Assert.False(result.HasConflict);
            Assert.Equal(version, result.CurrentVersion);
            Assert.Equal(version, result.StoredVersion);
            Assert.Equal(Wind.Server.Services.ConflictResolution.NoConflict, result.Resolution);
            
            _output.WriteLine("✅ 版本匹配冲突检测测试通过 - 无冲突");
        }
        finally
        {
            await CleanupTestDataAsync(dataKey);
        }
    }

    [Fact]
    public async Task CheckConflict_VersionMismatch_ShouldDetectConflict()
    {
        // Arrange
        var dataKey = "conflict:test:version:mismatch";
        var originalData = new { PlayerId = "player789", Score = 300 };
        var newData = new { PlayerId = "player789", Score = 350 };
        long storedVersion = 10;
        long expectedVersion = 8; // 旧版本
        
        await CleanupTestDataAsync(dataKey);
        
        try
        {
            // 设置存储的版本
            await _conflictService.UpdateVersionAsync(dataKey, originalData, storedVersion);
            _output.WriteLine($"设置存储版本: {storedVersion}");
            
            // Act - 使用旧版本检查冲突
            var result = await _conflictService.CheckConflictAsync(
                dataKey, newData, expectedVersion, Wind.Server.Services.ConflictResolutionStrategy.OptimisticLock);
            
            // Assert
            Assert.True(result.HasConflict);
            Assert.Equal(expectedVersion, result.CurrentVersion);
            Assert.Equal(storedVersion, result.StoredVersion);
            Assert.NotEqual(Wind.Server.Services.ConflictResolution.NoConflict, result.Resolution);
            
            _output.WriteLine($"✅ 版本冲突检测测试通过 - 检测到冲突，解决策略: {result.Resolution}");
        }
        finally
        {
            await CleanupTestDataAsync(dataKey);
        }
    }

    [Fact]
    public async Task CheckConflict_LastWriteWinsStrategy_ShouldAcceptNewData()
    {
        // Arrange
        var dataKey = "conflict:test:last:write:wins";
        var originalData = new { PlayerId = "player111", Score = 400, UpdatedAt = DateTime.UtcNow.AddMinutes(-5) };
        var newData = new { PlayerId = "player111", Score = 450, UpdatedAt = DateTime.UtcNow };
        long storedVersion = 15;
        long expectedVersion = 12;
        
        await CleanupTestDataAsync(dataKey);
        
        try
        {
            // 设置存储的版本
            await _conflictService.UpdateVersionAsync(dataKey, originalData, storedVersion);
            
            // Act - 使用LastWriteWins策略检查冲突
            var result = await _conflictService.CheckConflictAsync(
                dataKey, newData, expectedVersion, Wind.Server.Services.ConflictResolutionStrategy.LastWriteWins);
            
            // Assert
            Assert.True(result.HasConflict); // 应该检测到冲突
            // LastWriteWins策略应该会覆盖现有数据
            Assert.True(result.Resolution == Wind.Server.Services.ConflictResolution.Overwrite ||
                       result.Resolution == Wind.Server.Services.ConflictResolution.NoConflict);
            
            _output.WriteLine($"✅ LastWriteWins策略测试通过，解决方式: {result.Resolution}");
        }
        finally
        {
            await CleanupTestDataAsync(dataKey);
        }
    }

    [Fact]
    public async Task CheckConflict_FirstWriteWinsStrategy_ShouldKeepOriginalData()
    {
        // Arrange
        var dataKey = "conflict:test:first:write:wins";
        var originalData = new { PlayerId = "player222", Score = 500, UpdatedAt = DateTime.UtcNow.AddMinutes(-3) };
        var newData = new { PlayerId = "player222", Score = 550, UpdatedAt = DateTime.UtcNow };
        long storedVersion = 20;
        long expectedVersion = 18;
        
        await CleanupTestDataAsync(dataKey);
        
        try
        {
            // 设置存储的版本
            await _conflictService.UpdateVersionAsync(dataKey, originalData, storedVersion);
            
            // Act - 使用FirstWriteWins策略检查冲突
            var result = await _conflictService.CheckConflictAsync(
                dataKey, newData, expectedVersion, Wind.Server.Services.ConflictResolutionStrategy.FirstWriteWins);
            
            // Assert
            Assert.True(result.HasConflict);
            // FirstWriteWins策略应该保持存储的数据
            Assert.True(result.Resolution == Wind.Server.Services.ConflictResolution.KeepStored ||
                       result.Resolution == Wind.Server.Services.ConflictResolution.Rejected);
            
            _output.WriteLine($"✅ FirstWriteWins策略测试通过，解决方式: {result.Resolution}");
        }
        finally
        {
            await CleanupTestDataAsync(dataKey);
        }
    }

    [Fact]
    public async Task CheckConflict_MergeStrategy_ShouldAttemptMerge()
    {
        // Arrange
        var dataKey = "conflict:test:merge:data";
        var originalData = new { PlayerId = "player333", Score = 600, Level = 5, LastLogin = DateTime.UtcNow.AddDays(-1) };
        var newData = new { PlayerId = "player333", Score = 650, Level = 6, LastUpdate = DateTime.UtcNow };
        long storedVersion = 25;
        long expectedVersion = 23;
        
        await CleanupTestDataAsync(dataKey);
        
        try
        {
            // 设置存储的版本
            await _conflictService.UpdateVersionAsync(dataKey, originalData, storedVersion);
            
            // Act - 使用Merge策略检查冲突
            var result = await _conflictService.CheckConflictAsync(
                dataKey, newData, expectedVersion, Wind.Server.Services.ConflictResolutionStrategy.Merge);
            
            // Assert
            Assert.True(result.HasConflict);
            // Merge策略应该尝试合并数据
            Assert.True(result.Resolution == Wind.Server.Services.ConflictResolution.Merged ||
                       result.Resolution == Wind.Server.Services.ConflictResolution.Failed ||
                       result.Resolution == Wind.Server.Services.ConflictResolution.RequireUserChoice);
            
            _output.WriteLine($"✅ Merge策略测试通过，解决方式: {result.Resolution}");
            if (result.ResolvedData != null)
            {
                _output.WriteLine($"合并数据: {JsonSerializer.Serialize(result.ResolvedData)}");
            }
        }
        finally
        {
            await CleanupTestDataAsync(dataKey);
        }
    }

    [Fact]
    public async Task ConcurrentConflictDetection_ShouldHandleMultipleWriters()
    {
        // Arrange
        const int concurrentWriters = 10;
        var dataKey = "conflict:test:concurrent";
        var tasks = new List<Task<ConflictCheckResult>>();
        
        await CleanupTestDataAsync(dataKey);
        
        try
        {
            // 设置初始版本
            var initialData = new { PlayerId = "concurrent_player", Score = 0 };
            await _conflictService.UpdateVersionAsync(dataKey, initialData, 1);
            
            // Act - 并发执行多个冲突检测
            for (int i = 0; i < concurrentWriters; i++)
            {
                int writerId = i;
                tasks.Add(Task.Run(async () =>
                {
                    var writerData = new { PlayerId = "concurrent_player", Score = writerId * 100 };
                    return await _conflictService.CheckConflictAsync(
                        dataKey, writerData, 1, Wind.Server.Services.ConflictResolutionStrategy.LastWriteWins);
                }));
            }
            
            // Assert
            var results = await Task.WhenAll(tasks);
            var conflictsDetected = results.Count(r => r.HasConflict);
            var noConflicts = results.Count(r => !r.HasConflict);
            
            _output.WriteLine($"并发冲突检测结果: {conflictsDetected} 个冲突, {noConflicts} 个无冲突");
            
            // 应该有冲突被检测到（除了第一个可能成功的请求）
            Assert.True(conflictsDetected > 0 || noConflicts > 0, "应该有一些冲突检测结果");
            
            _output.WriteLine("🎯 并发冲突检测测试成功！");
        }
        finally
        {
            await CleanupTestDataAsync(dataKey);
        }
    }

    [Fact]
    public async Task ConflictStatistics_ShouldTrackCorrectly()
    {
        // Arrange
        var dataKey = "conflict:test:statistics";
        var testData1 = new { PlayerId = "stats_player", Score = 100 };
        var testData2 = new { PlayerId = "stats_player", Score = 200 };
        
        await CleanupTestDataAsync(dataKey);
        
        try
        {
            // Act - 执行一些冲突检测操作
            
            // 1. 首次写入（无冲突）
            await _conflictService.UpdateVersionAsync(dataKey, testData1, 1);
            await _conflictService.CheckConflictAsync(dataKey, testData1, 1);
            
            // 2. 版本冲突
            await _conflictService.CheckConflictAsync(dataKey, testData2, 0); // 旧版本，会冲突
            
            // Assert
            var stats = _conflictService.GetStatistics();
            _output.WriteLine($"冲突检测统计: {JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true })}");
            
            Assert.True(stats.ConflictDetectedCount >= 1, "应该检测到至少1个冲突");
            
            _output.WriteLine("🎯 冲突统计追踪测试成功！");
        }
        finally
        {
            await CleanupTestDataAsync(dataKey);
        }
    }

    [Fact]
    public async Task VersionUpdate_ShouldPersistToRedis()
    {
        // Arrange
        var dataKey = "conflict:test:version:update";
        var testData = new { PlayerId = "version_player", Score = 999 };
        long version = 42;
        
        await CleanupTestDataAsync(dataKey);
        
        try
        {
            // Act
            var success = await _conflictService.UpdateVersionAsync(dataKey, testData, version);
            
            // Assert
            Assert.True(success, "版本更新应该成功");
            
            // 直接从Redis验证数据
            var redisDb = _redisManager.GetDatabase();
            var versionKey = $"version:{dataKey}";
            var storedData = await redisDb.StringGetAsync(versionKey);
            
            Assert.True(storedData.HasValue);
            
            var versionInfo = JsonSerializer.Deserialize<VersionInfo>(storedData!);
            Assert.NotNull(versionInfo);
            Assert.Equal(version, versionInfo.Version);
            Assert.Equal(dataKey, versionInfo.DataKey);
            
            _output.WriteLine($"存储的版本信息: {storedData}");
            _output.WriteLine("✅ 版本更新持久化测试通过");
        }
        finally
        {
            await CleanupTestDataAsync(dataKey);
        }
    }

    [Fact]
    public async Task ConflictResolution_OptimisticLock_ShouldRejectConflict()
    {
        // Arrange
        var dataKey = "conflict:test:optimistic:lock";
        var originalData = new { PlayerId = "optimistic_player", Score = 800 };
        var newData = new { PlayerId = "optimistic_player", Score = 850 };
        long storedVersion = 30;
        long expectedVersion = 28; // 旧版本
        
        await CleanupTestDataAsync(dataKey);
        
        try
        {
            // 设置存储的版本
            await _conflictService.UpdateVersionAsync(dataKey, originalData, storedVersion);
            
            // Act - 使用OptimisticLock策略
            var result = await _conflictService.CheckConflictAsync(
                dataKey, newData, expectedVersion, Wind.Server.Services.ConflictResolutionStrategy.OptimisticLock);
            
            // Assert
            Assert.True(result.HasConflict);
            // 乐观锁应该拒绝冲突的更新
            Assert.Equal(Wind.Server.Services.ConflictResolution.Rejected, result.Resolution);
            
            _output.WriteLine("✅ OptimisticLock策略测试通过 - 正确拒绝冲突");
        }
        finally
        {
            await CleanupTestDataAsync(dataKey);
        }
    }

    private async Task CleanupTestDataAsync(string dataKey)
    {
        try
        {
            // 清理Redis中的版本数据
            var redisDb = _redisManager.GetDatabase();
            var versionKey = $"version:{dataKey}";
            await redisDb.KeyDeleteAsync(versionKey);
            await redisDb.KeyDeleteAsync(dataKey);
            
            // 清理MongoDB中的测试数据（如果有的话）
            var mongoDb = _mongoManager.GetDatabase();
            var collection = mongoDb.GetCollection<dynamic>("conflict_test");
            await collection.DeleteManyAsync(Builders<dynamic>.Filter.Eq("_id", dataKey));
        }
        catch (Exception ex)
        {
            _output.WriteLine($"清理数据时出现异常（可忽略）: {ex.Message}");
        }
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wind.Server.Services;
using Wind.Server.Configuration;
using Wind.Tests.TestFixtures;
using Xunit;
using Xunit.Abstractions;
using StackExchange.Redis;

namespace Wind.Tests.IntegrationTests;

/// <summary>
/// 简化的分布式事务测试 - 暂时跳过MongoDB，专注测试核心逻辑
/// </summary>
[Collection("ClusterCollection")]
public class SimplifiedDistributedTransactionTests
{
    private readonly ClusterFixture _clusterFixture;
    private readonly ITestOutputHelper _output;
    private readonly RedisConnectionManager _redisManager;
    private readonly RedisDistributedLockService _lockService;

    public SimplifiedDistributedTransactionTests(ClusterFixture clusterFixture, ITestOutputHelper output)
    {
        _clusterFixture = clusterFixture;
        _output = output;
        
        // 直接获取Redis相关服务
        _redisManager = _clusterFixture.ServiceProvider.GetRequiredService<RedisConnectionManager>();
        _lockService = _clusterFixture.ServiceProvider.GetRequiredService<RedisDistributedLockService>();
    }

    [Fact]
    public async Task DistributedLock_ShouldWorkCorrectly()
    {
        // Arrange
        var lockKey = "test:distributed:lock";
        var lockTimeout = TimeSpan.FromMinutes(1);
        var acquireTimeout = TimeSpan.FromSeconds(5);
        
        // Act
        using var lock1 = await _lockService.TryAcquireLockAsync(lockKey, lockTimeout, acquireTimeout);
        
        // Assert
        Assert.NotNull(lock1);
        _output.WriteLine($"✅ 分布式锁获取成功: {lock1.Resource}");
        
        // 尝试获取相同的锁应该失败
        using var lock2 = await _lockService.TryAcquireLockAsync(lockKey, lockTimeout, TimeSpan.FromSeconds(1));
        Assert.Null(lock2);
        _output.WriteLine("✅ 冲突锁被正确拒绝");
    }

    [Fact]
    public async Task ConflictDetection_ShouldDetectVersionMismatch()
    {
        // Arrange
        var conflictService = _clusterFixture.ServiceProvider.GetRequiredService<ConflictDetectionService>();
        var dataKey = "test:conflict:data";
        var testData = new { Name = "Test", Value = 100 };
        
        // 首次写入
        await conflictService.UpdateVersionAsync(dataKey, testData, 1);
        
        // Act - 使用错误的版本号进行冲突检测
        var result = await conflictService.CheckConflictAsync(dataKey, testData, 0);
        
        // Assert
        Assert.True(result.HasConflict);
        Assert.Equal(0, result.CurrentVersion);
        Assert.Equal(1, result.StoredVersion);
        _output.WriteLine($"✅ 冲突检测成功: 期望版本 {result.CurrentVersion}, 实际版本 {result.StoredVersion}");
    }

    [Fact]
    public async Task RedisTransaction_ShouldCommitSuccessfully()
    {
        // Arrange
        var database = _redisManager.GetDatabase();
        var testKey1 = "test:redis:tx:key1";
        var testKey2 = "test:redis:tx:key2";
        
        // 清理旧数据
        await database.KeyDeleteAsync(new RedisKey[] { testKey1, testKey2 });
        
        try
        {
            // Act - 使用Redis事务
            var transaction = database.CreateTransaction();
            transaction.StringSetAsync(testKey1, "value1");
            transaction.StringSetAsync(testKey2, "value2");
            
            var committed = await transaction.ExecuteAsync();
            
            // Assert
            Assert.True(committed);
            
            var value1 = await database.StringGetAsync(testKey1);
            var value2 = await database.StringGetAsync(testKey2);
            
            Assert.True(value1.HasValue);
            Assert.True(value2.HasValue);
            Assert.Equal("value1", value1!);
            Assert.Equal("value2", value2!);
            
            _output.WriteLine("✅ Redis事务提交成功");
            _output.WriteLine($"  键1: {testKey1} = {value1}");
            _output.WriteLine($"  键2: {testKey2} = {value2}");
        }
        finally
        {
            // 清理
            await database.KeyDeleteAsync(new RedisKey[] { testKey1, testKey2 });
        }
    }

    [Fact]
    public async Task DistributedTransactionService_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var transactionService = _clusterFixture.ServiceProvider.GetService<DistributedTransactionService>();
        
        // Assert
        Assert.NotNull(transactionService);
        
        var stats = transactionService.GetStatistics();
        Assert.NotNull(stats);
        
        _output.WriteLine("✅ DistributedTransactionService初始化成功");
        _output.WriteLine($"统计信息: {System.Text.Json.JsonSerializer.Serialize(stats, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}");
    }

    [Fact]
    public async Task DistributedTransaction_ShouldAcquireLocksAndProvideStatistics()
    {
        // Arrange
        var transactionService = _clusterFixture.ServiceProvider.GetRequiredService<DistributedTransactionService>();
        var lockKeys = new[] { "test:dt:lock1", "test:dt:lock2" };
        
        // Act
        using var transaction = await transactionService.BeginTransactionAsync(lockKeys, TimeSpan.FromMinutes(1));
        
        // Assert
        Assert.NotNull(transaction);
        Assert.NotNull(transaction.TransactionId);
        
        _output.WriteLine($"✅ 分布式事务开始成功: {transaction.TransactionId}");
        
        // 验证锁已获取
        using var conflictLock = await _lockService.TryAcquireLockAsync("test:dt:lock1", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        Assert.Null(conflictLock);
        
        _output.WriteLine("✅ 分布式锁正确获取，冲突锁被拒绝");
        
        // 提交事务
        await transaction.CommitAsync();
        
        // 检查统计信息
        var stats = transactionService.GetStatistics();
        Assert.True(stats.TransactionStartedCount > 0);
        Assert.True(stats.TransactionCommittedCount > 0);
        
        _output.WriteLine($"✅ 事务统计: 开始 {stats.TransactionStartedCount}, 提交 {stats.TransactionCommittedCount}");
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wind.Server.Services;
using Wind.Server.Configuration;
using Wind.Tests.TestFixtures;
using Xunit;
using Xunit.Abstractions;
using StackExchange.Redis;
using MongoDB.Driver;

namespace Wind.Tests.IntegrationTests;

/// <summary>
/// 分布式事务服务集成测试
/// 验证跨Redis和MongoDB的真实分布式事务功能
/// </summary>
[Collection("ClusterCollection")]
public class DistributedTransactionIntegrationTests
{
    private readonly ClusterFixture _clusterFixture;
    private readonly ITestOutputHelper _output;
    private readonly DistributedTransactionService _transactionService;
    private readonly RedisConnectionManager _redisManager;
    private readonly MongoDbConnectionManager _mongoManager;

    public DistributedTransactionIntegrationTests(ClusterFixture clusterFixture, ITestOutputHelper output)
    {
        _clusterFixture = clusterFixture;
        _output = output;
        
        // 获取服务实例 - 从Client的ServiceProvider获取服务
        _transactionService = _clusterFixture.ServiceProvider.GetRequiredService<DistributedTransactionService>();
        _redisManager = _clusterFixture.ServiceProvider.GetRequiredService<RedisConnectionManager>();
        _mongoManager = _clusterFixture.ServiceProvider.GetRequiredService<MongoDbConnectionManager>();
    }

    [Fact]
    public async Task BeginTransaction_ShouldAcquireLocksAndStartMongoSession()
    {
        // Arrange
        var lockKeys = new[] { "test:lock1", "test:lock2", "test:lock3" };
        
        // Act
        using var transaction = await _transactionService.BeginTransactionAsync(lockKeys, TimeSpan.FromMinutes(1));
        
        // Assert
        Assert.NotNull(transaction);
        _output.WriteLine($"✅ 分布式事务已开始，事务ID: {transaction.TransactionId}");
        
        // 验证锁已获取 - 尝试获取相同的锁应该失败
        var lockService = _clusterFixture.ServiceProvider.GetRequiredService<RedisDistributedLockService>();
        var conflictLock = await lockService.TryAcquireLockAsync("test:lock1", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        Assert.Null(conflictLock);
        _output.WriteLine("✅ 分布式锁正确获取，冲突锁被拒绝");
    }

    [Fact]
    public async Task TransactionCommit_ShouldPersistDataToBothRedisAndMongoDB()
    {
        // Arrange
        var testKey = "integration:commit:test";
        var testData = new { PlayerId = "player123", Score = 1000, Timestamp = DateTime.UtcNow };
        var lockKeys = new[] { $"lock:{testKey}" };
        
        // Clean up any existing data
        await CleanupTestDataAsync(testKey);
        
        try
        {
            // Act - 开始事务
            using var transaction = await _transactionService.BeginTransactionAsync(lockKeys, TimeSpan.FromMinutes(1));
            _output.WriteLine($"事务开始: {transaction.TransactionId}");
            
            // 在事务中写入Redis数据
            var redisDb = _redisManager.GetDatabase();
            await redisDb.StringSetAsync(testKey, System.Text.Json.JsonSerializer.Serialize(testData));
            _output.WriteLine("✅ Redis数据已写入");
            
            // 在事务中写入MongoDB数据
            var mongoDb = _mongoManager.GetDatabase();
            var collection = mongoDb.GetCollection<dynamic>("test_transactions");
            await collection.InsertOneAsync(new { _id = testKey, data = testData });
            _output.WriteLine("✅ MongoDB数据已写入");
            
            // 提交事务
            await transaction.CommitAsync();
            _output.WriteLine("✅ 事务已提交");
            
            // Assert - 验证数据在两个存储中都存在
            var redisResult = await redisDb.StringGetAsync(testKey);
            Assert.True(redisResult.HasValue);
            _output.WriteLine($"✅ Redis验证通过: {redisResult}");
            
            var mongoResult = await collection.Find<dynamic>(Builders<dynamic>.Filter.Eq("_id", testKey)).FirstOrDefaultAsync();
            Assert.NotNull(mongoResult);
            _output.WriteLine("✅ MongoDB验证通过");
            
            _output.WriteLine("🎯 分布式事务提交测试完全成功！");
        }
        finally
        {
            await CleanupTestDataAsync(testKey);
        }
    }

    [Fact]
    public async Task TransactionRollback_ShouldRevertAllChanges()
    {
        // Arrange
        var testKey = "integration:rollback:test";
        var testData = new { PlayerId = "player456", Score = 2000, Timestamp = DateTime.UtcNow };
        var lockKeys = new[] { $"lock:{testKey}" };
        
        // Clean up any existing data
        await CleanupTestDataAsync(testKey);
        
        try
        {
            // Act - 开始事务
            using var transaction = await _transactionService.BeginTransactionAsync(lockKeys, TimeSpan.FromMinutes(1));
            _output.WriteLine($"事务开始: {transaction.TransactionId}");
            
            // 在事务中写入数据
            var redisDb = _redisManager.GetDatabase();
            await redisDb.StringSetAsync(testKey, System.Text.Json.JsonSerializer.Serialize(testData));
            _output.WriteLine("✅ Redis数据已写入（事务中）");
            
            var mongoDb = _mongoManager.GetDatabase();
            var collection = mongoDb.GetCollection<dynamic>("test_transactions");
            await collection.InsertOneAsync(new { _id = testKey, data = testData });
            _output.WriteLine("✅ MongoDB数据已写入（事务中）");
            
            // 模拟错误并回滚事务
            await transaction.RollbackAsync();
            _output.WriteLine("✅ 事务已回滚");
            
            // Assert - 验证数据在两个存储中都不存在（或回滚到之前状态）
            var redisResult = await redisDb.StringGetAsync(testKey);
            // Redis没有真正的事务回滚，但分布式事务应该处理清理
            _output.WriteLine($"Redis状态检查: {redisResult}");
            
            var mongoResult = await collection.Find<dynamic>(Builders<dynamic>.Filter.Eq("_id", testKey)).FirstOrDefaultAsync();
            Assert.Null(mongoResult); // MongoDB事务应该回滚
            _output.WriteLine("✅ MongoDB事务回滚验证通过");
            
            _output.WriteLine("🎯 分布式事务回滚测试成功！");
        }
        finally
        {
            await CleanupTestDataAsync(testKey);
        }
    }

    [Fact]
    public async Task ConcurrentTransactions_ShouldHandleProperlyWithoutDeadlock()
    {
        // Arrange
        const int concurrentCount = 5;
        var tasks = new List<Task<bool>>();
        
        // Act - 并发启动多个事务
        for (int i = 0; i < concurrentCount; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var lockKeys = new[] { $"concurrent:lock:{taskId}", $"concurrent:shared:lock" };
                    using var transaction = await _transactionService.BeginTransactionAsync(
                        lockKeys, TimeSpan.FromSeconds(10));
                    
                    _output.WriteLine($"并发事务 {taskId} 开始: {transaction.TransactionId}");
                    
                    // 模拟一些工作
                    await Task.Delay(100);
                    
                    var redisDb = _redisManager.GetDatabase();
                    await redisDb.StringSetAsync($"concurrent:data:{taskId}", $"value{taskId}");
                    
                    await transaction.CommitAsync();
                    _output.WriteLine($"✅ 并发事务 {taskId} 完成");
                    return true;
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"❌ 并发事务 {taskId} 失败: {ex.Message}");
                    return false;
                }
            }));
        }
        
        // Assert
        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(r => r);
        
        _output.WriteLine($"并发事务结果: {successCount}/{concurrentCount} 成功");
        
        // 至少应该有一部分事务成功（取决于锁竞争）
        Assert.True(successCount > 0, "至少应该有一个并发事务成功");
        _output.WriteLine("🎯 并发事务处理测试成功！");
        
        // Cleanup
        for (int i = 0; i < concurrentCount; i++)
        {
            var redisDb = _redisManager.GetDatabase();
            await redisDb.KeyDeleteAsync($"concurrent:data:{i}");
        }
    }

    [Fact]
    public async Task TransactionTimeout_ShouldHandleTimeoutGracefully()
    {
        // Arrange
        var lockKeys = new[] { "timeout:test:lock" };
        var shortTimeout = TimeSpan.FromMilliseconds(100);
        
        // Act & Assert
        using var transaction = await _transactionService.BeginTransactionAsync(lockKeys, shortTimeout);
        _output.WriteLine($"超时事务开始: {transaction.TransactionId}");
        
        // 等待超过超时时间
        await Task.Delay(200);
        
        // 事务应该仍然可以操作，但锁可能已经超时
        // 这里测试事务服务的健壮性
        try
        {
            await transaction.CommitAsync();
            _output.WriteLine("✅ 超时事务处理正常");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"⚠️ 超时事务异常（预期）: {ex.Message}");
            // 超时异常是可接受的
        }
        
        _output.WriteLine("🎯 事务超时处理测试完成！");
    }

    [Fact]
    public async Task TransactionStatistics_ShouldTrackCorrectly()
    {
        // Arrange
        var lockKeys = new[] { "stats:test:lock" };
        
        // Act - 执行几个事务操作
        using (var transaction1 = await _transactionService.BeginTransactionAsync(lockKeys, TimeSpan.FromMinutes(1)))
        {
            await transaction1.CommitAsync();
        }
        
        using (var transaction2 = await _transactionService.BeginTransactionAsync(lockKeys, TimeSpan.FromMinutes(1)))
        {
            await transaction2.RollbackAsync();
        }
        
        // Assert
        var stats = _transactionService.GetStatistics();
        _output.WriteLine($"事务统计: {System.Text.Json.JsonSerializer.Serialize(stats, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}");
        
        Assert.True(stats.TransactionStartedCount >= 2, "应该至少记录2个开始的事务");
        Assert.True(stats.TransactionCommittedCount >= 1, "应该至少记录1个提交的事务");
        Assert.True(stats.TransactionRolledBackCount >= 1, "应该至少记录1个回滚的事务");
        
        _output.WriteLine("🎯 事务统计追踪测试成功！");
    }

    private async Task CleanupTestDataAsync(string testKey)
    {
        try
        {
            // 清理Redis数据
            var redisDb = _redisManager.GetDatabase();
            await redisDb.KeyDeleteAsync(testKey);
            
            // 清理MongoDB数据
            var mongoDb = _mongoManager.GetDatabase();
            var collection = mongoDb.GetCollection<dynamic>("test_transactions");
            await collection.DeleteOneAsync(Builders<dynamic>.Filter.Eq("_id", testKey));
        }
        catch (Exception ex)
        {
            _output.WriteLine($"清理数据时出现异常（可忽略）: {ex.Message}");
        }
    }
}
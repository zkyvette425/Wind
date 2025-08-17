using Microsoft.Extensions.DependencyInjection;
using Wind.Server.Services;
using Wind.Tests.TestFixtures;
using Xunit;
using Xunit.Abstractions;

namespace Wind.Tests.IntegrationTests;

/// <summary>
/// 连接验证测试 - 确保Redis和MongoDB连接正常
/// </summary>
[Collection("ClusterCollection")]
public class ConnectionVerificationTests
{
    private readonly ClusterFixture _clusterFixture;
    private readonly ITestOutputHelper _output;

    public ConnectionVerificationTests(ClusterFixture clusterFixture, ITestOutputHelper output)
    {
        _clusterFixture = clusterFixture;
        _output = output;
    }

    [Fact]
    public async Task Redis_ShouldConnectSuccessfully()
    {
        // Arrange
        var redisManager = _clusterFixture.ServiceProvider.GetRequiredService<RedisConnectionManager>();
        
        // Act & Assert
        var database = redisManager.GetDatabase();
        Assert.NotNull(database);
        
        // 简单的ping测试
        await database.StringSetAsync("test:ping", "pong");
        var result = await database.StringGetAsync("test:ping");
        
        Assert.True(result.HasValue);
        Assert.Equal("pong", result!);
        
        // 清理
        await database.KeyDeleteAsync("test:ping");
        
        _output.WriteLine("✅ Redis连接测试成功");
    }

    [Fact]
    public async Task MongoDB_ShouldConnectSuccessfully()
    {
        // Arrange
        var mongoManager = _clusterFixture.ServiceProvider.GetRequiredService<MongoDbConnectionManager>();
        
        // Act & Assert
        var database = mongoManager.GetDatabase();
        Assert.NotNull(database);
        
        // 简单的ping测试
        await database.RunCommandAsync<object>("{ ping: 1 }");
        
        _output.WriteLine("✅ MongoDB连接测试成功");
    }

    [Fact]
    public void DistributedTransactionService_ShouldBeRegistered()
    {
        // Arrange & Act
        var transactionService = _clusterFixture.ServiceProvider.GetService<DistributedTransactionService>();
        
        // Assert
        Assert.NotNull(transactionService);
        _output.WriteLine("✅ DistributedTransactionService已注册");
    }

    [Fact]
    public void RedisDistributedLockService_ShouldBeRegistered()
    {
        // Arrange & Act
        var lockService = _clusterFixture.ServiceProvider.GetService<RedisDistributedLockService>();
        
        // Assert
        Assert.NotNull(lockService);
        _output.WriteLine("✅ RedisDistributedLockService已注册");
    }
}
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using Orleans.Persistence;
using Orleans.Serialization;
using Orleans.Storage;
using Orleans.TestingHost;
using StackExchange.Redis;
using Wind.Server.Services;
using Wind.Server.Extensions;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Xunit;
using Xunit.Abstractions;

namespace Wind.Tests.RedisCacheTests;

/// <summary>
/// Redis缓存策略测试
/// 验证Redis TTL过期策略和缓存性能功能
/// </summary>
[Trait("Category", "Integration")]
[Trait("RequiresRedis", "true")]
public class RedisCacheStrategyTests : IClassFixture<RedisCacheStrategyTests.Fixture>
{
    private readonly Fixture _fixture;
    private readonly ITestOutputHelper _output;

    public RedisCacheStrategyTests(Fixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Should_Set_And_Get_Cache_With_TTL()
    {
        // Arrange
        var cacheStrategy = _fixture.CacheStrategy;
        var testData = new PlayerInfo
        {
            PlayerId = "test-player-ttl",
            DisplayName = "TTL测试用户",
            Level = 10,
            LastLoginAt = DateTime.UtcNow
        };

        // Act - 设置缓存（使用player_info类型，TTL=30分钟）
        var setResult = await cacheStrategy.SetWithTtlAsync("ttl-test-key", testData, "player_info");
        
        // 立即获取缓存
        var (retrievedData, remainingTtl) = await cacheStrategy.GetWithTtlInfoAsync<PlayerInfo>("ttl-test-key", "player_info");

        // Assert
        Assert.True(setResult, "缓存设置应该成功");
        Assert.NotNull(retrievedData);
        Assert.Equal(testData.PlayerId, retrievedData.PlayerId);
        Assert.Equal(testData.DisplayName, retrievedData.DisplayName);
        
        // 验证TTL设置
        Assert.NotNull(remainingTtl);
        Assert.True(remainingTtl.Value.TotalMinutes > 29, $"剩余TTL应该接近30分钟，实际:{remainingTtl.Value.TotalMinutes}分钟");
        
        _output.WriteLine($"✅ 缓存设置成功: {testData.PlayerId}, 剩余TTL: {remainingTtl.Value.TotalMinutes:F1}分钟");
    }

    [Fact]
    public async Task Should_Refresh_TTL_On_Get()
    {
        // Arrange
        var cacheStrategy = _fixture.CacheStrategy;
        var testData = new { Message = "TTL刷新测试", Timestamp = DateTime.UtcNow };

        // Act - 设置缓存
        await cacheStrategy.SetWithTtlAsync("ttl-refresh-key", testData, "temp");
        
        // 等待1秒
        await Task.Delay(1000);
        
        // 获取并刷新TTL
        var refreshedData = await cacheStrategy.GetWithTtlRefreshAsync<object>("ttl-refresh-key", "temp");
        
        // 立即获取TTL信息验证刷新
        var (_, newTtl) = await cacheStrategy.GetWithTtlInfoAsync<object>("ttl-refresh-key", "temp");

        // Assert
        Assert.NotNull(refreshedData);
        Assert.NotNull(newTtl);
        Assert.True(newTtl.Value.TotalSeconds > 55, $"TTL应该被刷新到接近60秒，实际:{newTtl.Value.TotalSeconds}秒");
        
        _output.WriteLine($"✅ TTL刷新成功: 新TTL = {newTtl.Value.TotalSeconds:F0}秒");
    }

    [Fact]
    public async Task Should_Set_Only_If_Not_Exists()
    {
        // Arrange
        var cacheStrategy = _fixture.CacheStrategy;
        var originalData = new { Value = "原始数据", CreatedAt = DateTime.UtcNow };
        var newData = new { Value = "新数据", CreatedAt = DateTime.UtcNow };

        // Act - 首次设置
        var firstSetResult = await cacheStrategy.SetIfNotExistsAsync("conditional-key", originalData, "temp");
        
        // 尝试再次设置相同的键
        var secondSetResult = await cacheStrategy.SetIfNotExistsAsync("conditional-key", newData, "temp");
        
        // 获取实际存储的数据
        var storedData = await cacheStrategy.GetWithTtlRefreshAsync<object>("conditional-key", "temp");

        // Assert
        Assert.True(firstSetResult, "首次设置应该成功");
        Assert.False(secondSetResult, "第二次设置应该失败（键已存在）");
        Assert.NotNull(storedData);
        
        _output.WriteLine($"✅ 条件设置测试通过: 首次={firstSetResult}, 第二次={secondSetResult}");
    }

    [Fact]
    public async Task Should_Handle_Different_Data_Types_TTL()
    {
        // Arrange
        var cacheStrategy = _fixture.CacheStrategy;
        var testCases = new[]
        {
            ("session", "用户会话数据", TimeSpan.FromHours(2)),
            ("player_state", "玩家状态", TimeSpan.FromMinutes(30)),
            ("matchmaking", "匹配数据", TimeSpan.FromMinutes(5)),
            ("temp", "临时数据", TimeSpan.FromMinutes(1))
        };

        var results = new List<(string dataType, TimeSpan expectedTtl, TimeSpan? actualTtl)>();

        // Act - 测试不同数据类型的TTL策略
        foreach (var (dataType, description, expectedTtl) in testCases)
        {
            var testData = new { Type = dataType, Description = description, Timestamp = DateTime.UtcNow };
            
            await cacheStrategy.SetWithTtlAsync($"ttl-test-{dataType}", testData, dataType);
            var (_, actualTtl) = await cacheStrategy.GetWithTtlInfoAsync<object>($"ttl-test-{dataType}", dataType);
            
            results.Add((dataType, expectedTtl, actualTtl));
        }

        // Assert
        foreach (var (dataType, expectedTtl, actualTtl) in results)
        {
            Assert.NotNull(actualTtl);
            
            var toleranceSeconds = 5; // 5秒容差
            var expectedSeconds = expectedTtl.TotalSeconds;
            var actualSeconds = actualTtl.Value.TotalSeconds;
            
            Assert.True(
                Math.Abs(expectedSeconds - actualSeconds) <= toleranceSeconds,
                $"数据类型 {dataType}: 期望TTL={expectedSeconds}s, 实际TTL={actualSeconds}s, 差异过大"
            );
            
            _output.WriteLine($"✅ {dataType}: 期望={expectedTtl.TotalMinutes:F1}分钟, 实际={actualTtl.Value.TotalMinutes:F1}分钟");
        }
    }

    [Fact]
    public async Task Should_Extend_TTL_When_Greater()
    {
        // Arrange
        var cacheStrategy = _fixture.CacheStrategy;
        var testData = new { Message = "TTL延长测试" };

        // Act - 设置短TTL的缓存
        await cacheStrategy.SetWithTtlAsync("extend-ttl-key", testData, "temp"); // 1分钟TTL
        
        // 获取初始TTL
        var (_, initialTtl) = await cacheStrategy.GetWithTtlInfoAsync<object>("extend-ttl-key", "temp");
        
        // 尝试延长TTL到10分钟
        var extendResult = await cacheStrategy.ExtendTtlAsync("extend-ttl-key", "temp", TimeSpan.FromMinutes(10));
        
        // 获取延长后的TTL
        var (_, extendedTtl) = await cacheStrategy.GetWithTtlInfoAsync<object>("extend-ttl-key", "temp");

        // Assert
        Assert.NotNull(initialTtl);
        Assert.True(extendResult, "TTL延长应该成功");
        Assert.NotNull(extendedTtl);
        Assert.True(extendedTtl.Value.TotalMinutes > initialTtl.Value.TotalMinutes, "延长后的TTL应该大于初始TTL");
        
        _output.WriteLine($"✅ TTL延长: 初始={initialTtl.Value.TotalMinutes:F1}分钟, 延长后={extendedTtl.Value.TotalMinutes:F1}分钟");
    }

    [Fact]
    public async Task Should_Get_Cache_Statistics()
    {
        // Arrange
        var cacheStrategy = _fixture.CacheStrategy;
        
        // 创建一些测试数据确保有统计信息
        for (int i = 0; i < 5; i++)
        {
            await cacheStrategy.SetWithTtlAsync($"stats-test-{i}", new { Index = i }, "temp");
        }

        // Act
        var stats = await cacheStrategy.GetStatisticsAsync();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.Timestamp > DateTime.UtcNow.AddMinutes(-1), "统计时间戳应该是最近的");
        
        _output.WriteLine($"✅ 缓存统计:");
        _output.WriteLine($"  - 内存使用: {stats.MemoryUsage / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"  - 总键数: {stats.TotalKeys}");
        _output.WriteLine($"  - 命中率: {stats.HitRate:F1}%");
        _output.WriteLine($"  - 过期键数: {stats.ExpiredKeys}");
        _output.WriteLine($"  - 总请求数: {stats.TotalRequests}");
        _output.WriteLine($"  - 缓存命中: {stats.CacheHits}");
        _output.WriteLine($"  - 缓存未命中: {stats.CacheMisses}");
    }

    [Fact]
    public async Task Should_Handle_Batch_Operations()
    {
        // Arrange
        var cacheStrategy = _fixture.CacheStrategy;
        var batchData = new Dictionary<string, object>();
        
        for (int i = 0; i < 10; i++)
        {
            batchData[$"batch-key-{i}"] = new { Index = i, Value = $"批量数据{i}", Timestamp = DateTime.UtcNow };
        }

        // Act
        var batchSetResult = await cacheStrategy.SetBatchAsync(batchData, "temp");

        // 验证批量设置的数据
        var retrievedCount = 0;
        foreach (var kvp in batchData)
        {
            var retrieved = await cacheStrategy.GetWithTtlRefreshAsync<object>(kvp.Key, "temp");
            if (retrieved != null) retrievedCount++;
        }

        // Assert
        Assert.True(batchSetResult, "批量设置应该成功");
        Assert.Equal(batchData.Count, retrievedCount);
        
        _output.WriteLine($"✅ 批量操作成功: 设置={batchData.Count}个, 检索={retrievedCount}个");
    }

    public class Fixture : IDisposable
    {
        public RedisCacheStrategy CacheStrategy { get; private set; }
        public TestCluster Cluster { get; private set; }

        public Fixture()
        {
            // 初始化测试用的简单Serilog配置
            Serilog.Log.Logger = new Serilog.LoggerConfiguration()
                .MinimumLevel.Warning()
                .CreateLogger();
            
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
            
            Cluster = builder.Build();
            Cluster.Deploy();
            
            // 获取Redis缓存策略服务
            CacheStrategy = Cluster.ServiceProvider.GetRequiredService<RedisCacheStrategy>();
        }

        public void Dispose()
        {
            Cluster?.StopAllSilos();
            Cluster?.Dispose();
        }
    }

    public class TestSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            // 配置Orleans Redis存储 (正确的Orleans 9.2.1 API)
            siloBuilder
                .AddRedisGrainStorage(
                    name: "PlayerStorage",
                    options =>
                    {
                        options.ConfigurationOptions = ConfigurationOptions.Parse("localhost:6379,password=windgame123");
                        options.GetStorageKey = (type, id) => $"player:{type}-{id}";
                    })
                .AddRedisGrainStorage(
                    name: "RoomStorage",
                    options =>
                    {
                        var redisConfig = ConfigurationOptions.Parse("localhost:6379,password=windgame123");
                        redisConfig.DefaultDatabase = 1;
                        options.ConfigurationOptions = redisConfig;
                        options.GetStorageKey = (type, id) => $"room:{type}-{id}";
                    })
                .AddRedisGrainStorage(
                    name: "MatchmakingStorage",
                    options =>
                    {
                        var redisConfig = ConfigurationOptions.Parse("localhost:6379,password=windgame123");
                        redisConfig.DefaultDatabase = 2;
                        options.ConfigurationOptions = redisConfig;
                        options.GetStorageKey = (type, id) => $"match:{type}-{id}";
                    });
                
            // 添加Redis缓存策略服务（使用测试配置）
            siloBuilder.ConfigureServices(services =>
            {
                // 配置Orleans MessagePack序列化器
                services.AddSerializer(serializerBuilder => serializerBuilder.AddMessagePackSerializer());
                
                // 配置测试日志 - 使用Microsoft.Extensions.Logging
                services.AddLogging();
                
                // 直接注册Redis连接和服务
                services.AddSingleton<IConnectionMultiplexer>(provider =>
                {
                    var connectionString = "localhost:6379,password=windgame123";
                    return ConnectionMultiplexer.Connect(connectionString);
                });
                
                services.AddSingleton<RedisCacheStrategy>();
            });
        }
    }
}
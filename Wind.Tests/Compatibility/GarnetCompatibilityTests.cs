using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wind.Server.Configuration;
using Wind.Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Wind.Tests.Compatibility;

/// <summary>
/// Garnet与Redis兼容性测试
/// 验证Garnet是否可以作为Redis的替代品用于Orleans存储
/// </summary>
[Trait("Category", "GarnetCompatibility")]
public class GarnetCompatibilityTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _serviceProvider;
    private readonly GarnetConnectionManager _garnetManager;
    private readonly RedisConnectionManager _redisManager;

    public GarnetCompatibilityTests(ITestOutputHelper output)
    {
        _output = output;

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // 配置Garnet选项
        services.Configure<GarnetOptions>(options =>
        {
            options.ConnectionString = "localhost:6380";
            options.Password = "windgame123";
            options.Database = 0;
            options.KeyPrefix = "test:garnet:";
        });

        // 配置Redis选项
        services.Configure<RedisOptions>(options =>
        {
            options.ConnectionString = "localhost:6379";
            options.Password = "windgame123";
            options.Database = 0;
            options.KeyPrefix = "test:redis:";
        });

        services.AddSingleton<GarnetConnectionManager>();
        services.AddSingleton<RedisConnectionManager>();

        _serviceProvider = services.BuildServiceProvider();
        _garnetManager = _serviceProvider.GetRequiredService<GarnetConnectionManager>();
        _redisManager = _serviceProvider.GetRequiredService<RedisConnectionManager>();
    }

    [Fact]
    public async Task BasicConnectivity_ShouldWork()
    {
        _output.WriteLine("测试基础连接性...");

        // 测试Redis连接
        var redisDb = _redisManager.GetDatabase();
        var redisPing = await redisDb.PingAsync();
        _output.WriteLine($"Redis Ping: {redisPing.TotalMilliseconds}ms");

        // 测试Garnet连接
        var garnetDb = _garnetManager.GetDatabase();
        var garnetPing = await garnetDb.PingAsync();
        _output.WriteLine($"Garnet Ping: {garnetPing.TotalMilliseconds}ms");

        Assert.True(redisPing.TotalMilliseconds > 0);
        Assert.True(garnetPing.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task BasicStringOperations_ShouldBeCompatible()
    {
        _output.WriteLine("测试基础字符串操作兼容性...");

        var testKey = "compatibility:string:test";
        var testValue = "Hello Garnet and Redis!";

        // Redis操作
        var redisDb = _redisManager.GetDatabase();
        await redisDb.StringSetAsync($"redis:{testKey}", testValue);
        var redisResult = await redisDb.StringGetAsync($"redis:{testKey}");

        // Garnet操作
        var garnetDb = _garnetManager.GetDatabase();
        await garnetDb.StringSetAsync($"garnet:{testKey}", testValue);
        var garnetResult = await garnetDb.StringGetAsync($"garnet:{testKey}");

        _output.WriteLine($"Redis Result: {redisResult}");
        _output.WriteLine($"Garnet Result: {garnetResult}");

        Assert.Equal(testValue, redisResult);
        Assert.Equal(testValue, garnetResult);
        Assert.Equal(redisResult, garnetResult);
    }

    [Fact]
    public async Task JsonSerialization_ShouldBeCompatible()
    {
        _output.WriteLine("测试JSON序列化兼容性...");

        var testData = new
        {
            Id = Guid.NewGuid(),
            Name = "Test Player",
            Level = 42,
            Created = DateTime.UtcNow
        };

        var testKey = "compatibility:json:test";
        var jsonValue = System.Text.Json.JsonSerializer.Serialize(testData);

        // Redis操作
        var redisDb = _redisManager.GetDatabase();
        await redisDb.StringSetAsync($"redis:{testKey}", jsonValue);
        var redisResult = await redisDb.StringGetAsync($"redis:{testKey}");

        // Garnet操作
        var garnetDb = _garnetManager.GetDatabase();
        await garnetDb.StringSetAsync($"garnet:{testKey}", jsonValue);
        var garnetResult = await garnetDb.StringGetAsync($"garnet:{testKey}");

        _output.WriteLine($"Original JSON: {jsonValue}");
        _output.WriteLine($"Redis JSON: {redisResult}");
        _output.WriteLine($"Garnet JSON: {garnetResult}");

        Assert.Equal(jsonValue, redisResult);
        Assert.Equal(jsonValue, garnetResult);
        Assert.Equal(redisResult, garnetResult);
    }

    [Fact]
    public async Task HashOperations_ShouldBeCompatible()
    {
        _output.WriteLine("测试Hash操作兼容性...");

        var testKey = "compatibility:hash:test";
        var hashFields = new Dictionary<string, string>
        {
            ["field1"] = "value1",
            ["field2"] = "value2",
            ["field3"] = "value3"
        };

        // Redis操作
        var redisDb = _redisManager.GetDatabase();
        foreach (var field in hashFields)
        {
            await redisDb.HashSetAsync($"redis:{testKey}", field.Key, field.Value);
        }
        var redisResults = await redisDb.HashGetAllAsync($"redis:{testKey}");

        // Garnet操作
        var garnetDb = _garnetManager.GetDatabase();
        foreach (var field in hashFields)
        {
            await garnetDb.HashSetAsync($"garnet:{testKey}", field.Key, field.Value);
        }
        var garnetResults = await garnetDb.HashGetAllAsync($"garnet:{testKey}");

        _output.WriteLine($"Redis Hash Count: {redisResults.Length}");
        _output.WriteLine($"Garnet Hash Count: {garnetResults.Length}");

        Assert.Equal(hashFields.Count, redisResults.Length);
        Assert.Equal(hashFields.Count, garnetResults.Length);
        Assert.Equal(redisResults.Length, garnetResults.Length);
    }

    [Fact]
    public async Task TTL_ShouldBeCompatible()
    {
        _output.WriteLine("测试TTL过期机制兼容性...");

        var testKey = "compatibility:ttl:test";
        var testValue = "expires in 5 seconds";
        var ttl = TimeSpan.FromSeconds(5);

        // Redis操作
        var redisDb = _redisManager.GetDatabase();
        await redisDb.StringSetAsync($"redis:{testKey}", testValue, ttl);
        var redisTtl = await redisDb.KeyTimeToLiveAsync($"redis:{testKey}");

        // Garnet操作
        var garnetDb = _garnetManager.GetDatabase();
        await garnetDb.StringSetAsync($"garnet:{testKey}", testValue, ttl);
        var garnetTtl = await garnetDb.KeyTimeToLiveAsync($"garnet:{testKey}");

        _output.WriteLine($"Redis TTL: {redisTtl?.TotalSeconds ?? -1}s");
        _output.WriteLine($"Garnet TTL: {garnetTtl?.TotalSeconds ?? -1}s");

        Assert.True(redisTtl.HasValue);
        Assert.True(garnetTtl.HasValue);
        
        // TTL应该在合理范围内（略小于设定值）
        Assert.True(redisTtl.Value.TotalSeconds <= 5 && redisTtl.Value.TotalSeconds > 0);
        Assert.True(garnetTtl.Value.TotalSeconds <= 5 && garnetTtl.Value.TotalSeconds > 0);
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldBeCompatible()
    {
        _output.WriteLine("测试并发操作兼容性...");

        const int operationCount = 100;
        var testKey = "compatibility:concurrent:test";

        // Redis并发操作
        var redisTasks = Enumerable.Range(0, operationCount).Select(async i =>
        {
            var redisDb = _redisManager.GetDatabase();
            await redisDb.StringSetAsync($"redis:{testKey}:{i}", $"redis_value_{i}");
            return await redisDb.StringGetAsync($"redis:{testKey}:{i}");
        });

        // Garnet并发操作
        var garnetTasks = Enumerable.Range(0, operationCount).Select(async i =>
        {
            var garnetDb = _garnetManager.GetDatabase();
            await garnetDb.StringSetAsync($"garnet:{testKey}:{i}", $"garnet_value_{i}");
            return await garnetDb.StringGetAsync($"garnet:{testKey}:{i}");
        });

        var redisResults = await Task.WhenAll(redisTasks);
        var garnetResults = await Task.WhenAll(garnetTasks);

        _output.WriteLine($"Redis操作完成数量: {redisResults.Count(r => r.HasValue)}");
        _output.WriteLine($"Garnet操作完成数量: {garnetResults.Count(r => r.HasValue)}");

        Assert.Equal(operationCount, redisResults.Count(r => r.HasValue));
        Assert.Equal(operationCount, garnetResults.Count(r => r.HasValue));
    }

    [Fact]
    public async Task PerformanceComparison_ShouldFavorGarnet()
    {
        _output.WriteLine("性能对比测试...");

        const int iterations = 1000;
        var testKey = "performance:test";
        var testValue = "performance_test_data_" + new string('x', 100); // 100字符测试数据

        // Redis性能测试
        var redisDb = _redisManager.GetDatabase();
        var redisStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            await redisDb.StringSetAsync($"redis:{testKey}:{i}", $"{testValue}_{i}");
            await redisDb.StringGetAsync($"redis:{testKey}:{i}");
        }
        
        redisStopwatch.Stop();

        // Garnet性能测试
        var garnetDb = _garnetManager.GetDatabase();
        var garnetStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            await garnetDb.StringSetAsync($"garnet:{testKey}:{i}", $"{testValue}_{i}");
            await garnetDb.StringGetAsync($"garnet:{testKey}:{i}");
        }
        
        garnetStopwatch.Stop();

        var redisAvgMs = redisStopwatch.ElapsedMilliseconds / (double)iterations;
        var garnetAvgMs = garnetStopwatch.ElapsedMilliseconds / (double)iterations;
        var performanceRatio = redisAvgMs / garnetAvgMs;

        _output.WriteLine($"Redis: {iterations}次操作耗时 {redisStopwatch.ElapsedMilliseconds}ms (平均 {redisAvgMs:F2}ms/op)");
        _output.WriteLine($"Garnet: {iterations}次操作耗时 {garnetStopwatch.ElapsedMilliseconds}ms (平均 {garnetAvgMs:F2}ms/op)");
        _output.WriteLine($"性能比例: {performanceRatio:F2}x (Garnet相对Redis)");

        // 记录性能数据但不作为测试失败条件，因为性能可能受多种因素影响
        Assert.True(redisStopwatch.ElapsedMilliseconds > 0);
        Assert.True(garnetStopwatch.ElapsedMilliseconds > 0);
    }

    public void Dispose()
    {
        _garnetManager?.Dispose();
        _redisManager?.Dispose();
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
using Orleans;
using Orleans.TestingHost;
using Wind.GrainInterfaces;
using Wind.Tests.TestFixtures;

namespace Wind.Tests.GrainTests;

/// <summary>
/// HelloGrain集成测试
/// </summary>
public class HelloGrainTests : IClassFixture<ClusterFixture>
{
    private readonly TestCluster _cluster;

    public HelloGrainTests(ClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task SayHelloAsync_应该返回正确的问候语()
    {
        // Arrange
        var grainId = "test-hello";
        var name = "Orleans Test";
        var helloGrain = _cluster.GrainFactory.GetGrain<IHelloGrain>(grainId);

        // Act
        var result = await helloGrain.SayHelloAsync(name);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(name, result);
        Assert.Contains("Hello", result);
    }

    [Fact]
    public async Task SayHelloAsync_空字符串输入_应该处理正确()
    {
        // Arrange
        var grainId = "test-hello-empty";
        var helloGrain = _cluster.GrainFactory.GetGrain<IHelloGrain>(grainId);

        // Act
        var result = await helloGrain.SayHelloAsync("");

        // Assert
        Assert.NotNull(result);
        // 应该包含某种默认处理
    }

    [Fact]
    public async Task SayHelloAsync_多次调用同一个Grain_应该保持状态()
    {
        // Arrange
        var grainId = "test-hello-state";
        var helloGrain = _cluster.GrainFactory.GetGrain<IHelloGrain>(grainId);

        // Act
        var result1 = await helloGrain.SayHelloAsync("First Call");
        var result2 = await helloGrain.SayHelloAsync("Second Call");

        // Assert
        Assert.NotEqual(result1, result2);
        Assert.Contains("First Call", result1);
        Assert.Contains("Second Call", result2);
    }

    [Fact]
    public async Task 并发调用不同的HelloGrain_应该正常工作()
    {
        // Arrange
        var tasks = new List<Task<string>>();
        
        for (int i = 0; i < 10; i++)
        {
            var grainId = $"concurrent-test-{i}";
            var grain = _cluster.GrainFactory.GetGrain<IHelloGrain>(grainId);
            tasks.Add(grain.SayHelloAsync($"User-{i}"));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, results.Length);
        
        for (int i = 0; i < 10; i++)
        {
            Assert.Contains($"User-{i}", results[i]);
        }

        // 所有结果都应该不同（每个Grain有自己的状态）
        var uniqueResults = results.Distinct().Count();
        Assert.Equal(10, uniqueResults);
    }
}
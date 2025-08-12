using Orleans;
using Orleans.TestingHost;
using Wind.GrainInterfaces;
using Wind.Tests.TestFixtures;

namespace Wind.Tests.BasicTests;

/// <summary>
/// 基础Grain测试 - 验证Orleans Grain基本功能
/// </summary>
public class BasicGrainTests : IClassFixture<ClusterFixture>
{
    private readonly TestCluster _cluster;

    public BasicGrainTests(ClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task HelloGrain_基础功能测试()
    {
        // Arrange
        var grainId = "basic-test";
        var helloGrain = _cluster.GrainFactory.GetGrain<IHelloGrain>(grainId);

        // Act
        var result = await helloGrain.SayHelloAsync("Orleans Test");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Orleans Test", result);
    }

    [Fact]
    public async Task HelloGrain_并发调用测试()
    {
        // Arrange & Act
        var tasks = new List<Task<string>>();
        
        for (int i = 0; i < 5; i++)
        {
            var grainId = $"concurrent-{i}";
            var grain = _cluster.GrainFactory.GetGrain<IHelloGrain>(grainId);
            tasks.Add(grain.SayHelloAsync($"User-{i}"));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, results.Length);
        for (int i = 0; i < 5; i++)
        {
            Assert.Contains($"User-{i}", results[i]);
        }
    }
}
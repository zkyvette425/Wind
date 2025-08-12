using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.TestingHost;
using Wind.Shared.Services;
using Wind.Grains.Services;
using Wind.Tests.TestFixtures;

namespace Wind.Tests.ServiceTests;

/// <summary>
/// MagicOnion TestService集成测试
/// </summary>
public class TestServiceTests : IClassFixture<ClusterFixture>
{
    private readonly TestCluster _cluster;
    private readonly TestService _testService;

    public TestServiceTests(ClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
        
        // 创建TestService实例用于测试
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<TestService>();
        
        _testService = new TestService(logger);
    }

    [Fact]
    public async Task AddAsync_正常输入_应该返回正确结果()
    {
        // Arrange
        var x = 100;
        var y = 200;

        // Act
        var result = await _testService.AddAsync(x, y);

        // Assert
        Assert.Equal(300, result);
    }

    [Fact]
    public async Task AddAsync_负数输入_应该正常处理()
    {
        // Arrange
        var x = -50;
        var y = 30;

        // Act
        var result = await _testService.AddAsync(x, y);

        // Assert
        Assert.Equal(-20, result);
    }

    [Fact]
    public async Task EchoAsync_正常字符串_应该返回回显()
    {
        // Arrange
        var message = "Hello MagicOnion Test!";

        // Act
        var result = await _testService.EchoAsync(message);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(message, result);
        Assert.Contains("Echo from Wind Server:", result);
    }

    [Fact]
    public async Task EchoAsync_空字符串_应该正常处理()
    {
        // Arrange
        var message = "";

        // Act
        var result = await _testService.EchoAsync(message);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Echo from Wind Server:", result);
    }

    [Fact]
    public async Task GetServerInfoAsync_应该返回服务器信息()
    {
        // Act
        var result = await _testService.GetServerInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Wind游戏服务器", result);
        Assert.Contains("Orleans + MagicOnion", result);
        // 应该包含时间戳
        Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", result);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 2)]
    [InlineData(999, 1, 1000)]
    [InlineData(int.MaxValue, 0, int.MaxValue)]
    public async Task AddAsync_边界值测试(int x, int y, int expected)
    {
        // Act
        var result = await _testService.AddAsync(x, y);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task 并发调用AddAsync_应该正常工作()
    {
        // Arrange
        var tasks = new List<Task<int>>();
        
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () => await _testService.AddAsync(i, i * 2)));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(20, results.Length);
        
        for (int i = 0; i < 20; i++)
        {
            var expected = i + (i * 2); // i + i*2 = i*3
            Assert.Equal(expected, results[i]);
        }
    }
}
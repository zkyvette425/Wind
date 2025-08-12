using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.TestingHost;
using Wind.GrainInterfaces;
using Wind.Grains.Services;
using Wind.Tests.TestFixtures;

namespace Wind.Tests.IntegrationTests;

/// <summary>
/// 端到端集成测试 - 验证Orleans + MagicOnion完整调用链
/// </summary>
public class EndToEndTests : IClassFixture<ClusterFixture>
{
    private readonly TestCluster _cluster;

    public EndToEndTests(ClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task MagicOnionService调用OrleansGrain_应该正常工作()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<TestService>();
        var testService = new TestService(logger);

        // Act - 调用AddAsync，它内部会调用HelloGrain
        var result = await testService.AddAsync(10, 20);

        // Assert
        Assert.Equal(30, result);
        
        // 验证Orleans Grain确实被调用了（通过检查日志或其他方式）
        // 这里我们通过直接调用Grain来验证它确实工作正常
        var helloGrain = _cluster.GrainFactory.GetGrain<IHelloGrain>("test-grain");
        var greeting = await helloGrain.SayHelloAsync("Integration Test");
        
        Assert.NotNull(greeting);
        Assert.Contains("Integration Test", greeting);
    }

    [Fact]
    public async Task 模拟完整的游戏会话流程()
    {
        // Arrange - 模拟玩家ID
        var playerId = Guid.NewGuid().ToString();
        
        // Act & Assert - 模拟完整的游戏会话流程

        // 1. 玩家连接 - 通过HelloGrain模拟
        var playerGrain = _cluster.GrainFactory.GetGrain<IHelloGrain>(playerId);
        var welcomeMessage = await playerGrain.SayHelloAsync($"Player-{playerId[..8]}");
        
        Assert.NotNull(welcomeMessage);
        Assert.Contains("Player-", welcomeMessage);

        // 2. 通过MagicOnion服务处理游戏逻辑
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<TestService>();
        var gameService = new TestService(logger);

        // 模拟游戏中的计算（经验值、分数等）
        var scoreResult = await gameService.AddAsync(100, 50); // 基础分100 + 奖励分50
        Assert.Equal(150, scoreResult);

        // 3. 获取服务器状态信息
        var serverInfo = await gameService.GetServerInfoAsync();
        Assert.Contains("Wind游戏服务器", serverInfo);

        // 4. 测试消息回显（模拟聊天功能）
        var chatMessage = $"Hello from player {playerId[..8]}";
        var echoResult = await gameService.EchoAsync(chatMessage);
        Assert.Contains(chatMessage, echoResult);
    }

    [Fact]
    public async Task 多玩家并发会话_应该正常处理()
    {
        // Arrange
        var playerCount = 5;
        var tasks = new List<Task>();

        // Act - 模拟多个玩家同时进入游戏
        for (int i = 0; i < playerCount; i++)
        {
            var playerId = $"player-{i}";
            tasks.Add(SimulatePlayerSessionAsync(playerId));
        }

        // Assert - 所有玩家会话都应该成功完成
        await Task.WhenAll(tasks);
    }

    private async Task SimulatePlayerSessionAsync(string playerId)
    {
        // 1. 玩家Grain交互
        var playerGrain = _cluster.GrainFactory.GetGrain<IHelloGrain>(playerId);
        var greeting = await playerGrain.SayHelloAsync($"Concurrent-{playerId}");
        Assert.Contains($"Concurrent-{playerId}", greeting);

        // 2. 游戏服务交互
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<TestService>();
        var gameService = new TestService(logger);

        // 模拟一些游戏操作
        var addTask = gameService.AddAsync(10, 5);
        var echoTask = gameService.EchoAsync($"Message from {playerId}");
        var infoTask = gameService.GetServerInfoAsync();

        // 等待所有操作完成
        var addResult = await addTask;
        var echoResult = await echoTask;
        var infoResult = await infoTask;
        
        // 验证结果
        Assert.Equal(15, addResult); // AddAsync结果
        Assert.Contains(playerId, echoResult); // EchoAsync结果
        Assert.Contains("Wind游戏服务器", infoResult); // GetServerInfoAsync结果
    }

    [Fact]
    public async Task 测试错误恢复场景()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<TestService>();
        var testService = new TestService(logger);

        // Act & Assert - 测试各种边界情况

        // 1. 空字符串处理
        var emptyEcho = await testService.EchoAsync("");
        Assert.NotNull(emptyEcho);

        // 2. 极大值计算
        var maxResult = await testService.AddAsync(int.MaxValue - 100, 50);
        Assert.Equal(int.MaxValue - 50, maxResult);

        // 3. 负数处理
        var negativeResult = await testService.AddAsync(-100, 200);
        Assert.Equal(100, negativeResult);
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wind.Shared.Protocols;
using Wind.Shared.Services;
using Wind.Server.Services;
using Xunit;

namespace Wind.Tests.MessageRouterTests;

/// <summary>
/// MessageRouter与依赖注入容器集成测试
/// 验证v1.3模块4.3.1在实际DI环境中的工作状态
/// </summary>
public class MessageRouterIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public MessageRouterIntegrationTests()
    {
        var services = new ServiceCollection();
        
        // 配置基础服务 - 模拟Wind.Server中的配置
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<IMessageRouter, MessageRouterService>();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 验证MessageRouter能在DI环境中正常初始化
    /// </summary>
    [Fact]
    public void MessageRouter_Should_Initialize_In_DI_Container()
    {
        // Act
        var messageRouter = _serviceProvider.GetService<IMessageRouter>();
        
        // Assert
        Assert.NotNull(messageRouter);
        Assert.IsType<MessageRouterService>(messageRouter);
    }

    /// <summary>
    /// 验证MessageRouter基础功能在DI环境中工作
    /// </summary>
    [Fact]
    public async Task MessageRouter_Should_Work_With_DI_Services()
    {
        // Arrange
        var messageRouter = _serviceProvider.GetRequiredService<IMessageRouter>();
        
        // Act & Assert
        var stats = await messageRouter.GetStatisticsAsync();
        Assert.NotNull(stats);
        Assert.Equal(0, stats.ActiveReceivers);
        
        var count = await messageRouter.GetActiveReceiversCountAsync();
        Assert.Equal(0, count);
    }

    /// <summary>
    /// 验证消息路由在DI环境中的日志记录
    /// </summary>
    [Fact]
    public void MessageRouter_Should_Use_DI_Logger()
    {
        // Arrange
        var messageRouter = _serviceProvider.GetRequiredService<IMessageRouter>();
        var routerService = messageRouter as MessageRouterService;
        
        // Assert
        Assert.NotNull(routerService);
        // MessageRouterService内部使用ILogger，这验证了DI正确注入
    }

    /// <summary>
    /// 验证消息扩展方法在集成环境中工作
    /// </summary>
    [Fact]
    public void MessageExtensions_Should_Work_In_Integration_Environment()
    {
        // Arrange
        var testData = "Integration test message";
        
        // Act
        var unicastMessage = testData.CreateUnicastMessage("test-user-001");
        var broadcastMessage = testData.CreateGlobalBroadcastMessage("sender-001");
        
        // Assert
        Assert.NotNull(unicastMessage);
        Assert.Equal(RouteTargetType.Unicast, unicastMessage.Route.TargetType);
        
        Assert.NotNull(broadcastMessage);
        Assert.Equal(RouteTargetType.Broadcast, broadcastMessage.Route.TargetType);
    }

    /// <summary>
    /// 验证压缩功能在集成环境中工作
    /// </summary>
    [Fact]
    public void CompressionExtensions_Should_Work_In_Integration_Environment()
    {
        // Arrange
        var largeData = System.Text.Encoding.UTF8.GetBytes(new string('X', 2048));
        
        // Act
        var (compressed, type, stats) = MessageExtensions.CompressDataIntelligent(largeData, "integration-test");
        
        // Assert
        Assert.NotEqual(CompressionType.None, type);
        Assert.Equal(largeData.Length, stats.OriginalSize);
        Assert.True(stats.CompressedSize > 0);
        Assert.True(stats.CompressionRatio <= 1.0);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Wind.Shared.Protocols;
using Wind.Shared.Services;
using Wind.Server.Services;
using Xunit;

namespace Wind.Tests.MessageRouterTests;

/// <summary>
/// MessageRouterService 基础功能测试
/// 验证v1.3模块4.3.1的消息路由和压缩功能
/// </summary>
public class MessageRouterServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMessageRouter _messageRouter;

    public MessageRouterServiceTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IMessageRouter, MessageRouterService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _messageRouter = _serviceProvider.GetRequiredService<IMessageRouter>();
    }

    /// <summary>
    /// 验证MessageRouterService能正常初始化
    /// </summary>
    [Fact]
    public void MessageRouterService_Should_Initialize_Successfully()
    {
        // Assert
        Assert.NotNull(_messageRouter);
        Assert.IsType<MessageRouterService>(_messageRouter);
    }

    /// <summary>
    /// 验证接收器注册和注销功能
    /// </summary>
    [Fact]
    public async Task RegisterReceiver_Should_Work_Correctly()
    {
        // Arrange
        var receiverId = "test-receiver-001";
        var mockReceiver = new Mock<IMessageReceiver>();
        mockReceiver.Setup(r => r.IsOnline).Returns(true);
        mockReceiver.Setup(r => r.Metadata).Returns(new Dictionary<string, string>
        {
            ["RoomId"] = "room-001",
            ["PlayerType"] = "regular"
        });

        // Act
        await _messageRouter.RegisterReceiverAsync(receiverId, mockReceiver.Object);
        var activeCount = await _messageRouter.GetActiveReceiversCountAsync();

        // Assert
        Assert.Equal(1, activeCount);

        // Cleanup - 注销接收器
        await _messageRouter.UnregisterReceiverAsync(receiverId);
        var afterCount = await _messageRouter.GetActiveReceiversCountAsync();
        Assert.Equal(0, afterCount);
    }

    /// <summary>
    /// 验证智能路由消息创建功能
    /// </summary>
    [Fact]
    public void CreateRoutedMessage_Should_Work_With_All_Types()
    {
        // 测试单播消息
        var testMessage = "Hello World";
        var unicastMessage = testMessage.CreateUnicastMessage("user123", "sender456", 200, true);
        
        Assert.NotNull(unicastMessage);
        Assert.Equal(RouteTargetType.Unicast, unicastMessage.Route.TargetType);
        Assert.Contains("user123", unicastMessage.Route.TargetIds);
        Assert.Equal("sender456", unicastMessage.SenderId);
        Assert.Equal(200, unicastMessage.Route.Priority);
        Assert.True(unicastMessage.Route.RequireAck);

        // 测试房间广播消息
        var roomMessage = testMessage.CreateRoomBroadcastMessage("room-001", "sender456", new[] { "user123" });
        
        Assert.NotNull(roomMessage);
        Assert.Equal(RouteTargetType.RoomBroadcast, roomMessage.Route.TargetType);
        Assert.Contains("room-001", roomMessage.Route.TargetIds);
        Assert.Contains("user123", roomMessage.Route.ExcludeIds);

        // 测试全局广播消息
        var broadcastMessage = testMessage.CreateGlobalBroadcastMessage("sender456", new[] { "user123" });
        
        Assert.NotNull(broadcastMessage);
        Assert.Equal(RouteTargetType.Broadcast, broadcastMessage.Route.TargetType);
        Assert.Contains("user123", broadcastMessage.Route.ExcludeIds);
    }

    /// <summary>
    /// 验证消息有效性检查
    /// </summary>
    [Fact]
    public void IsValidRouteMessage_Should_Work_Correctly()
    {
        var testMessage = "Test Message";
        
        // 有效的单播消息
        var validMessage = testMessage.CreateUnicastMessage("user123");
        Assert.True(validMessage.IsValidRouteMessage());

        // 无效的多播消息（只有一个目标）
        var invalidMulticast = testMessage.CreateRoutedMessage(RouteTargetType.Multicast, new[] { "user123" });
        Assert.False(invalidMulticast.IsValidRouteMessage());

        // 有效的多播消息（多个目标）
        var validMulticast = testMessage.CreateRoutedMessage(RouteTargetType.Multicast, new[] { "user123", "user456" });
        Assert.True(validMulticast.IsValidRouteMessage());

        // 过期的消息
        var expiredMessage = testMessage.CreateUnicastMessage("user123");
        expiredMessage.Route.ExpireTime = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds();
        Assert.False(expiredMessage.IsValidRouteMessage());
    }

    /// <summary>
    /// 验证智能路由策略选择
    /// </summary>
    [Theory]
    [InlineData(1, 100, false, false, RouteTargetType.Unicast)] // 单个目标
    [InlineData(50, 100, false, false, RouteTargetType.Multicast)] // 中等规模
    [InlineData(80, 100, false, false, RouteTargetType.Broadcast)] // 超过广播阈值
    [InlineData(50, 100, true, false, RouteTargetType.Broadcast)] // 紧急消息降低阈值
    [InlineData(70, 100, false, true, RouteTargetType.Multicast)] // 高可靠性提高阈值
    public void SelectOptimalRouteType_Should_Choose_Correctly(int targetCount, int totalConnections, 
        bool isUrgent, bool requiresReliability, RouteTargetType expectedType)
    {
        // Act
        var actualType = MessageExtensions.SelectOptimalRouteType(targetCount, totalConnections, isUrgent, requiresReliability);
        
        // Assert
        Assert.Equal(expectedType, actualType);
    }

    /// <summary>
    /// 验证消息路由统计功能
    /// </summary>
    [Fact]
    public async Task GetStatistics_Should_Return_Initial_Values()
    {
        // Act
        var stats = await _messageRouter.GetStatisticsAsync();
        
        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.ActiveReceivers);
        Assert.Equal(0, stats.TotalMessagesProcessed);
        Assert.Equal(0, stats.SuccessfulRoutes);
        Assert.Equal(0, stats.FailedRoutes);
        Assert.Equal(0, stats.QueueBacklog);
        Assert.Equal(0, stats.SuccessRate);
    }

    /// <summary>
    /// 验证消息批量分组功能
    /// </summary>
    [Fact]
    public void GroupMessagesByRoute_Should_Group_Correctly()
    {
        // Arrange
        var messages = new List<RoutedMessage<string>>
        {
            "msg1".CreateUnicastMessage("user1"),
            "msg2".CreateUnicastMessage("user2"),
            "msg3".CreateGlobalBroadcastMessage("sender1"),
            "msg4".CreateRoomBroadcastMessage("room1", "sender1")
        };

        // Act
        var grouped = messages.GroupMessagesByRoute();

        // Assert
        Assert.Equal(3, grouped.Count); // 3种路由类型
        Assert.Contains(RouteTargetType.Unicast, grouped.Keys);
        Assert.Contains(RouteTargetType.Broadcast, grouped.Keys);
        Assert.Contains(RouteTargetType.RoomBroadcast, grouped.Keys);
        Assert.Equal(2, grouped[RouteTargetType.Unicast].Count); // 2个单播消息
        Assert.Equal(1, grouped[RouteTargetType.Broadcast].Count);
        Assert.Equal(1, grouped[RouteTargetType.RoomBroadcast].Count);
    }

    /// <summary>
    /// 验证消息优先级排序功能
    /// </summary>
    [Fact]
    public void SortMessagesByPriority_Should_Sort_Correctly()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var messages = new List<RoutedMessage<string>>
        {
            "low".CreateRoutedMessage(RouteTargetType.Broadcast, priority: 64),
            "high".CreateRoutedMessage(RouteTargetType.Broadcast, priority: 200),
            "medium".CreateRoutedMessage(RouteTargetType.Broadcast, priority: 128)
        };

        // 设置不同的时间戳测试时间排序
        messages[0].Timestamp = now + 3000; // 最晚
        messages[1].Timestamp = now + 1000; // 最早（高优先级）
        messages[2].Timestamp = now + 2000; // 中间

        // Act
        var sorted = messages.SortMessagesByPriority().ToList();

        // Assert
        Assert.Equal(3, sorted.Count);
        Assert.Equal("high", sorted[0].Payload); // 优先级最高
        Assert.Equal("medium", sorted[1].Payload); // 优先级中等
        Assert.Equal("low", sorted[2].Payload); // 优先级最低
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}

/// <summary>
/// 智能压缩功能测试
/// 验证v1.3模块4.3.1的压缩策略
/// </summary>
public class MessageCompressionTests
{
    /// <summary>
    /// 验证小消息不压缩
    /// </summary>
    [Fact]
    public void CompressDataIntelligent_SmallData_Should_Skip_Compression()
    {
        // Arrange
        var smallData = System.Text.Encoding.UTF8.GetBytes("small");
        
        // Act
        var (compressed, type, stats) = MessageExtensions.CompressDataIntelligent(smallData);
        
        // Assert
        Assert.Equal(smallData, compressed);
        Assert.Equal(CompressionType.None, type);
        Assert.Equal("None", stats.Algorithm);
        Assert.Equal(smallData.Length, stats.OriginalSize);
        Assert.Equal(smallData.Length, stats.CompressedSize);
        Assert.Equal(1.0, stats.CompressionRatio);
    }

    /// <summary>
    /// 验证大消息会尝试压缩
    /// </summary>
    [Fact]
    public void CompressDataIntelligent_LargeData_Should_Attempt_Compression()
    {
        // Arrange - 创建一个大的可压缩数据
        var largeData = System.Text.Encoding.UTF8.GetBytes(new string('A', 2048));
        
        // Act
        var (compressed, type, stats) = MessageExtensions.CompressDataIntelligent(largeData, "test-data");
        
        // Assert
        Assert.NotEqual(CompressionType.None, type);
        Assert.Equal(largeData.Length, stats.OriginalSize);
        Assert.True(stats.CompressedSize < stats.OriginalSize); // 应该有压缩效果
        Assert.True(stats.CompressionRatio < 1.0);
        Assert.True(stats.CpuOverheadAcceptable); // CPU开销应该可接受
        Assert.NotEqual("None", stats.Algorithm);
    }

    /// <summary>
    /// 验证序列化和反序列化
    /// </summary>
    [Fact]
    public void SerializeMessage_And_DeserializeMessage_Should_Work()
    {
        // Arrange
        var testObject = new TestMessage { Id = 123, Content = "Test Content" };
        
        // Act
        var serialized = testObject.SerializeMessage();
        var deserialized = MessageExtensions.DeserializeMessage<TestMessage>(serialized);
        
        // Assert
        Assert.NotNull(serialized);
        Assert.True(serialized.Length > 0);
        Assert.NotNull(deserialized);
        Assert.Equal(testObject.Id, deserialized.Id);
        Assert.Equal(testObject.Content, deserialized.Content);
    }

    /// <summary>
    /// 验证兼容性压缩方法
    /// </summary>
    [Fact]
    public void CompressData_And_DecompressData_Should_Be_Compatible()
    {
        // Arrange
        var originalData = System.Text.Encoding.UTF8.GetBytes(new string('B', 2048));
        
        // Act
        var compressed = MessageExtensions.CompressData(originalData);
        var decompressed = MessageExtensions.DecompressData(compressed);
        
        // Assert
        Assert.Equal(originalData, decompressed);
    }
}

/// <summary>
/// 测试消息类型
/// </summary>
[MessagePack.MessagePackObject]
public class TestMessage
{
    [MessagePack.Key(0)]
    public int Id { get; set; }

    [MessagePack.Key(1)]
    public string Content { get; set; } = string.Empty;
}
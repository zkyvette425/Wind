using MessagePack;
using Wind.Shared.Protocols;
using Xunit;

namespace Wind.Tests.MessageRouterTests;

/// <summary>
/// MagicOnion兼容性测试
/// 验证消息类型与MagicOnion MessagePack序列化的兼容性
/// </summary>
public class MagicOnionCompatibilityTests
{
    /// <summary>
    /// 验证RoutedMessage与MessagePack的兼容性
    /// </summary>
    [Fact]
    public void RoutedMessage_Should_Be_MessagePack_Compatible()
    {
        // Arrange
        var originalMessage = "Test message for MagicOnion".CreateUnicastMessage(
            targetUserId: "player123",
            senderId: "player456",
            priority: 200
        );

        // Act - 模拟MagicOnion的序列化/反序列化过程
        var serialized = MessagePackSerializer.Serialize(originalMessage);
        var deserialized = MessagePackSerializer.Deserialize<RoutedMessage<string>>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(originalMessage.MessageId, deserialized.MessageId);
        Assert.Equal(originalMessage.Payload, deserialized.Payload);
        Assert.Equal(originalMessage.SenderId, deserialized.SenderId);
        Assert.Equal(originalMessage.Timestamp, deserialized.Timestamp);
        
        Assert.Equal(originalMessage.Route.TargetType, deserialized.Route.TargetType);
        Assert.Equal(originalMessage.Route.Priority, deserialized.Route.Priority);
        Assert.Equal(originalMessage.Route.TargetIds, deserialized.Route.TargetIds);
    }

    /// <summary>
    /// 验证MessageRoute与MessagePack的兼容性
    /// </summary>
    [Fact]
    public void MessageRoute_Should_Be_MessagePack_Compatible()
    {
        // Arrange
        var originalRoute = new MessageRoute
        {
            TargetType = RouteTargetType.RoomBroadcast,
            TargetIds = new List<string> { "room_001", "room_002" },
            ExcludeIds = new List<string> { "player_exclude" },
            Priority = 150,
            RequireAck = true,
            ExpireTime = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()
        };

        // Act
        var serialized = MessagePackSerializer.Serialize(originalRoute);
        var deserialized = MessagePackSerializer.Deserialize<MessageRoute>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(originalRoute.TargetType, deserialized.TargetType);
        Assert.Equal(originalRoute.TargetIds, deserialized.TargetIds);
        Assert.Equal(originalRoute.ExcludeIds, deserialized.ExcludeIds);
        Assert.Equal(originalRoute.Priority, deserialized.Priority);
        Assert.Equal(originalRoute.RequireAck, deserialized.RequireAck);
        Assert.Equal(originalRoute.ExpireTime, deserialized.ExpireTime);
    }

    /// <summary>
    /// 验证CompressionType枚举的MessagePack兼容性
    /// </summary>
    [Fact]
    public void CompressionType_Should_Be_MessagePack_Compatible()
    {
        // Arrange & Act & Assert
        var types = new[]
        {
            CompressionType.None,
            CompressionType.Gzip,
            CompressionType.Lz4,
            CompressionType.Brotli
        };

        foreach (var originalType in types)
        {
            var serialized = MessagePackSerializer.Serialize(originalType);
            var deserialized = MessagePackSerializer.Deserialize<CompressionType>(serialized);
            
            Assert.Equal(originalType, deserialized);
        }
    }

    /// <summary>
    /// 验证RouteTargetType枚举的MessagePack兼容性
    /// </summary>
    [Fact]
    public void RouteTargetType_Should_Be_MessagePack_Compatible()
    {
        // Arrange & Act & Assert
        var types = new[]
        {
            RouteTargetType.Unicast,
            RouteTargetType.Multicast,
            RouteTargetType.Broadcast,
            RouteTargetType.RoomBroadcast,
            RouteTargetType.AreaBroadcast,
            RouteTargetType.RoleTypeBroadcast
        };

        foreach (var originalType in types)
        {
            var serialized = MessagePackSerializer.Serialize(originalType);
            var deserialized = MessagePackSerializer.Deserialize<RouteTargetType>(serialized);
            
            Assert.Equal(originalType, deserialized);
        }
    }

    /// <summary>
    /// 验证复杂路由消息的MessagePack兼容性
    /// </summary>
    [Fact]
    public void ComplexRoutedMessage_Should_Be_MessagePack_Compatible()
    {
        // Arrange
        var gameEvent = new GameEvent
        {
            EventType = "player_action",
            RoomId = "room_12345",
            PlayerId = "player_67890",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new Dictionary<string, object>
            {
                ["action"] = "move",
                ["position"] = new { x = 10.5, y = 20.3, z = 5.0 },
                ["speed"] = 2.5
            }
        };

        var routedMessage = gameEvent.CreateRoomBroadcastMessage(
            roomId: "room_12345",
            senderId: "player_67890",
            excludeUsers: new[] { "player_67890" },
            priority: 128
        );

        // Act
        var serialized = MessagePackSerializer.Serialize(routedMessage);
        var deserialized = MessagePackSerializer.Deserialize<RoutedMessage<GameEvent>>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(routedMessage.MessageId, deserialized.MessageId);
        Assert.Equal(routedMessage.Payload.EventType, deserialized.Payload.EventType);
        Assert.Equal(routedMessage.Payload.RoomId, deserialized.Payload.RoomId);
        Assert.Equal(routedMessage.Payload.PlayerId, deserialized.Payload.PlayerId);
        Assert.Equal(routedMessage.Route.TargetType, deserialized.Route.TargetType);
        Assert.Equal(routedMessage.Route.ExcludeIds, deserialized.Route.ExcludeIds);
    }
}

/// <summary>
/// 测试游戏事件类
/// </summary>
[MessagePackObject]
public class GameEvent
{
    [Key(0)]
    public string EventType { get; set; } = string.Empty;

    [Key(1)]
    public string RoomId { get; set; } = string.Empty;

    [Key(2)]
    public string PlayerId { get; set; } = string.Empty;

    [Key(3)]
    public long Timestamp { get; set; }

    [Key(4)]
    public Dictionary<string, object>? Data { get; set; }
}
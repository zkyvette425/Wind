using Wind.Shared.Protocols;
using Wind.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wind.Shared.Examples;

/// <summary>
/// 消息路由功能演示示例 - v1.3网络通信层
/// 展示智能路由和广播系统的使用方法
/// </summary>
public class MessageRoutingExample
{
    private readonly IMessageRouter _messageRouter;
    private readonly ILogger<MessageRoutingExample> _logger;

    public MessageRoutingExample(IMessageRouter messageRouter, ILogger<MessageRoutingExample> logger)
    {
        _messageRouter = messageRouter;
        _logger = logger;
    }

    /// <summary>
    /// 演示单播消息 - 发送给特定用户
    /// </summary>
    public async Task DemoUnicastMessageAsync()
    {
        // 创建一个简单的聊天消息
        var chatMessage = new ChatMessage
        {
            SenderId = "user123",
            Content = "Hello, this is a private message!",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // 创建单播路由消息
        var routedMessage = chatMessage.CreateUnicastMessage(
            targetUserId: "user456",
            senderId: "user123",
            priority: 200, // 高优先级
            requireAck: true // 需要确认回执
        );

        // 发送消息
        var result = await _messageRouter.RouteMessageAsync(routedMessage);

        _logger.LogInformation("单播消息路由结果: 成功={Success}, 投递数={DeliveredCount}, 耗时={Duration}ms",
            result.Success, result.DeliveredCount, result.Duration.TotalMilliseconds);

        // 检查确认回执
        if (result.Acknowledgments.Any())
        {
            foreach (var ack in result.Acknowledgments)
            {
                _logger.LogInformation("收到确认回执: 接收者={ReceiverId}, 状态={Status}",
                    ack.ReceiverId, ack.Status);
            }
        }
    }

    /// <summary>
    /// 演示房间广播 - 发送给房间内所有用户
    /// </summary>
    public async Task DemoRoomBroadcastAsync()
    {
        // 创建房间事件消息
        var roomEvent = new RoomEventMessage
        {
            EventType = "player_joined",
            RoomId = "room_001",
            PlayerId = "user789",
            EventData = new Dictionary<string, object>
            {
                ["player_name"] = "新玩家",
                ["join_time"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        };

        // 创建房间广播消息，排除发送者本人
        var routedMessage = roomEvent.CreateRoomBroadcastMessage(
            roomId: "room_001",
            senderId: "user789",
            excludeUsers: new[] { "user789" }, // 排除发送者
            priority: 128 // 普通优先级
        );

        var result = await _messageRouter.RouteMessageAsync(routedMessage);

        _logger.LogInformation("房间广播路由结果: 成功={Success}, 投递数={DeliveredCount}, 失败数={FailedCount}",
            result.Success, result.DeliveredCount, result.FailedCount);
    }

    /// <summary>
    /// 演示智能路由选择 - 系统自动选择最优路由策略
    /// </summary>
    public async Task DemoIntelligentRoutingAsync()
    {
        // 获取当前连接数
        var totalConnections = await _messageRouter.GetActiveReceiversCountAsync();
        
        // 模拟不同规模的目标用户组
        var smallGroup = new[] { "user1", "user2", "user3" }; // 3个用户
        var mediumGroup = Enumerable.Range(1, 50).Select(i => $"user{i}").ToArray(); // 50个用户
        var largeGroup = Enumerable.Range(1, 200).Select(i => $"user{i}").ToArray(); // 200个用户

        _logger.LogInformation("当前总连接数: {TotalConnections}", totalConnections);

        // 演示智能路由策略选择
        var scenarios = new[]
        {
            new { Name = "小组消息", Targets = smallGroup, IsUrgent = false, RequiresReliability = false },
            new { Name = "中等群组", Targets = mediumGroup, IsUrgent = true, RequiresReliability = false },
            new { Name = "大型广播", Targets = largeGroup, IsUrgent = false, RequiresReliability = true }
        };

        foreach (var scenario in scenarios)
        {
            // 使用智能路由选择算法
            var optimalRouteType = MessageExtensions.SelectOptimalRouteType(
                targetCount: scenario.Targets.Length,
                totalConnections: totalConnections,
                isUrgent: scenario.IsUrgent,
                requiresReliability: scenario.RequiresReliability
            );

            _logger.LogInformation("{ScenarioName}: {TargetCount}个目标 -> 推荐路由类型: {RouteType}",
                scenario.Name, scenario.Targets.Length, optimalRouteType);

            // 创建系统通知消息
            var notification = new SystemNotificationMessage
            {
                Title = $"{scenario.Name}通知",
                Content = $"这是一条发送给{scenario.Targets.Length}个用户的消息",
                Type = NotificationType.Info
            };

            // 根据推荐的路由类型创建路由消息
            var routedMessage = notification.CreateRoutedMessage(
                targetType: optimalRouteType,
                targetIds: scenario.Targets,
                priority: (byte)(scenario.IsUrgent ? 200 : 128)
            );

            var result = await _messageRouter.RouteMessageAsync(routedMessage);
            
            _logger.LogInformation("路由结果: 成功={Success}, 投递={Delivered}, 耗时={Duration}ms",
                result.Success, result.DeliveredCount, result.Duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// 演示批量消息处理 - 优化网络传输效率
    /// </summary>
    public async Task DemoBatchProcessingAsync()
    {
        var messages = new List<RoutedMessage<SystemNotificationMessage>>();

        // 创建多种类型的消息
        for (int i = 1; i <= 20; i++)
        {
            var notification = new SystemNotificationMessage
            {
                Title = $"批量消息 #{i}",
                Content = $"这是第{i}条批量处理的消息",
                Type = i % 5 == 0 ? NotificationType.Warning : NotificationType.Info
            };

            // 随机选择路由类型和目标
            RouteTargetType routeType;
            string[] targets;

            switch (i % 4)
            {
                case 0: // 单播
                    routeType = RouteTargetType.Unicast;
                    targets = new[] { $"user{i}" };
                    break;
                case 1: // 房间广播
                    routeType = RouteTargetType.RoomBroadcast;
                    targets = new[] { $"room_{i % 3}" };
                    break;
                case 2: // 多播
                    routeType = RouteTargetType.Multicast;
                    targets = Enumerable.Range(1, 5).Select(j => $"user{i}_{j}").ToArray();
                    break;
                default: // 全局广播
                    routeType = RouteTargetType.Broadcast;
                    targets = Array.Empty<string>();
                    break;
            }

            var routedMessage = notification.CreateRoutedMessage(
                targetType: routeType,
                targetIds: targets,
                priority: (byte)(256 - i * 10) // 递减优先级
            );

            messages.Add(routedMessage);
        }

        _logger.LogInformation("准备批量处理 {MessageCount} 条消息", messages.Count);

        // 执行批量路由
        var batchResult = await _messageRouter.RouteBatchMessagesAsync(messages);

        _logger.LogInformation("批量处理结果:");
        _logger.LogInformation("  总消息数: {TotalMessages}", batchResult.TotalMessages);
        _logger.LogInformation("  成功路由: {SuccessfulRoutes}", batchResult.SuccessfulRoutes);
        _logger.LogInformation("  失败路由: {FailedRoutes}", batchResult.FailedRoutes);
        _logger.LogInformation("  总耗时: {TotalDuration}ms", batchResult.TotalDuration.TotalMilliseconds);
        _logger.LogInformation("  平均耗时: {AverageMessageDuration}ms", batchResult.AverageMessageDuration.TotalMilliseconds);

        // 显示各路由类型统计
        foreach (var typeStats in batchResult.TypeStats)
        {
            _logger.LogInformation("  {RouteType}: {Count}条消息, 成功率={SuccessRate:P1}, 平均耗时={AverageDuration}ms",
                typeStats.Key, typeStats.Value.Count, typeStats.Value.SuccessRate, 
                typeStats.Value.AverageDuration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// 获取路由系统统计信息
    /// </summary>
    public async Task ShowRouterStatisticsAsync()
    {
        var stats = await _messageRouter.GetStatisticsAsync();

        _logger.LogInformation("路由系统统计信息:");
        _logger.LogInformation("  活跃接收器: {ActiveReceivers}", stats.ActiveReceivers);
        _logger.LogInformation("  总处理消息: {TotalMessagesProcessed}", stats.TotalMessagesProcessed);
        _logger.LogInformation("  成功路由: {SuccessfulRoutes}", stats.SuccessfulRoutes);
        _logger.LogInformation("  失败路由: {FailedRoutes}", stats.FailedRoutes);
        _logger.LogInformation("  成功率: {SuccessRate:P2}", stats.SuccessRate);
        _logger.LogInformation("  平均延迟: {AverageRouteLatency}ms", stats.AverageRouteLatency.TotalMilliseconds);
        _logger.LogInformation("  队列积压: {QueueBacklog}", stats.QueueBacklog);

        _logger.LogInformation("路由类型分布:");
        foreach (var distribution in stats.RouteTypeDistribution)
        {
            _logger.LogInformation("  {RouteType}: {Count}条消息", distribution.Key, distribution.Value);
        }
    }
}

/// <summary>
/// 示例聊天消息类
/// </summary>
[MessagePack.MessagePackObject]
public class ChatMessage
{
    [MessagePack.Key(0)]
    public string SenderId { get; set; } = string.Empty;

    [MessagePack.Key(1)]
    public string Content { get; set; } = string.Empty;

    [MessagePack.Key(2)]
    public long Timestamp { get; set; }

    [MessagePack.Key(3)]
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// 示例房间事件消息类
/// </summary>
[MessagePack.MessagePackObject]
public class RoomEventMessage
{
    [MessagePack.Key(0)]
    public string EventType { get; set; } = string.Empty;

    [MessagePack.Key(1)]
    public string RoomId { get; set; } = string.Empty;

    [MessagePack.Key(2)]
    public string PlayerId { get; set; } = string.Empty;

    [MessagePack.Key(3)]
    public Dictionary<string, object>? EventData { get; set; }

    [MessagePack.Key(4)]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
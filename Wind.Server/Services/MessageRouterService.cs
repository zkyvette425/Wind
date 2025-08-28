using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wind.Shared.Protocols;
using Wind.Shared.Services;

namespace Wind.Server.Services;

/// <summary>
/// 消息路由服务实现 - v1.3网络通信层
/// 高性能智能消息路由和广播系统
/// </summary>
public class MessageRouterService : IMessageRouter
{
    private readonly ILogger<MessageRouterService> _logger;
    private readonly ConcurrentDictionary<string, RegisteredReceiver> _receivers = new();
    private readonly ConcurrentDictionary<RouteTargetType, ConcurrentQueue<PendingMessage>> _routeQueues = new();
    private readonly RouterStatistics _statistics = new();
    private readonly Timer _cleanupTimer;
    private readonly object _statsLock = new();

    public MessageRouterService(ILogger<MessageRouterService> logger)
    {
        _logger = logger;

        // 初始化路由队列
        foreach (RouteTargetType routeType in Enum.GetValues<RouteTargetType>())
        {
            _routeQueues[routeType] = new ConcurrentQueue<PendingMessage>();
        }

        // 启动清理定时器 - 每5分钟清理一次过期数据
        _cleanupTimer = new Timer(async _ => await CleanupExpiredDataAsync(TimeSpan.FromHours(1)), 
                                  null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _logger.LogInformation("MessageRouterService 已启动 - 智能路由系统就绪");
    }

    public async Task<RouteResult> RouteMessageAsync<T>(RoutedMessage<T> message, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new RouteResult
        {
            MessageId = message.MessageId
        };

        try
        {
            // 验证消息有效性
            if (!message.IsValidRouteMessage())
            {
                result.Success = false;
                result.Errors.Add("消息验证失败 - 消息无效或已过期");
                _logger.LogWarning("路由消息失败: MessageId={MessageId}, 原因=消息验证失败", message.MessageId);
                return result;
            }

            // 增加跳数计数
            message.Route.CurrentHops++;

            // 根据路由类型选择目标接收器
            var targetReceivers = await SelectTargetReceiversAsync(message);
            
            if (!targetReceivers.Any())
            {
                result.Success = false;
                result.Errors.Add($"未找到符合条件的目标接收器 - 路由类型: {message.Route.TargetType}");
                _logger.LogWarning("未找到目标接收器: MessageId={MessageId}, RouteType={RouteType}", 
                    message.MessageId, message.Route.TargetType);
                return result;
            }

            // 并行投递消息到所有目标
            var deliveryTasks = targetReceivers.Select(async receiver =>
            {
                try
                {
                    var deliveryResult = await receiver.Value.Receiver.ReceiveMessageAsync(message, cancellationToken);
                    
                    if (deliveryResult.Success)
                    {
                        result.DeliveredCount++;
                        
                        // 处理确认回执
                        if (message.Route.RequireAck && deliveryResult.RequiresAck)
                        {
                            result.Acknowledgments.Add(new MessageAckResponse
                            {
                                MessageId = message.MessageId,
                                ReceiverId = receiver.Key,
                                Status = deliveryResult.AckStatus,
                                ProcessedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                            });
                        }
                    }
                    else
                    {
                        result.FailedCount++;
                        result.Errors.Add($"接收器 {receiver.Key} 处理失败: {deliveryResult.ErrorMessage}");
                    }

                    return deliveryResult.Success;
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add($"投递到接收器 {receiver.Key} 时异常: {ex.Message}");
                    _logger.LogError(ex, "消息投递异常: MessageId={MessageId}, ReceiverId={ReceiverId}", 
                        message.MessageId, receiver.Key);
                    return false;
                }
            });

            await Task.WhenAll(deliveryTasks);

            result.Success = result.DeliveredCount > 0;
            result.Duration = stopwatch.Elapsed;

            // 更新统计信息
            UpdateStatistics(message.Route.TargetType, result);

            // 更新消息路由统计
            message.UpdateRouteStatistics(result.Success, result.Duration);

            _logger.LogDebug("消息路由完成: MessageId={MessageId}, 成功={DeliveredCount}, 失败={FailedCount}, 耗时={Duration}ms",
                message.MessageId, result.DeliveredCount, result.FailedCount, result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"路由处理异常: {ex.Message}");
            result.Duration = stopwatch.Elapsed;
            
            _logger.LogError(ex, "消息路由异常: MessageId={MessageId}", message.MessageId);
            return result;
        }
    }

    public async Task<BatchRouteResult> RouteBatchMessagesAsync<T>(IEnumerable<RoutedMessage<T>> messages, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var batchResult = new BatchRouteResult();

        try
        {
            var messageList = messages.ToList();
            batchResult.TotalMessages = messageList.Count;

            if (!messageList.Any())
            {
                _logger.LogWarning("批量路由消息列表为空");
                return batchResult;
            }

            // 按路由类型分组并排序
            var groupedMessages = messageList
                .GroupMessagesByRoute()
                .SelectMany(g => g.Value.SortMessagesByPriority())
                .ToList();

            // 并行处理消息 - 限制并发数避免资源过载
            const int maxConcurrency = 10;
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            var routeTasks = groupedMessages.Select(async message =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var routeResult = await RouteMessageAsync(message, cancellationToken);
                    
                    lock (batchResult)
                    {
                        batchResult.Results.Add(routeResult);
                        
                        if (routeResult.Success)
                            batchResult.SuccessfulRoutes++;
                        else
                            batchResult.FailedRoutes++;

                        // 更新路由类型统计
                        var routeType = message.Route.TargetType;
                        if (!batchResult.TypeStats.ContainsKey(routeType))
                        {
                            batchResult.TypeStats[routeType] = new RouteTypeStats();
                        }

                        var typeStats = batchResult.TypeStats[routeType];
                        typeStats.Count++;
                        
                        if (routeResult.Success)
                            typeStats.SuccessCount++;
                        else
                            typeStats.FailureCount++;

                        // 更新平均耗时 (简化计算)
                        typeStats.AverageDuration = TimeSpan.FromTicks(
                            (typeStats.AverageDuration.Ticks * (typeStats.Count - 1) + routeResult.Duration.Ticks) / typeStats.Count);
                    }

                    return routeResult;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(routeTasks);

            batchResult.TotalDuration = stopwatch.Elapsed;
            batchResult.AverageMessageDuration = batchResult.TotalMessages > 0 
                ? TimeSpan.FromTicks(batchResult.TotalDuration.Ticks / batchResult.TotalMessages)
                : TimeSpan.Zero;

            _logger.LogInformation("批量消息路由完成: 总数={TotalMessages}, 成功={SuccessfulRoutes}, 失败={FailedRoutes}, 耗时={Duration}ms",
                batchResult.TotalMessages, batchResult.SuccessfulRoutes, batchResult.FailedRoutes, 
                batchResult.TotalDuration.TotalMilliseconds);

            return batchResult;
        }
        catch (Exception ex)
        {
            batchResult.TotalDuration = stopwatch.Elapsed;
            _logger.LogError(ex, "批量消息路由异常");
            throw;
        }
    }

    public async Task RegisterReceiverAsync(string receiverId, IMessageReceiver receiver, Dictionary<string, string>? metadata = null)
    {
        await Task.CompletedTask; // 保持接口异步

        var registeredReceiver = new RegisteredReceiver
        {
            Receiver = receiver,
            Metadata = metadata ?? new Dictionary<string, string>(),
            RegisterTime = DateTimeOffset.UtcNow
        };

        _receivers.AddOrUpdate(receiverId, registeredReceiver, (_, _) => registeredReceiver);

        _logger.LogInformation("消息接收器已注册: ReceiverId={ReceiverId}, Metadata={Metadata}", 
            receiverId, string.Join(",", registeredReceiver.Metadata.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    public async Task UnregisterReceiverAsync(string receiverId)
    {
        await Task.CompletedTask; // 保持接口异步

        if (_receivers.TryRemove(receiverId, out var receiver))
        {
            _logger.LogInformation("消息接收器已注销: ReceiverId={ReceiverId}", receiverId);
        }
        else
        {
            _logger.LogWarning("尝试注销不存在的接收器: ReceiverId={ReceiverId}", receiverId);
        }
    }

    public async Task<int> GetActiveReceiversCountAsync(Dictionary<string, string>? filterMetadata = null)
    {
        await Task.CompletedTask; // 保持接口异步

        if (filterMetadata == null || !filterMetadata.Any())
        {
            return _receivers.Values.Count(r => r.Receiver.IsOnline);
        }

        return _receivers.Values.Count(r => 
            r.Receiver.IsOnline && 
            filterMetadata.All(filter => 
                r.Metadata.TryGetValue(filter.Key, out var value) && value == filter.Value));
    }

    public async Task<RouterStatistics> GetStatisticsAsync()
    {
        await Task.CompletedTask; // 保持接口异步

        lock (_statsLock)
        {
            var stats = new RouterStatistics
            {
                ActiveReceivers = _receivers.Values.Count(r => r.Receiver.IsOnline),
                TotalMessagesProcessed = _statistics.TotalMessagesProcessed,
                SuccessfulRoutes = _statistics.SuccessfulRoutes,
                FailedRoutes = _statistics.FailedRoutes,
                AverageRouteLatency = _statistics.AverageRouteLatency,
                QueueBacklog = _routeQueues.Values.Sum(q => q.Count),
                RouteTypeDistribution = new Dictionary<RouteTargetType, long>(_statistics.RouteTypeDistribution),
                LastUpdated = DateTimeOffset.UtcNow
            };

            return stats;
        }
    }

    public async Task CleanupExpiredDataAsync(TimeSpan maxAge)
    {
        await Task.Run(() =>
        {
            try
            {
                var cutoffTime = DateTimeOffset.UtcNow.Subtract(maxAge);
                int removedReceiversCount = 0;

                // 清理长时间离线的接收器
                var expiredReceivers = _receivers
                    .Where(kvp => !kvp.Value.Receiver.IsOnline && kvp.Value.RegisterTime < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var receiverId in expiredReceivers)
                {
                    if (_receivers.TryRemove(receiverId, out _))
                    {
                        removedReceiversCount++;
                    }
                }

                // 清理路由统计缓存
                MessageExtensions.CleanupExpiredRouteStats(maxAge);

                if (removedReceiversCount > 0)
                {
                    _logger.LogInformation("清理过期数据完成: 移除离线接收器={RemovedCount}", removedReceiversCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期数据时发生异常");
            }
        });
    }

    /// <summary>
    /// 根据路由信息选择目标接收器
    /// </summary>
    private async Task<Dictionary<string, RegisteredReceiver>> SelectTargetReceiversAsync<T>(RoutedMessage<T> message)
    {
        await Task.CompletedTask; // 保持方法异步

        var targetReceivers = new Dictionary<string, RegisteredReceiver>();
        var activeReceivers = _receivers.Where(kvp => kvp.Value.Receiver.IsOnline).ToList();

        switch (message.Route.TargetType)
        {
            case RouteTargetType.Unicast:
                // 单播 - 精确匹配目标ID
                foreach (var targetId in message.Route.TargetIds)
                {
                    if (activeReceivers.FirstOrDefault(r => r.Key == targetId) is var target && target.Key != null)
                    {
                        targetReceivers[target.Key] = target.Value;
                    }
                }
                break;

            case RouteTargetType.Multicast:
                // 多播 - 匹配所有指定目标
                foreach (var targetId in message.Route.TargetIds)
                {
                    if (activeReceivers.FirstOrDefault(r => r.Key == targetId) is var target && target.Key != null)
                    {
                        targetReceivers[target.Key] = target.Value;
                    }
                }
                break;

            case RouteTargetType.Broadcast:
                // 全局广播 - 所有在线接收器
                foreach (var receiver in activeReceivers)
                {
                    if (!message.Route.ExcludeIds.Contains(receiver.Key))
                    {
                        targetReceivers[receiver.Key] = receiver.Value;
                    }
                }
                break;

            case RouteTargetType.RoomBroadcast:
                // 房间广播 - 匹配房间ID的接收器
                foreach (var roomId in message.Route.TargetIds)
                {
                    var roomReceivers = activeReceivers.Where(r => 
                        r.Value.Metadata.TryGetValue("RoomId", out var value) && value == roomId &&
                        !message.Route.ExcludeIds.Contains(r.Key));
                        
                    foreach (var receiver in roomReceivers)
                    {
                        targetReceivers[receiver.Key] = receiver.Value;
                    }
                }
                break;

            case RouteTargetType.AreaBroadcast:
                // 区域广播 - 匹配区域ID的接收器
                foreach (var areaId in message.Route.TargetIds)
                {
                    var areaReceivers = activeReceivers.Where(r => 
                        r.Value.Metadata.TryGetValue("AreaId", out var value) && value == areaId &&
                        !message.Route.ExcludeIds.Contains(r.Key));
                        
                    foreach (var receiver in areaReceivers)
                    {
                        targetReceivers[receiver.Key] = receiver.Value;
                    }
                }
                break;

            case RouteTargetType.RoleTypeBroadcast:
                // 角色类型广播 - 匹配角色类型的接收器
                foreach (var roleType in message.Route.TargetIds)
                {
                    var roleReceivers = activeReceivers.Where(r => 
                        r.Value.Metadata.TryGetValue("RoleType", out var value) && value == roleType &&
                        !message.Route.ExcludeIds.Contains(r.Key));
                        
                    foreach (var receiver in roleReceivers)
                    {
                        targetReceivers[receiver.Key] = receiver.Value;
                    }
                }
                break;
        }

        return targetReceivers;
    }

    /// <summary>
    /// 更新统计信息
    /// </summary>
    private void UpdateStatistics(RouteTargetType routeType, RouteResult result)
    {
        lock (_statsLock)
        {
            _statistics.TotalMessagesProcessed++;
            
            if (result.Success)
                _statistics.SuccessfulRoutes++;
            else
                _statistics.FailedRoutes++;

            // 更新路由类型分布
            if (!_statistics.RouteTypeDistribution.ContainsKey(routeType))
                _statistics.RouteTypeDistribution[routeType] = 0;
            _statistics.RouteTypeDistribution[routeType]++;

            // 更新平均延迟 (指数移动平均)
            const double alpha = 0.1;
            _statistics.AverageRouteLatency = TimeSpan.FromTicks(
                (long)(_statistics.AverageRouteLatency.Ticks * (1 - alpha) + result.Duration.Ticks * alpha));
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _logger.LogInformation("MessageRouterService 已停止");
    }
}

/// <summary>
/// 注册的接收器信息
/// </summary>
internal class RegisteredReceiver
{
    public IMessageReceiver Receiver { get; set; } = default!;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTimeOffset RegisterTime { get; set; }
}

/// <summary>
/// 等待处理的消息
/// </summary>
internal class PendingMessage
{
    public object Message { get; set; } = default!;
    public DateTimeOffset QueueTime { get; set; }
    public int Priority { get; set; }
}
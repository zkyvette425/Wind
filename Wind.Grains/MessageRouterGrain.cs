using Microsoft.Extensions.Logging;
using Orleans;
using System.Collections.Concurrent;
using System.Diagnostics;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;

namespace Wind.Grains
{
    /// <summary>
    /// 消息路由Grain实现
    /// 负责消息的分发、队列管理和可靠投递
    /// 支持点对点、组播、广播等多种消息传递模式
    /// </summary>
    public class MessageRouterGrain : Grain, IMessageRouterGrain
    {
        private readonly ILogger<MessageRouterGrain> _logger;
        
        // 状态管理 (内存状态，与PlayerGrain保持一致)
        private MessageRouterState _state = new();
        
        // 消息队列：订阅者ID -> 消息队列
        private readonly ConcurrentDictionary<string, Queue<QueuedMessage>> _messageQueues = new();
        
        // 订阅信息：订阅者ID -> 订阅者信息
        private readonly ConcurrentDictionary<string, SubscriberInfo> _subscribers = new();
        
        // 失败消息队列：订阅者ID -> 失败消息列表
        private readonly ConcurrentDictionary<string, List<QueuedMessage>> _failedMessages = new();
        
        // 消息历史记录
        private readonly Queue<BaseMessage> _messageHistory = new();
        
        // 配置信息
        private MessageRouterConfig _config = new();
        
        // 统计信息
        private MessageRouterStats _stats = new();
        
        // 定时器
        private IDisposable? _cleanupTimer;
        private IDisposable? _deliveryTimer;

        public MessageRouterGrain(ILogger<MessageRouterGrain> logger)
        {
            _logger = logger;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            // 初始化状态数据
            if (_state.Configuration != null)
            {
                _config = _state.Configuration;
                _stats = _state.Statistics ?? new MessageRouterStats();
                
                // 恢复订阅者信息  
                foreach (var subscriber in _state.Subscribers ?? new())
                {
                    _subscribers[subscriber.Key] = subscriber.Value;
                    _messageQueues[subscriber.Key] = new Queue<QueuedMessage>();
                    _failedMessages[subscriber.Key] = new List<QueuedMessage>();
                }
            }

            // 启动定时器
            _cleanupTimer = RegisterTimer(
                CleanupExpiredMessagesTimerCallback,
                null,
                TimeSpan.FromMilliseconds(_config.CleanupIntervalMs),
                TimeSpan.FromMilliseconds(_config.CleanupIntervalMs));

            _deliveryTimer = RegisterTimer(
                ProcessMessageQueuesTimerCallback,
                null,
                TimeSpan.FromMilliseconds(100), // 快速处理
                TimeSpan.FromMilliseconds(100));

            _logger.LogInformation("消息路由器 {GrainId} 已激活，配置: {Config}", 
                this.GetPrimaryKeyString(), _config);

            await base.OnActivateAsync(cancellationToken);
        }

        public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            // 更新内存状态
            _state.Configuration = _config;
            _state.Statistics = _stats;
            _state.Subscribers = new Dictionary<string, SubscriberInfo>(_subscribers);
            _state.LastSavedAt = DateTime.UtcNow;

            // 清理定时器
            _cleanupTimer?.Dispose();
            _deliveryTimer?.Dispose();

            _logger.LogInformation("消息路由器 {GrainId} 已停用，原因: {Reason}", 
                this.GetPrimaryKeyString(), reason);

            await base.OnDeactivateAsync(reason, cancellationToken);
        }

        // ======== 核心消息路由功能 ========

        public async Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                _logger.LogDebug("发送消息: {MessageId}, 类型: {MessageType}, 发送者: {SenderId}", 
                    request.Message.MessageId, request.Message.Type, request.Message.SenderId);

                // 验证请求
                var validationResult = ValidateMessage(request.Message);
                if (!validationResult.IsValid)
                {
                    return CreateFailureResponse(request.Message.MessageId, validationResult.ErrorMessage);
                }

                // 确定目标接收者
                var targetSubscribers = await DetermineTargetSubscribersAsync(request.Message);
                if (!targetSubscribers.Any())
                {
                    return CreateFailureResponse(request.Message.MessageId, "没有找到有效的接收者");
                }

                // 创建队列消息
                var queuedMessage = new QueuedMessage
                {
                    Message = request.Message,
                    CreatedAt = DateTime.UtcNow,
                    RetryCount = 0,
                    Status = MessageStatus.Pending
                };

                // 分发到目标订阅者的队列
                var deliveredCount = 0;
                var failedDeliveries = new List<string>();

                foreach (var subscriberId in targetSubscribers)
                {
                    try
                    {
                        await QueueMessageForSubscriberAsync(subscriberId, queuedMessage);
                        deliveredCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "消息队列失败: {SubscriberId}, 消息: {MessageId}", 
                            subscriberId, request.Message.MessageId);
                        failedDeliveries.Add(subscriberId);
                    }
                }

                // 更新统计信息
                _stats.TotalMessagesSent++;
                _stats.MessageCountByType.TryGetValue(request.Message.Type, out var count);
                _stats.MessageCountByType[request.Message.Type] = count + 1;
                _stats.MessageCountByPriority.TryGetValue(request.Message.Priority, out var priorityCount);
                _stats.MessageCountByPriority[request.Message.Priority] = priorityCount + 1;

                // 添加到历史记录
                AddToHistory(request.Message);

                stopwatch.Stop();
                _stats.AverageDeliveryTime = (_stats.AverageDeliveryTime + stopwatch.ElapsedMilliseconds) / 2.0;

                return new SendMessageResponse
                {
                    Success = deliveredCount > 0,
                    MessageId = request.Message.MessageId,
                    DeliveredCount = deliveredCount,
                    FailedCount = failedDeliveries.Count,
                    FailedTargets = failedDeliveries,
                    DeliveryTime = stopwatch.ElapsedMilliseconds,
                    Message = deliveredCount == targetSubscribers.Count ? "消息发送成功" : 
                             $"部分发送成功: {deliveredCount}/{targetSubscribers.Count}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送消息失败: {MessageId}", request.Message.MessageId);
                return CreateFailureResponse(request.Message.MessageId, $"发送失败: {ex.Message}");
            }
        }

        public async Task<BatchSendMessageResponse> SendBatchMessagesAsync(BatchSendMessageRequest request)
        {
            var response = new BatchSendMessageResponse
            {
                Success = true,
                TotalMessages = request.Messages.Count,
                Results = new List<SendMessageResponse>()
            };

            var tasks = request.Messages.Select(async message =>
            {
                var sendRequest = new SendMessageRequest { Message = message };
                return await SendMessageAsync(sendRequest);
            });

            var results = await Task.WhenAll(tasks);
            response.Results.AddRange(results);

            response.SuccessCount = results.Count(r => r.Success);
            response.FailureCount = results.Count(r => !r.Success);
            response.Success = response.SuccessCount > 0;

            return response;
        }

        public async Task<SubscribeMessageResponse> SubscribeAsync(SubscribeMessageRequest request)
        {
            try
            {
                var subscriberId = request.SubscriberId;
                var subscriptionId = request.SubscriptionId ?? Guid.NewGuid().ToString();

                // 检查是否已存在订阅
                if (_subscribers.ContainsKey(subscriberId))
                {
                    _logger.LogWarning("订阅者 {SubscriberId} 已存在，将更新订阅信息", subscriberId);
                }

                // 创建或更新订阅者信息
                var subscriberInfo = new SubscriberInfo
                {
                    SubscriberId = subscriberId,
                    SubscriptionId = subscriptionId,
                    Filter = request.Filter ?? new MessageFilter(),
                    SubscribedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow,
                    IsActive = true,
                    DeliveryPaused = false
                };

                _subscribers[subscriberId] = subscriberInfo;
                
                // 确保队列存在
                if (!_messageQueues.ContainsKey(subscriberId))
                {
                    _messageQueues[subscriberId] = new Queue<QueuedMessage>();
                }
                
                if (!_failedMessages.ContainsKey(subscriberId))
                {
                    _failedMessages[subscriberId] = new List<QueuedMessage>();
                }

                _stats.ActiveSubscribers = _subscribers.Count;

                _logger.LogInformation("订阅者 {SubscriberId} 订阅成功，过滤器: {Filter}", 
                    subscriberId, request.Filter);

                return new SubscribeMessageResponse
                {
                    Success = true,
                    SubscriberId = subscriberId,
                    SubscriptionId = subscriptionId,
                    Message = "订阅成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订阅失败: {SubscriberId}", request.SubscriberId);
                return new SubscribeMessageResponse
                {
                    Success = false,
                    SubscriberId = request.SubscriberId,
                    Message = $"订阅失败: {ex.Message}"
                };
            }
        }

        public async Task<UnsubscribeMessageResponse> UnsubscribeAsync(UnsubscribeMessageRequest request)
        {
            try
            {
                var subscriberId = request.SubscriberId;

                if (!_subscribers.TryRemove(subscriberId, out var subscriberInfo))
                {
                    return new UnsubscribeMessageResponse
                    {
                        Success = false,
                        SubscriberId = subscriberId,
                        Message = "订阅者不存在"
                    };
                }

                // 清理相关队列
                _messageQueues.TryRemove(subscriberId, out _);
                _failedMessages.TryRemove(subscriberId, out _);

                _stats.ActiveSubscribers = _subscribers.Count;

                _logger.LogInformation("订阅者 {SubscriberId} 取消订阅成功", subscriberId);

                return new UnsubscribeMessageResponse
                {
                    Success = true,
                    SubscriberId = subscriberId,
                    Message = "取消订阅成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消订阅失败: {SubscriberId}", request.SubscriberId);
                return new UnsubscribeMessageResponse
                {
                    Success = false,
                    SubscriberId = request.SubscriberId,
                    Message = $"取消订阅失败: {ex.Message}"
                };
            }
        }

        public async Task<MessageAcknowledgmentResponse> AcknowledgeMessageAsync(MessageAcknowledgmentRequest request)
        {
            try
            {
                var subscriberId = request.SubscriberId;
                var messageId = request.MessageId;

                if (!_subscribers.ContainsKey(subscriberId))
                {
                    return new MessageAcknowledgmentResponse
                    {
                        Success = false,
                        MessageId = messageId,
                        Message = "订阅者不存在"
                    };
                }

                // 更新订阅者活动时间
                if (_subscribers.TryGetValue(subscriberId, out var subscriber))
                {
                    subscriber.LastActivityAt = DateTime.UtcNow;
                    subscriber.TotalMessagesReceived++;
                }

                _stats.TotalMessagesDelivered++;

                _logger.LogDebug("消息确认: {MessageId}, 订阅者: {SubscriberId}", messageId, subscriberId);

                return new MessageAcknowledgmentResponse
                {
                    Success = true,
                    MessageId = messageId,
                    SubscriberId = subscriberId,
                    Message = "确认成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "消息确认失败: {MessageId}, 订阅者: {SubscriberId}", 
                    request.MessageId, request.SubscriberId);
                return new MessageAcknowledgmentResponse
                {
                    Success = false,
                    MessageId = request.MessageId,
                    Message = $"确认失败: {ex.Message}"
                };
            }
        }

        // ======== 消息查询和历史记录 ========

        public async Task<GetMessageHistoryResponse> GetMessageHistoryAsync(GetMessageHistoryRequest request)
        {
            await Task.CompletedTask;
            
            var messages = _messageHistory
                .Where(m => request.MessageTypes == null || !request.MessageTypes.Any() || 
                           request.MessageTypes.Contains(m.Type))
                .Where(m => request.StartTime == null || m.Timestamp >= request.StartTime)
                .Where(m => request.EndTime == null || m.Timestamp <= request.EndTime)
                .OrderByDescending(m => m.Timestamp)
                .Take(request.Limit)
                .ToList();

            return new GetMessageHistoryResponse
            {
                Success = true,
                Messages = messages,
                TotalCount = messages.Count
            };
        }

        public async Task<int> GetPendingMessageCountAsync(string subscriberId)
        {
            await Task.CompletedTask;
            
            if (_messageQueues.TryGetValue(subscriberId, out var queue))
            {
                return queue.Count;
            }
            
            return 0;
        }

        public async Task<List<BaseMessage>> GetFailedMessagesAsync(string subscriberId, int limit = 50)
        {
            await Task.CompletedTask;
            
            if (_failedMessages.TryGetValue(subscriberId, out var failedList))
            {
                return failedList
                    .OrderByDescending(qm => qm.CreatedAt)
                    .Take(limit)
                    .Select(qm => qm.Message)
                    .ToList();
            }
            
            return new List<BaseMessage>();
        }

        // ======== 队列管理功能 ========

        public async Task<int> ClearQueueAsync(string subscriberId)
        {
            await Task.CompletedTask;
            
            var clearedCount = 0;
            
            if (_messageQueues.TryGetValue(subscriberId, out var queue))
            {
                clearedCount = queue.Count;
                queue.Clear();
            }
            
            if (_failedMessages.TryGetValue(subscriberId, out var failedList))
            {
                clearedCount += failedList.Count;
                failedList.Clear();
            }

            _logger.LogInformation("清空订阅者 {SubscriberId} 队列，清理 {Count} 条消息", 
                subscriberId, clearedCount);
            
            return clearedCount;
        }

        public async Task<bool> PauseDeliveryAsync(string subscriberId)
        {
            await Task.CompletedTask;
            
            if (_subscribers.TryGetValue(subscriberId, out var subscriber))
            {
                subscriber.DeliveryPaused = true;
                _logger.LogInformation("暂停订阅者 {SubscriberId} 的消息投递", subscriberId);
                return true;
            }
            
            return false;
        }

        public async Task<bool> ResumeDeliveryAsync(string subscriberId)
        {
            await Task.CompletedTask;
            
            if (_subscribers.TryGetValue(subscriberId, out var subscriber))
            {
                subscriber.DeliveryPaused = false;
                _logger.LogInformation("恢复订阅者 {SubscriberId} 的消息投递", subscriberId);
                return true;
            }
            
            return false;
        }

        public async Task<SendMessageResponse> RetryFailedMessageAsync(string messageId)
        {
            try
            {
                // 查找失败的消息
                QueuedMessage? failedMessage = null;
                string? targetSubscriberId = null;

                foreach (var kvp in _failedMessages)
                {
                    var message = kvp.Value.FirstOrDefault(m => m.Message.MessageId == messageId);
                    if (message != null)
                    {
                        failedMessage = message;
                        targetSubscriberId = kvp.Key;
                        break;
                    }
                }

                if (failedMessage == null || targetSubscriberId == null)
                {
                    return CreateFailureResponse(messageId, "未找到失败的消息");
                }

                // 重试发送
                var retryRequest = new SendMessageRequest { Message = failedMessage.Message };
                var result = await SendMessageAsync(retryRequest);

                if (result.Success)
                {
                    // 从失败队列中移除
                    _failedMessages[targetSubscriberId].Remove(failedMessage);
                    _stats.TotalMessagesRetried++;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重试失败消息 {MessageId} 时发生错误", messageId);
                return CreateFailureResponse(messageId, $"重试失败: {ex.Message}");
            }
        }

        // ======== 监控和统计功能 ========

        public async Task<MessageRouterStats> GetStatsAsync()
        {
            await Task.CompletedTask;
            
            // 更新实时统计
            _stats.ActiveSubscribers = _subscribers.Count;
            _stats.PendingMessages = _messageQueues.Values.Sum(q => q.Count);
            
            return _stats;
        }

        public async Task<List<string>> GetActiveSubscribersAsync()
        {
            await Task.CompletedTask;
            return _subscribers.Values
                .Where(s => s.IsActive)
                .Select(s => s.SubscriberId)
                .ToList();
        }

        public async Task<SubscriberInfo?> GetSubscriberInfoAsync(string subscriberId)
        {
            await Task.CompletedTask;
            
            if (_subscribers.TryGetValue(subscriberId, out var subscriber))
            {
                // 更新实时统计
                subscriber.PendingMessageCount = _messageQueues.TryGetValue(subscriberId, out var queue) ? queue.Count : 0;
                subscriber.FailedMessageCount = _failedMessages.TryGetValue(subscriberId, out var failed) ? failed.Count : 0;
                return subscriber;
            }
            
            return null;
        }

        // ======== 系统管理功能 ========

        public async Task<int> CleanupExpiredMessagesAsync()
        {
            var cleanedCount = 0;
            var cutoffTime = DateTime.UtcNow.AddMilliseconds(-_config.MessageTimeoutMs);

            // 清理过期的队列消息
            foreach (var kvp in _messageQueues)
            {
                var queue = kvp.Value;
                var expiredMessages = new List<QueuedMessage>();
                
                // 收集过期消息
                foreach (var message in queue)
                {
                    if (message.CreatedAt < cutoffTime || 
                        (message.Message.ExpiresAt.HasValue && message.Message.ExpiresAt < DateTime.UtcNow))
                    {
                        expiredMessages.Add(message);
                    }
                }
                
                // 移除过期消息
                foreach (var expired in expiredMessages)
                {
                    var tempQueue = new Queue<QueuedMessage>();
                    while (queue.Count > 0)
                    {
                        var msg = queue.Dequeue();
                        if (msg != expired)
                        {
                            tempQueue.Enqueue(msg);
                        }
                        else
                        {
                            cleanedCount++;
                        }
                    }
                    
                    // 重新构建队列
                    while (tempQueue.Count > 0)
                    {
                        queue.Enqueue(tempQueue.Dequeue());
                    }
                }
            }

            // 清理历史记录
            var historyLimit = _config.MaxHistorySize;
            while (_messageHistory.Count > historyLimit)
            {
                _messageHistory.Dequeue();
                cleanedCount++;
            }

            if (cleanedCount > 0)
            {
                _logger.LogInformation("清理过期消息完成，清理 {Count} 条消息", cleanedCount);
            }

            return cleanedCount;
        }

        public async Task<bool> SetConfigurationAsync(MessageRouterConfig config)
        {
            try
            {
                _config = config ?? throw new ArgumentNullException(nameof(config));
                
                // 更新内存状态
                _state.Configuration = _config;

                _logger.LogInformation("消息路由器配置已更新: {Config}", config);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置配置失败");
                return false;
            }
        }

        public async Task<MessageRouterConfig> GetConfigurationAsync()
        {
            await Task.CompletedTask;
            return _config;
        }

        public async Task<MessageRouterHealthStatus> GetHealthStatusAsync()
        {
            await Task.CompletedTask;
            
            var issues = new List<string>();
            var isHealthy = true;

            // 检查队列大小
            var totalPendingMessages = _messageQueues.Values.Sum(q => q.Count);
            if (totalPendingMessages > _config.MaxQueueSize * 0.8)
            {
                issues.Add($"队列消息过多: {totalPendingMessages}/{_config.MaxQueueSize}");
                isHealthy = false;
            }

            // 检查失败消息
            var totalFailedMessages = _failedMessages.Values.Sum(f => f.Count);
            if (totalFailedMessages > 100)
            {
                issues.Add($"失败消息过多: {totalFailedMessages}");
                isHealthy = false;
            }

            // 检查订阅者活动
            var inactiveSubscribers = _subscribers.Values.Count(s => 
                DateTime.UtcNow - s.LastActivityAt > TimeSpan.FromMinutes(10));
            if (inactiveSubscribers > _subscribers.Count * 0.5)
            {
                issues.Add($"不活跃订阅者过多: {inactiveSubscribers}/{_subscribers.Count}");
            }

            var metrics = new Dictionary<string, object>
            {
                ["TotalSubscribers"] = _subscribers.Count,
                ["PendingMessages"] = totalPendingMessages,
                ["FailedMessages"] = totalFailedMessages,
                ["MessagesSentPerSecond"] = _stats.MessageProcessingRate
            };

            return new MessageRouterHealthStatus
            {
                IsHealthy = isHealthy,
                Status = isHealthy ? "Healthy" : "Unhealthy",
                CheckTime = DateTime.UtcNow,
                Issues = issues,
                Metrics = metrics,
                ActiveConnections = _subscribers.Count,
                MessageProcessingRate = _stats.MessageProcessingRate
            };
        }

        // ======== 私有辅助方法 ========

        private (bool IsValid, string ErrorMessage) ValidateMessage(BaseMessage message)
        {
            if (string.IsNullOrEmpty(message.MessageId))
                return (false, "消息ID不能为空");
                
            if (string.IsNullOrEmpty(message.SenderId))
                return (false, "发送者ID不能为空");
                
            if (message.ExpiresAt.HasValue && message.ExpiresAt < DateTime.UtcNow)
                return (false, "消息已过期");
                
            return (true, string.Empty);
        }

        private async Task<List<string>> DetermineTargetSubscribersAsync(BaseMessage message)
        {
            var targets = new List<string>();

            switch (message.DeliveryMode)
            {
                case MessageDeliveryMode.Unicast:
                    // 直接指定的目标
                    if (message.TargetIds.Any())
                    {
                        targets.AddRange(message.TargetIds.Where(id => _subscribers.ContainsKey(id)));
                    }
                    break;

                case MessageDeliveryMode.Multicast:
                    // 组播：根据房间ID或目标列表
                    if (!string.IsNullOrEmpty(message.RoomId))
                    {
                        // 获取房间内的所有订阅者（这里需要与RoomGrain协调）
                        targets.AddRange(await GetRoomSubscribersAsync(message.RoomId));
                    }
                    else if (message.TargetIds.Any())
                    {
                        targets.AddRange(message.TargetIds.Where(id => _subscribers.ContainsKey(id)));
                    }
                    break;

                case MessageDeliveryMode.Broadcast:
                    // 房间广播
                    if (!string.IsNullOrEmpty(message.RoomId))
                    {
                        targets.AddRange(await GetRoomSubscribersAsync(message.RoomId));
                    }
                    break;

                case MessageDeliveryMode.GlobalBroadcast:
                    // 全局广播：所有活跃订阅者
                    targets.AddRange(_subscribers.Keys);
                    break;
            }

            // 应用消息过滤器
            return targets.Where(subscriberId => 
            {
                if (_subscribers.TryGetValue(subscriberId, out var subscriber))
                {
                    return ApplyMessageFilter(message, subscriber.Filter);
                }
                return false;
            }).ToList();
        }

        private async Task<List<string>> GetRoomSubscribersAsync(string roomId)
        {
            // TODO: 这里需要与RoomGrain协调，获取房间内的玩家列表
            // 目前返回一个空列表作为占位符
            await Task.CompletedTask;
            return new List<string>();
        }

        private bool ApplyMessageFilter(BaseMessage message, MessageFilter filter)
        {
            // 检查消息类型过滤
            if (filter.AllowedMessageTypes?.Any() == true && 
                !filter.AllowedMessageTypes.Contains(message.Type))
            {
                return false;
            }

            // 检查发送者过滤
            if (filter.BlockedSenders?.Contains(message.SenderId) == true)
            {
                return false;
            }

            // 检查优先级过滤
            if (filter.MinimumPriority.HasValue && 
                message.Priority < filter.MinimumPriority.Value)
            {
                return false;
            }

            return true;
        }

        private async Task QueueMessageForSubscriberAsync(string subscriberId, QueuedMessage message)
        {
            if (!_messageQueues.TryGetValue(subscriberId, out var queue))
            {
                queue = new Queue<QueuedMessage>();
                _messageQueues[subscriberId] = queue;
            }

            // 检查队列大小限制
            if (queue.Count >= _config.MaxQueueSize)
            {
                // 移除最旧的消息
                queue.Dequeue();
                _logger.LogWarning("订阅者 {SubscriberId} 队列已满，移除最旧消息", subscriberId);
            }

            queue.Enqueue(message);
            await Task.CompletedTask;
        }

        private void AddToHistory(BaseMessage message)
        {
            _messageHistory.Enqueue(message);
            
            // 限制历史记录大小
            while (_messageHistory.Count > _config.MaxHistorySize)
            {
                _messageHistory.Dequeue();
            }
        }

        private SendMessageResponse CreateFailureResponse(string messageId, string errorMessage)
        {
            return new SendMessageResponse
            {
                Success = false,
                MessageId = messageId,
                Message = errorMessage,
                DeliveredCount = 0,
                FailedCount = 1
            };
        }

        // ======== 定时器回调 ========

        private async Task CleanupExpiredMessagesTimerCallback(object state)
        {
            try
            {
                await CleanupExpiredMessagesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "定时清理过期消息失败");
            }
        }

        private async Task ProcessMessageQueuesTimerCallback(object state)
        {
            try
            {
                await ProcessPendingMessagesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理待发送消息失败");
            }
        }

        private async Task ProcessPendingMessagesAsync()
        {
            // 处理所有订阅者的待发送消息
            foreach (var kvp in _messageQueues.ToList())
            {
                var subscriberId = kvp.Key;
                var queue = kvp.Value;

                if (!_subscribers.TryGetValue(subscriberId, out var subscriber) || 
                    subscriber.DeliveryPaused || !subscriber.IsActive)
                {
                    continue;
                }

                // 处理队列中的消息
                while (queue.Count > 0)
                {
                    var queuedMessage = queue.Dequeue();
                    
                    try
                    {
                        // 这里应该调用实际的消息投递逻辑
                        // 目前只是模拟投递
                        await DeliverMessageToSubscriberAsync(subscriberId, queuedMessage);
                        
                        // 更新统计
                        _stats.TotalMessagesDelivered++;
                        subscriber.TotalMessagesReceived++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "投递消息失败: {MessageId} -> {SubscriberId}", 
                            queuedMessage.Message.MessageId, subscriberId);
                        
                        // 重试逻辑
                        queuedMessage.RetryCount++;
                        if (queuedMessage.RetryCount < _config.MaxRetryAttempts)
                        {
                            // 重新加入队列
                            queue.Enqueue(queuedMessage);
                        }
                        else
                        {
                            // 移到失败队列
                            queuedMessage.Status = MessageStatus.Failed;
                            _failedMessages.GetOrAdd(subscriberId, new List<QueuedMessage>()).Add(queuedMessage);
                            _stats.TotalMessagesFailed++;
                            subscriber.FailedMessageCount++;
                        }
                    }
                    
                    // 限制每次处理的数量，避免阻塞
                    break;
                }
            }
        }

        private async Task DeliverMessageToSubscriberAsync(string subscriberId, QueuedMessage queuedMessage)
        {
            // TODO: 这里应该实现实际的消息投递逻辑
            // 可能通过Hub推送、HTTP回调或其他方式
            await Task.CompletedTask;
            
            _logger.LogDebug("消息投递成功: {MessageId} -> {SubscriberId}", 
                queuedMessage.Message.MessageId, subscriberId);
        }
    }

    // ======== 辅助数据类型 ========

    /// <summary>
    /// 消息路由器持久化状态
    /// </summary>
    [MessagePack.MessagePackObject]
    public class MessageRouterState
    {
        [MessagePack.Key(0)]
        public MessageRouterConfig? Configuration { get; set; }

        [MessagePack.Key(1)]
        public MessageRouterStats? Statistics { get; set; }

        [MessagePack.Key(2)]
        public Dictionary<string, SubscriberInfo>? Subscribers { get; set; }

        [MessagePack.Key(3)]
        public DateTime LastSavedAt { get; set; }
    }

    /// <summary>
    /// 队列中的消息
    /// </summary>
    public class QueuedMessage
    {
        public BaseMessage Message { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public int RetryCount { get; set; }
        public MessageStatus Status { get; set; }
    }

    /// <summary>
    /// 消息状态
    /// </summary>
    public enum MessageStatus
    {
        Pending,    // 待发送
        Delivered,  // 已投递
        Failed,     // 投递失败
        Expired     // 已过期
    }
}
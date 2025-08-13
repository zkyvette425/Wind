using Microsoft.Extensions.Logging;
using Orleans.TestingHost;
using Wind.GrainInterfaces;
using Wind.Grains;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Tests.TestFixtures;
using Xunit;
using Xunit.Abstractions;

namespace Wind.Tests.MessageRouterTests
{
    /// <summary>
    /// MessageRouterGrain单元测试套件
    /// 验证消息路由核心功能和边界条件
    /// </summary>
    public class MessageRouterGrainUnitTests : IClassFixture<ClusterFixture>
    {
        private readonly TestCluster _cluster;
        private readonly ITestOutputHelper _output;

        public MessageRouterGrainUnitTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _cluster = fixture.Cluster;
            _output = output;
        }

        #region 基础功能测试

        [Fact]
        public async Task MessageRouter_Should_Send_Unicast_Message_Successfully()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-1");
            var subscriberId = "subscriber-1";
            
            // 订阅消息
            var subscribeRequest = new SubscribeMessageRequest
            {
                SubscriberId = subscriberId,
                Filter = new MessageFilter()
            };
            await router.SubscribeAsync(subscribeRequest);

            // 创建测试消息
            var message = new TextMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderId = "sender-1",
                Content = "Test message",
                DeliveryMode = MessageDeliveryMode.Unicast,
                TargetIds = [subscriberId]
            };

            // Act
            var sendRequest = new SendMessageRequest { Message = message };
            var response = await router.SendMessageAsync(sendRequest);

            // Assert
            Assert.True(response.Success);
            Assert.Equal(message.MessageId, response.MessageId);
            Assert.Equal(1, response.DeliveredCount);
            Assert.Equal(0, response.FailedCount);
        }

        [Fact]
        public async Task MessageRouter_Should_Handle_Multiple_Subscribers()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-2");
            var subscriberIds = new[] { "sub-1", "sub-2", "sub-3" };
            
            // 订阅多个订阅者
            foreach (var subscriberId in subscriberIds)
            {
                var subscribeRequest = new SubscribeMessageRequest
                {
                    SubscriberId = subscriberId,
                    Filter = new MessageFilter()
                };
                await router.SubscribeAsync(subscribeRequest);
            }

            // 创建广播消息
            var message = new TextMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderId = "broadcaster",
                Content = "Broadcast message",
                DeliveryMode = MessageDeliveryMode.GlobalBroadcast
            };

            // Act
            var sendRequest = new SendMessageRequest { Message = message };
            var response = await router.SendMessageAsync(sendRequest);

            // Assert
            Assert.True(response.Success);
            Assert.Equal(3, response.DeliveredCount);
            Assert.Equal(0, response.FailedCount);
        }

        [Fact]
        public async Task MessageRouter_Should_Apply_Message_Filters()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-3");
            var subscriberId = "filtered-subscriber";
            
            // 订阅带过滤器的消息
            var filter = new MessageFilter
            {
                AllowedMessageTypes = [MessageType.PlayerChat],
                MinimumPriority = MessagePriority.Normal
            };
            
            var subscribeRequest = new SubscribeMessageRequest
            {
                SubscriberId = subscriberId,
                Filter = filter
            };
            await router.SubscribeAsync(subscribeRequest);

            // 创建不符合过滤条件的消息
            var lowPriorityMessage = new TextMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderId = "sender-1",
                Type = MessageType.PlayerChat,
                Priority = MessagePriority.Low, // 低于最小优先级
                Content = "Low priority message",
                DeliveryMode = MessageDeliveryMode.GlobalBroadcast
            };

            // Act
            var sendRequest = new SendMessageRequest { Message = lowPriorityMessage };
            var response = await router.SendMessageAsync(sendRequest);

            // Assert
            Assert.False(response.Success);
            Assert.Equal(0, response.DeliveredCount);
        }

        #endregion

        #region 订阅管理测试

        [Fact]
        public async Task MessageRouter_Should_Subscribe_And_Unsubscribe()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-4");
            var subscriberId = "temp-subscriber";

            // Act - 订阅
            var subscribeRequest = new SubscribeMessageRequest
            {
                SubscriberId = subscriberId,
                Filter = new MessageFilter()
            };
            var subscribeResponse = await router.SubscribeAsync(subscribeRequest);

            // Assert - 订阅成功
            Assert.True(subscribeResponse.Success);
            Assert.Equal(subscriberId, subscribeResponse.SubscriberId);
            Assert.NotEmpty(subscribeResponse.SubscriptionId);

            // Act - 取消订阅
            var unsubscribeRequest = new UnsubscribeMessageRequest
            {
                SubscriberId = subscriberId,
                SubscriptionId = subscribeResponse.SubscriptionId
            };
            var unsubscribeResponse = await router.UnsubscribeAsync(unsubscribeRequest);

            // Assert - 取消订阅成功
            Assert.True(unsubscribeResponse.Success);
            Assert.Equal(subscriberId, unsubscribeResponse.SubscriberId);

            // 验证订阅者不在活跃列表中
            var activeSubscribers = await router.GetActiveSubscribersAsync();
            Assert.DoesNotContain(subscriberId, activeSubscribers);
        }

        [Fact]
        public async Task MessageRouter_Should_Return_Subscriber_Info()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-5");
            var subscriberId = "info-subscriber";

            var subscribeRequest = new SubscribeMessageRequest
            {
                SubscriberId = subscriberId,
                Filter = new MessageFilter { MinimumPriority = MessagePriority.High }
            };

            // Act
            await router.SubscribeAsync(subscribeRequest);
            var subscriberInfo = await router.GetSubscriberInfoAsync(subscriberId);

            // Assert
            Assert.NotNull(subscriberInfo);
            Assert.Equal(subscriberId, subscriberInfo.SubscriberId);
            Assert.True(subscriberInfo.IsActive);
            Assert.False(subscriberInfo.DeliveryPaused);
            Assert.Equal(MessagePriority.High, subscriberInfo.Filter.MinimumPriority);
        }

        #endregion

        #region 消息队列管理测试

        [Fact]
        public async Task MessageRouter_Should_Manage_Message_Queues()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-6");
            var subscriberId = "queue-subscriber";

            await router.SubscribeAsync(new SubscribeMessageRequest
            {
                SubscriberId = subscriberId,
                Filter = new MessageFilter()
            });

            // 暂停投递以积累消息
            await router.PauseDeliveryAsync(subscriberId);

            // 发送多条消息
            for (int i = 0; i < 5; i++)
            {
                var message = new TextMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = "queue-sender",
                    Content = $"Queued message {i}",
                    DeliveryMode = MessageDeliveryMode.Unicast,
                    TargetIds = [subscriberId]
                };

                await router.SendMessageAsync(new SendMessageRequest { Message = message });
            }

            // Act
            var pendingCount = await router.GetPendingMessageCountAsync(subscriberId);

            // Assert
            Assert.Equal(5, pendingCount);

            // 清空队列
            var clearedCount = await router.ClearQueueAsync(subscriberId);
            Assert.Equal(5, clearedCount);

            // 验证队列已清空
            var remainingCount = await router.GetPendingMessageCountAsync(subscriberId);
            Assert.Equal(0, remainingCount);
        }

        [Fact]
        public async Task MessageRouter_Should_Pause_And_Resume_Delivery()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-7");
            var subscriberId = "pause-subscriber";

            await router.SubscribeAsync(new SubscribeMessageRequest
            {
                SubscriberId = subscriberId,
                Filter = new MessageFilter()
            });

            // Act - 暂停投递
            var pauseResult = await router.PauseDeliveryAsync(subscriberId);
            Assert.True(pauseResult);

            // 验证订阅者状态
            var subscriberInfo = await router.GetSubscriberInfoAsync(subscriberId);
            Assert.True(subscriberInfo?.DeliveryPaused);

            // Act - 恢复投递
            var resumeResult = await router.ResumeDeliveryAsync(subscriberId);
            Assert.True(resumeResult);

            // 验证订阅者状态恢复
            subscriberInfo = await router.GetSubscriberInfoAsync(subscriberId);
            Assert.False(subscriberInfo?.DeliveryPaused);
        }

        #endregion

        #region 消息确认和历史记录测试

        [Fact]
        public async Task MessageRouter_Should_Handle_Message_Acknowledgment()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-8");
            var subscriberId = "ack-subscriber";
            var messageId = Guid.NewGuid().ToString();

            await router.SubscribeAsync(new SubscribeMessageRequest
            {
                SubscriberId = subscriberId,
                Filter = new MessageFilter()
            });

            // Act
            var ackRequest = new MessageAcknowledgmentRequest
            {
                MessageId = messageId,
                SubscriberId = subscriberId,
                Processed = true
            };

            var ackResponse = await router.AcknowledgeMessageAsync(ackRequest);

            // Assert
            Assert.True(ackResponse.Success);
            Assert.Equal(messageId, ackResponse.MessageId);
            Assert.Equal(subscriberId, ackResponse.SubscriberId);
        }

        [Fact]
        public async Task MessageRouter_Should_Track_Message_History()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-9");
            var subscriberId = "history-subscriber";

            await router.SubscribeAsync(new SubscribeMessageRequest
            {
                SubscriberId = subscriberId,
                Filter = new MessageFilter()
            });

            // 发送几条不同类型的消息
            var messageTypes = new[] { MessageType.PlayerChat, MessageType.RoomAnnouncement, MessageType.GameEvent };
            
            foreach (var messageType in messageTypes)
            {
                var message = new TextMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = messageType,
                    SenderId = "history-sender",
                    Content = $"Message of type {messageType}",
                    DeliveryMode = MessageDeliveryMode.GlobalBroadcast
                };

                await router.SendMessageAsync(new SendMessageRequest { Message = message });
            }

            // Act - 查询历史记录
            var historyRequest = new GetMessageHistoryRequest
            {
                MessageTypes = [MessageType.PlayerChat, MessageType.RoomAnnouncement],
                Limit = 10
            };

            var historyResponse = await router.GetMessageHistoryAsync(historyRequest);

            // Assert
            Assert.True(historyResponse.Success);
            Assert.True(historyResponse.Messages.Count >= 2);
            Assert.All(historyResponse.Messages, msg => 
                Assert.Contains(msg.Type, new[] { MessageType.PlayerChat, MessageType.RoomAnnouncement }));
        }

        #endregion

        #region 统计和健康检查测试

        [Fact]
        public async Task MessageRouter_Should_Provide_Statistics()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-10");
            var subscriberId = "stats-subscriber";

            await router.SubscribeAsync(new SubscribeMessageRequest
            {
                SubscriberId = subscriberId,
                Filter = new MessageFilter()
            });

            // 发送消息产生统计数据
            var message = new TextMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderId = "stats-sender",
                Content = "Stats test message",
                DeliveryMode = MessageDeliveryMode.Unicast,
                TargetIds = [subscriberId]
            };

            await router.SendMessageAsync(new SendMessageRequest { Message = message });

            // Act
            var stats = await router.GetStatsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.True(stats.TotalMessagesSent > 0);
            Assert.True(stats.ActiveSubscribers > 0);
            Assert.True(stats.MessageCountByType.Count > 0);
        }

        [Fact]
        public async Task MessageRouter_Should_Report_Health_Status()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-11");

            // Act
            var healthStatus = await router.GetHealthStatusAsync();

            // Assert
            Assert.NotNull(healthStatus);
            Assert.NotEmpty(healthStatus.Status);
            Assert.True(healthStatus.CheckTime > DateTime.MinValue);
            Assert.NotNull(healthStatus.Metrics);
        }

        #endregion

        #region 错误处理和边界条件测试

        [Fact]
        public async Task MessageRouter_Should_Handle_Invalid_Message()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-12");

            // 创建无效消息（空MessageId）
            var invalidMessage = new TextMessage
            {
                MessageId = "", // 无效
                SenderId = "sender",
                Content = "Invalid message"
            };

            // Act
            var sendRequest = new SendMessageRequest { Message = invalidMessage };
            var response = await router.SendMessageAsync(sendRequest);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("消息ID不能为空", response.Message);
        }

        [Fact]
        public async Task MessageRouter_Should_Handle_Nonexistent_Subscriber()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-13");
            var nonexistentId = "nonexistent-subscriber";

            // Act
            var subscriberInfo = await router.GetSubscriberInfoAsync(nonexistentId);
            var pendingCount = await router.GetPendingMessageCountAsync(nonexistentId);

            // Assert
            Assert.Null(subscriberInfo);
            Assert.Equal(0, pendingCount);
        }

        [Fact]
        public async Task MessageRouter_Should_Handle_Expired_Messages()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-14");

            // 创建已过期的消息
            var expiredMessage = new TextMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderId = "sender",
                Content = "Expired message",
                ExpiresAt = DateTime.UtcNow.AddSeconds(-1) // 已过期
            };

            // Act
            var sendRequest = new SendMessageRequest { Message = expiredMessage };
            var response = await router.SendMessageAsync(sendRequest);

            // Assert
            Assert.False(response.Success);
            Assert.Contains("已过期", response.Message);
        }

        [Fact]
        public async Task MessageRouter_Should_Handle_Configuration_Updates()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-15");

            var newConfig = new MessageRouterConfig
            {
                MaxQueueSize = 5000,
                MaxRetryAttempts = 5,
                MessageTimeoutMs = 60000,
                EnableMetrics = true
            };

            // Act
            var result = await router.SetConfigurationAsync(newConfig);
            var retrievedConfig = await router.GetConfigurationAsync();

            // Assert
            Assert.True(result);
            Assert.Equal(newConfig.MaxQueueSize, retrievedConfig.MaxQueueSize);
            Assert.Equal(newConfig.MaxRetryAttempts, retrievedConfig.MaxRetryAttempts);
            Assert.Equal(newConfig.MessageTimeoutMs, retrievedConfig.MessageTimeoutMs);
        }

        #endregion

        #region 并发和压力测试

        [Fact]
        public async Task MessageRouter_Should_Handle_Concurrent_Subscriptions()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-16");
            var subscriberCount = 50;
            var tasks = new List<Task<SubscribeMessageResponse>>();

            // Act - 并发订阅
            for (int i = 0; i < subscriberCount; i++)
            {
                var subscriberId = $"concurrent-sub-{i}";
                var subscribeRequest = new SubscribeMessageRequest
                {
                    SubscriberId = subscriberId,
                    Filter = new MessageFilter()
                };

                tasks.Add(router.SubscribeAsync(subscribeRequest));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert
            Assert.All(responses, response => Assert.True(response.Success));
            
            var activeSubscribers = await router.GetActiveSubscribersAsync();
            Assert.True(activeSubscribers.Count >= subscriberCount);
        }

        [Fact]
        public async Task MessageRouter_Should_Handle_Message_Burst()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("test-router-17");
            var subscriberId = "burst-subscriber";

            await router.SubscribeAsync(new SubscribeMessageRequest
            {
                SubscriberId = subscriberId,
                Filter = new MessageFilter()
            });

            var messageCount = 100;
            var sendTasks = new List<Task<SendMessageResponse>>();

            // Act - 并发发送消息
            for (int i = 0; i < messageCount; i++)
            {
                var message = new TextMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = "burst-sender",
                    Content = $"Burst message {i}",
                    DeliveryMode = MessageDeliveryMode.Unicast,
                    TargetIds = [subscriberId]
                };

                sendTasks.Add(router.SendMessageAsync(new SendMessageRequest { Message = message }));
            }

            var responses = await Task.WhenAll(sendTasks);

            // Assert
            var successCount = responses.Count(r => r.Success);
            Assert.True(successCount > messageCount * 0.9); // 至少90%成功

            var stats = await router.GetStatsAsync();
            Assert.True(stats.TotalMessagesSent >= successCount);
        }

        #endregion
    }
}
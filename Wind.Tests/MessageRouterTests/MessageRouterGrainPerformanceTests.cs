using Microsoft.Extensions.Logging;
using Orleans.TestingHost;
using System.Diagnostics;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Tests.TestFixtures;
using Xunit;
using Xunit.Abstractions;

namespace Wind.Tests.MessageRouterTests
{
    /// <summary>
    /// MessageRouterGrain性能测试套件
    /// 验证消息路由系统在高负载下的性能表现
    /// </summary>
    public class MessageRouterGrainPerformanceTests : IClassFixture<ClusterFixture>
    {
        private readonly TestCluster _cluster;
        private readonly ITestOutputHelper _output;

        public MessageRouterGrainPerformanceTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _cluster = fixture.Cluster;
            _output = output;
        }

        #region 吞吐量性能测试

        [Fact]
        public async Task MessageRouter_Should_Handle_High_Throughput_Messages()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("perf-router-1");
            var subscriberCount = 100;
            var messagesPerSubscriber = 50;
            var totalMessages = subscriberCount * messagesPerSubscriber;

            // 设置订阅者
            var subscriptionTasks = new List<Task>();
            for (int i = 0; i < subscriberCount; i++)
            {
                var subscriberId = $"perf-sub-{i}";
                var subscribeRequest = new SubscribeMessageRequest
                {
                    SubscriberId = subscriberId,
                    Filter = new MessageFilter()
                };
                subscriptionTasks.Add(router.SubscribeAsync(subscribeRequest));
            }
            await Task.WhenAll(subscriptionTasks);

            var stopwatch = Stopwatch.StartNew();

            // Act - 高吞吐量消息发送
            var sendTasks = new List<Task<SendMessageResponse>>();
            for (int i = 0; i < totalMessages; i++)
            {
                var targetSubscriberId = $"perf-sub-{i % subscriberCount}";
                var message = new TextMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = "perf-sender",
                    Content = $"Performance test message {i}",
                    Type = MessageType.PlayerChat,
                    Priority = MessagePriority.Normal,
                    DeliveryMode = MessageDeliveryMode.Unicast,
                    TargetIds = [targetSubscriberId]
                };

                sendTasks.Add(router.SendMessageAsync(new SendMessageRequest { Message = message }));
            }

            var responses = await Task.WhenAll(sendTasks);
            stopwatch.Stop();

            // Assert
            var successCount = responses.Count(r => r.Success);
            var successRate = (double)successCount / totalMessages * 100;
            var messagesPerSecond = totalMessages / stopwatch.Elapsed.TotalSeconds;
            var averageLatency = responses.Where(r => r.Success).Average(r => r.DeliveryTime);

            _output.WriteLine($"高吞吐量测试结果:");
            _output.WriteLine($"- 总消息数: {totalMessages:N0}");
            _output.WriteLine($"- 成功消息数: {successCount:N0}");
            _output.WriteLine($"- 成功率: {successRate:F2}%");
            _output.WriteLine($"- 吞吐量: {messagesPerSecond:F0} 消息/秒");
            _output.WriteLine($"- 平均延迟: {averageLatency:F2}ms");
            _output.WriteLine($"- 总耗时: {stopwatch.ElapsedMilliseconds:N0}ms");

            // 性能要求验证
            Assert.True(successRate >= 95.0, $"成功率应该 >= 95%，实际: {successRate:F2}%");
            Assert.True(messagesPerSecond >= 500, $"吞吐量应该 >= 500 消息/秒，实际: {messagesPerSecond:F0}");
            Assert.True(averageLatency <= 200, $"平均延迟应该 <= 200ms，实际: {averageLatency:F2}ms");
        }

        [Fact]
        public async Task MessageRouter_Should_Handle_Broadcast_Performance()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("perf-router-2");
            var subscriberCount = 500;
            var broadcastMessageCount = 20;

            // 设置大量订阅者
            var subscriptionTasks = new List<Task>();
            for (int i = 0; i < subscriberCount; i++)
            {
                var subscriberId = $"broadcast-sub-{i}";
                var subscribeRequest = new SubscribeMessageRequest
                {
                    SubscriberId = subscriberId,
                    Filter = new MessageFilter()
                };
                subscriptionTasks.Add(router.SubscribeAsync(subscribeRequest));
            }
            await Task.WhenAll(subscriptionTasks);

            var stopwatch = Stopwatch.StartNew();

            // Act - 广播消息性能测试
            var broadcastTasks = new List<Task<SendMessageResponse>>();
            for (int i = 0; i < broadcastMessageCount; i++)
            {
                var message = new EventMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = "broadcast-sender",
                    EventType = "PerformanceTest",
                    EventData = new Dictionary<string, object>
                    {
                        ["MessageIndex"] = i,
                        ["Timestamp"] = DateTime.UtcNow
                    },
                    Type = MessageType.RoomAnnouncement,
                    Priority = MessagePriority.High,
                    DeliveryMode = MessageDeliveryMode.GlobalBroadcast
                };

                broadcastTasks.Add(router.SendMessageAsync(new SendMessageRequest { Message = message }));
            }

            var responses = await Task.WhenAll(broadcastTasks);
            stopwatch.Stop();

            // Assert
            var totalDeliveries = responses.Sum(r => r.DeliveredCount);
            var averageDeliveriesPerMessage = (double)totalDeliveries / broadcastMessageCount;
            var deliveriesPerSecond = totalDeliveries / stopwatch.Elapsed.TotalSeconds;

            _output.WriteLine($"广播性能测试结果:");
            _output.WriteLine($"- 订阅者数量: {subscriberCount:N0}");
            _output.WriteLine($"- 广播消息数: {broadcastMessageCount:N0}");
            _output.WriteLine($"- 总投递数: {totalDeliveries:N0}");
            _output.WriteLine($"- 平均每消息投递数: {averageDeliveriesPerMessage:F0}");
            _output.WriteLine($"- 投递速度: {deliveriesPerSecond:F0} 投递/秒");
            _output.WriteLine($"- 总耗时: {stopwatch.ElapsedMilliseconds:N0}ms");

            Assert.True(averageDeliveriesPerMessage >= subscriberCount * 0.95, 
                $"广播覆盖率应该 >= 95%，实际: {averageDeliveriesPerMessage / subscriberCount * 100:F2}%");
            Assert.True(deliveriesPerSecond >= 1000, 
                $"投递速度应该 >= 1000 投递/秒，实际: {deliveriesPerSecond:F0}");
        }

        #endregion

        #region 并发性能测试

        [Fact]
        public async Task MessageRouter_Should_Handle_Concurrent_Operations()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("perf-router-3");
            var concurrentOperations = 200;
            var operationsPerType = concurrentOperations / 4;

            var stopwatch = Stopwatch.StartNew();

            // Act - 并发执行多种操作
            var tasks = new List<Task>();

            // 1. 并发订阅操作
            for (int i = 0; i < operationsPerType; i++)
            {
                var subscriberId = $"concurrent-sub-{i}";
                var subscribeRequest = new SubscribeMessageRequest
                {
                    SubscriberId = subscriberId,
                    Filter = new MessageFilter()
                };
                tasks.Add(router.SubscribeAsync(subscribeRequest));
            }

            // 2. 并发消息发送操作
            for (int i = 0; i < operationsPerType; i++)
            {
                var message = new TextMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = $"concurrent-sender-{i}",
                    Content = $"Concurrent message {i}",
                    DeliveryMode = MessageDeliveryMode.GlobalBroadcast
                };
                tasks.Add(router.SendMessageAsync(new SendMessageRequest { Message = message }));
            }

            // 3. 并发统计查询操作
            for (int i = 0; i < operationsPerType; i++)
            {
                tasks.Add(router.GetStatsAsync());
            }

            // 4. 并发健康检查操作
            for (int i = 0; i < operationsPerType; i++)
            {
                tasks.Add(router.GetHealthStatusAsync());
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var operationsPerSecond = concurrentOperations / stopwatch.Elapsed.TotalSeconds;

            _output.WriteLine($"并发操作性能测试结果:");
            _output.WriteLine($"- 并发操作数: {concurrentOperations:N0}");
            _output.WriteLine($"- 操作速度: {operationsPerSecond:F0} 操作/秒");
            _output.WriteLine($"- 总耗时: {stopwatch.ElapsedMilliseconds:N0}ms");

            Assert.True(operationsPerSecond >= 100, 
                $"并发操作速度应该 >= 100 操作/秒，实际: {operationsPerSecond:F0}");
            Assert.True(stopwatch.ElapsedMilliseconds <= 10000, 
                $"并发操作总耗时应该 <= 10秒，实际: {stopwatch.ElapsedMilliseconds:N0}ms");
        }

        #endregion

        #region 内存和资源性能测试

        [Fact]
        public async Task MessageRouter_Should_Handle_Memory_Pressure()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("perf-router-4");
            var subscriberCount = 1000;
            var messageCount = 2000;

            // 创建大量订阅者
            var subscriptionTasks = new List<Task>();
            for (int i = 0; i < subscriberCount; i++)
            {
                var subscriberId = $"memory-sub-{i}";
                var subscribeRequest = new SubscribeMessageRequest
                {
                    SubscriberId = subscriberId,
                    Filter = new MessageFilter
                    {
                        AllowedMessageTypes = [MessageType.PlayerChat, MessageType.RoomAnnouncement],
                        MinimumPriority = MessagePriority.Normal
                    }
                };
                subscriptionTasks.Add(router.SubscribeAsync(subscribeRequest));
            }
            await Task.WhenAll(subscriptionTasks);

            var initialStats = await router.GetStatsAsync();
            var stopwatch = Stopwatch.StartNew();

            // Act - 发送大量消息进行内存压力测试
            var sendTasks = new List<Task>();
            for (int i = 0; i < messageCount; i++)
            {
                var message = new TextMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = $"memory-sender-{i % 10}",
                    Content = $"Memory pressure test message {i} with some additional content to increase message size",
                    Type = MessageType.PlayerChat,
                    Priority = MessagePriority.Normal,
                    DeliveryMode = MessageDeliveryMode.GlobalBroadcast,
                    Metadata = new Dictionary<string, string>
                    {
                        ["TestIndex"] = i.ToString(),
                        ["BatchId"] = (i / 100).ToString(),
                        ["AdditionalData"] = $"Extra data for message {i}"
                    }
                };
                sendTasks.Add(router.SendMessageAsync(new SendMessageRequest { Message = message }));
            }

            await Task.WhenAll(sendTasks);
            stopwatch.Stop();

            // 等待消息处理完成
            await Task.Delay(2000);

            var finalStats = await router.GetStatsAsync();
            var healthStatus = await router.GetHealthStatusAsync();

            // Assert
            var processedMessages = finalStats.TotalMessagesSent - initialStats.TotalMessagesSent;
            var averageLatency = stopwatch.ElapsedMilliseconds / (double)messageCount;

            _output.WriteLine($"内存压力测试结果:");
            _output.WriteLine($"- 订阅者数量: {subscriberCount:N0}");
            _output.WriteLine($"- 发送消息数: {messageCount:N0}");
            _output.WriteLine($"- 处理消息数: {processedMessages:N0}");
            _output.WriteLine($"- 平均延迟: {averageLatency:F2}ms");
            _output.WriteLine($"- 系统健康状态: {healthStatus.Status}");
            _output.WriteLine($"- 活跃订阅者: {finalStats.ActiveSubscribers:N0}");

            Assert.True(healthStatus.IsHealthy, "系统应该保持健康状态");
            Assert.True(processedMessages >= messageCount * 0.9, 
                $"消息处理率应该 >= 90%，实际: {(double)processedMessages / messageCount * 100:F2}%");
            Assert.True(averageLatency <= 500, 
                $"平均延迟应该 <= 500ms，实际: {averageLatency:F2}ms");
        }

        #endregion

        #region 过滤器性能测试

        [Fact]
        public async Task MessageRouter_Should_Handle_Complex_Filtering_Performance()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("perf-router-5");
            var subscriberCount = 300;
            var messageCount = 1000;

            // 创建具有复杂过滤器的订阅者
            var subscriptionTasks = new List<Task>();
            for (int i = 0; i < subscriberCount; i++)
            {
                var subscriberId = $"filter-sub-{i}";
                var filter = new MessageFilter
                {
                    AllowedMessageTypes = i % 3 == 0 ? [MessageType.PlayerChat] : 
                                        i % 3 == 1 ? [MessageType.RoomAnnouncement] : 
                                        [MessageType.GameEvent],
                    MinimumPriority = i % 4 == 0 ? MessagePriority.Critical :
                                     i % 4 == 1 ? MessagePriority.High :
                                     i % 4 == 2 ? MessagePriority.Normal : MessagePriority.Low,
                    BlockedSenders = i % 5 == 0 ? [$"blocked-sender-{i}"] : null,
                    MetadataFilters = new Dictionary<string, string>
                    {
                        ["Category"] = $"category-{i % 10}"
                    }
                };

                var subscribeRequest = new SubscribeMessageRequest
                {
                    SubscriberId = subscriberId,
                    Filter = filter
                };
                subscriptionTasks.Add(router.SubscribeAsync(subscribeRequest));
            }
            await Task.WhenAll(subscriptionTasks);

            var stopwatch = Stopwatch.StartNew();

            // Act - 发送各种类型的消息测试过滤性能
            var sendTasks = new List<Task<SendMessageResponse>>();
            var messageTypes = new[] { MessageType.PlayerChat, MessageType.RoomAnnouncement, MessageType.GameEvent };
            var priorities = new[] { MessagePriority.Low, MessagePriority.Normal, MessagePriority.High, MessagePriority.Critical };

            for (int i = 0; i < messageCount; i++)
            {
                var message = new EventMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = i % 10 == 0 ? $"blocked-sender-{i % subscriberCount}" : $"normal-sender-{i}",
                    EventType = "FilterTest",
                    Type = messageTypes[i % messageTypes.Length],
                    Priority = priorities[i % priorities.Length],
                    DeliveryMode = MessageDeliveryMode.GlobalBroadcast,
                    Metadata = new Dictionary<string, string>
                    {
                        ["Category"] = $"category-{i % 10}",
                        ["TestId"] = i.ToString()
                    }
                };

                sendTasks.Add(router.SendMessageAsync(new SendMessageRequest { Message = message }));
            }

            var responses = await Task.WhenAll(sendTasks);
            stopwatch.Stop();

            // Assert
            var totalDeliveries = responses.Sum(r => r.DeliveredCount);
            var averageDeliveriesPerMessage = (double)totalDeliveries / messageCount;
            var filteringThroughput = messageCount / stopwatch.Elapsed.TotalSeconds;
            var stats = await router.GetStatsAsync();

            _output.WriteLine($"复杂过滤器性能测试结果:");
            _output.WriteLine($"- 订阅者数量: {subscriberCount:N0}");
            _output.WriteLine($"- 测试消息数: {messageCount:N0}");
            _output.WriteLine($"- 总投递数: {totalDeliveries:N0}");
            _output.WriteLine($"- 平均每消息投递数: {averageDeliveriesPerMessage:F1}");
            _output.WriteLine($"- 过滤吞吐量: {filteringThroughput:F0} 消息/秒");
            _output.WriteLine($"- 过滤处理时间: {stopwatch.ElapsedMilliseconds:N0}ms");

            Assert.True(filteringThroughput >= 200, 
                $"过滤吞吐量应该 >= 200 消息/秒，实际: {filteringThroughput:F0}");
            Assert.True(averageDeliveriesPerMessage <= subscriberCount * 0.5, 
                "过滤器应该有效减少投递数量");
            Assert.True(averageDeliveriesPerMessage >= subscriberCount * 0.1, 
                "过滤器不应该过度阻止消息投递");
        }

        #endregion

        #region 长时间运行性能测试

        [Fact]
        public async Task MessageRouter_Should_Maintain_Performance_Over_Time()
        {
            // Arrange
            var router = _cluster.GrainFactory.GetGrain<IMessageRouterGrain>("perf-router-6");
            var subscriberCount = 100;
            var testDurationSeconds = 30;
            var messagesPerSecond = 50;

            // 设置订阅者
            for (int i = 0; i < subscriberCount; i++)
            {
                var subscriberId = $"longrun-sub-{i}";
                await router.SubscribeAsync(new SubscribeMessageRequest
                {
                    SubscriberId = subscriberId,
                    Filter = new MessageFilter()
                });
            }

            var overallStopwatch = Stopwatch.StartNew();
            var performanceMetrics = new List<(TimeSpan Elapsed, double Latency, int Success)>();
            var totalMessagesSent = 0;

            // Act - 长时间运行测试
            while (overallStopwatch.Elapsed.TotalSeconds < testDurationSeconds)
            {
                var batchStopwatch = Stopwatch.StartNew();
                var batchTasks = new List<Task<SendMessageResponse>>();

                // 发送一批消息
                for (int i = 0; i < messagesPerSecond; i++)
                {
                    var targetSubscriber = $"longrun-sub-{i % subscriberCount}";
                    var message = new TextMessage
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        SenderId = "longrun-sender",
                        Content = $"Long run message {totalMessagesSent + i}",
                        DeliveryMode = MessageDeliveryMode.Unicast,
                        TargetIds = [targetSubscriber]
                    };

                    batchTasks.Add(router.SendMessageAsync(new SendMessageRequest { Message = message }));
                }

                var batchResponses = await Task.WhenAll(batchTasks);
                batchStopwatch.Stop();

                var successCount = batchResponses.Count(r => r.Success);
                var avgLatency = batchResponses.Where(r => r.Success).Average(r => r.DeliveryTime);

                performanceMetrics.Add((overallStopwatch.Elapsed, avgLatency, successCount));
                totalMessagesSent += messagesPerSecond;

                // 短暂停顿以模拟真实负载
                await Task.Delay(1000);
            }

            overallStopwatch.Stop();

            // Assert
            var overallSuccessRate = performanceMetrics.Sum(m => m.Success) / (double)totalMessagesSent * 100;
            var averageLatency = performanceMetrics.Average(m => m.Latency);
            var maxLatency = performanceMetrics.Max(m => m.Latency);
            var latencyStdDev = CalculateStandardDeviation(performanceMetrics.Select(m => m.Latency));

            var finalStats = await router.GetStatsAsync();
            var healthStatus = await router.GetHealthStatusAsync();

            _output.WriteLine($"长时间运行性能测试结果:");
            _output.WriteLine($"- 测试时长: {overallStopwatch.Elapsed.TotalSeconds:F0}秒");
            _output.WriteLine($"- 总消息数: {totalMessagesSent:N0}");
            _output.WriteLine($"- 总成功率: {overallSuccessRate:F2}%");
            _output.WriteLine($"- 平均延迟: {averageLatency:F2}ms");
            _output.WriteLine($"- 最大延迟: {maxLatency:F2}ms");
            _output.WriteLine($"- 延迟标准差: {latencyStdDev:F2}ms");
            _output.WriteLine($"- 最终健康状态: {healthStatus.Status}");
            _output.WriteLine($"- 处理的总消息数: {finalStats.TotalMessagesSent:N0}");

            Assert.True(overallSuccessRate >= 95.0, 
                $"长时间运行成功率应该 >= 95%，实际: {overallSuccessRate:F2}%");
            Assert.True(averageLatency <= 100, 
                $"长时间运行平均延迟应该 <= 100ms，实际: {averageLatency:F2}ms");
            Assert.True(maxLatency <= 1000, 
                $"最大延迟应该 <= 1000ms，实际: {maxLatency:F2}ms");
            Assert.True(healthStatus.IsHealthy, "系统应该在长时间运行后保持健康");
        }

        #endregion

        #region 辅助方法

        private static double CalculateStandardDeviation(IEnumerable<double> values)
        {
            var valueList = values.ToList();
            var average = valueList.Average();
            var sumOfSquaresOfDifferences = valueList.Select(val => (val - average) * (val - average)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / valueList.Count);
        }

        #endregion
    }
}
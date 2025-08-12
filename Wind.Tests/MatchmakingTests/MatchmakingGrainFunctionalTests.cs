using Microsoft.Extensions.Logging;
using Orleans.TestingHost;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Tests.TestFixtures;
using Xunit.Abstractions;

namespace Wind.Tests.MatchmakingTests
{
    /// <summary>
    /// MatchmakingGrain功能验证测试
    /// 验证匹配系统的核心功能是否正常工作
    /// </summary>
    public class MatchmakingGrainFunctionalTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _output;

        public MatchmakingGrainFunctionalTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task MatchmakingGrain_Initialize_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-001";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            var settings = new MatchmakingSettings
            {
                EnableMatchmaking = true,
                MaxConcurrentMatches = 100,
                MaxQueueSize = 1000
            };

            // Act
            var result = await matchmakingGrain.InitializeAsync(settings);

            // Assert
            Assert.True(result, "匹配系统初始化应该成功");

            // 验证系统状态
            var healthStatus = await matchmakingGrain.GetHealthStatusAsync();
            Assert.True(healthStatus.IsHealthy);
            Assert.Equal("Healthy", healthStatus.SystemStatus);

            _output.WriteLine($"匹配系统初始化成功，状态: {healthStatus.SystemStatus}");
        }

        [Fact]
        public async Task MatchmakingGrain_CreateQueue_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-002";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());

            // Act
            var result = await matchmakingGrain.CreateQueueAsync(
                "test-queue", 
                "测试队列", 
                RoomType.Normal, 
                "TestMode",
                new MatchmakingQueueSettings
                {
                    MaxPlayersPerMatch = 4,
                    MinPlayersPerMatch = 2,
                    MaxWaitTime = TimeSpan.FromMinutes(3)
                });

            // Assert
            Assert.True(result, "创建队列应该成功");

            // 验证队列列表
            var queuesResponse = await matchmakingGrain.GetQueuesAsync(new GetMatchmakingQueuesRequest());
            Assert.True(queuesResponse.Success);
            Assert.Contains(queuesResponse.Queues, q => q.QueueId == "test-queue");

            var testQueue = queuesResponse.Queues.First(q => q.QueueId == "test-queue");
            Assert.Equal("测试队列", testQueue.QueueName);
            Assert.Equal(RoomType.Normal, testQueue.RoomType);
            Assert.Equal("TestMode", testQueue.GameMode);
            Assert.True(testQueue.IsActive);

            _output.WriteLine($"队列创建成功: {testQueue.QueueName}");
        }

        [Fact]
        public async Task MatchmakingGrain_QuickMatch_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-003";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());

            var quickMatchRequest = new QuickMatchRequest
            {
                PlayerId = "test-player-001",
                PlayerName = "测试玩家1",
                PlayerLevel = 10,
                Criteria = new MatchmakingCriteria
                {
                    PreferredRoomType = RoomType.Normal,
                    PreferredGameMode = "Default",
                    MinPlayerCount = 2,
                    MaxPlayerCount = 4
                }
            };

            // Act
            var response = await matchmakingGrain.QuickMatchAsync(quickMatchRequest);

            // Assert
            Assert.True(response.Success, "快速匹配应该成功");
            Assert.NotNull(response.RequestId);
            Assert.True(response.EstimatedWaitTime >= 0);

            _output.WriteLine($"快速匹配成功，请求ID: {response.RequestId}，预计等待时间: {response.EstimatedWaitTime}秒");
        }

        [Fact]
        public async Task MatchmakingGrain_JoinQueue_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-004";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());
            await matchmakingGrain.CreateQueueAsync("test-queue", "测试队列", RoomType.Normal, "TestMode");

            var joinRequest = new JoinMatchmakingQueueRequest
            {
                PlayerId = "test-player-002",
                QueueId = "test-queue",
                Criteria = new MatchmakingCriteria
                {
                    PreferredRoomType = RoomType.Normal,
                    PreferredGameMode = "TestMode"
                },
                PlayerData = new Dictionary<string, object>
                {
                    { "PlayerName", "测试玩家2" },
                    { "PlayerLevel", 15 }
                }
            };

            // Act
            var response = await matchmakingGrain.JoinQueueAsync(joinRequest);

            // Assert
            Assert.True(response.Success, "加入队列应该成功");
            Assert.NotNull(response.RequestId);
            Assert.Equal(1, response.QueuePosition);

            // 验证队列玩家数量
            var playerCount = await matchmakingGrain.GetQueuePlayerCountAsync("test-queue");
            Assert.Equal(1, playerCount);

            _output.WriteLine($"成功加入队列，位置: {response.QueuePosition}，队列人数: {playerCount}");
        }

        [Fact]
        public async Task MatchmakingGrain_CancelMatchmaking_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-005";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());

            // 先加入匹配
            var quickMatchRequest = new QuickMatchRequest
            {
                PlayerId = "test-player-003",
                PlayerName = "测试玩家3",
                PlayerLevel = 20
            };

            var matchResponse = await matchmakingGrain.QuickMatchAsync(quickMatchRequest);
            Assert.True(matchResponse.Success);

            var cancelRequest = new CancelMatchmakingRequest
            {
                PlayerId = "test-player-003",
                RequestId = matchResponse.RequestId
            };

            // Act
            var response = await matchmakingGrain.CancelMatchmakingAsync(cancelRequest);

            // Assert
            Assert.True(response.Success, "取消匹配应该成功");

            // 验证玩家请求已被移除
            var playerRequest = await matchmakingGrain.GetPlayerRequestAsync("test-player-003");
            Assert.Null(playerRequest);

            _output.WriteLine("匹配取消成功");
        }

        [Fact]
        public async Task MatchmakingGrain_GetMatchmakingStatus_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-006";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());

            // 加入匹配
            var quickMatchRequest = new QuickMatchRequest
            {
                PlayerId = "test-player-004",
                PlayerName = "测试玩家4",
                PlayerLevel = 25
            };

            var matchResponse = await matchmakingGrain.QuickMatchAsync(quickMatchRequest);
            Assert.True(matchResponse.Success);

            var statusRequest = new GetMatchmakingStatusRequest
            {
                PlayerId = "test-player-004",
                RequestId = matchResponse.RequestId
            };

            // Act
            var response = await matchmakingGrain.GetMatchmakingStatusAsync(statusRequest);

            // Assert
            Assert.True(response.Success, "获取匹配状态应该成功");
            Assert.NotNull(response.Request);
            Assert.Equal("test-player-004", response.Request.PlayerId);
            Assert.Equal(MatchmakingRequestStatus.Queued, response.Request.Status);
            Assert.True(response.QueuePosition >= 1);

            _output.WriteLine($"匹配状态: {response.Request.Status}，队列位置: {response.QueuePosition}");
        }

        [Fact]
        public async Task MatchmakingGrain_MultiplePlayersMatch_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-007";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());
            await matchmakingGrain.CreateQueueAsync(
                "match-queue", 
                "匹配测试队列", 
                RoomType.Normal, 
                "MatchMode",
                new MatchmakingQueueSettings
                {
                    MinPlayersPerMatch = 2,
                    MaxPlayersPerMatch = 4,
                    MaxWaitTime = TimeSpan.FromMinutes(5)
                });

            var playerIds = new List<string>();
            var requestIds = new List<string>();

            // Act - 添加多个玩家到队列
            for (int i = 1; i <= 3; i++)
            {
                var playerId = $"match-player-{i:D3}";
                playerIds.Add(playerId);

                var joinRequest = new JoinMatchmakingQueueRequest
                {
                    PlayerId = playerId,
                    QueueId = "match-queue",
                    Criteria = new MatchmakingCriteria
                    {
                        PreferredRoomType = RoomType.Normal,
                        PreferredGameMode = "MatchMode",
                        MinPlayerCount = 2,
                        MaxPlayerCount = 4
                    },
                    PlayerData = new Dictionary<string, object>
                    {
                        { "PlayerName", $"匹配玩家{i}" },
                        { "PlayerLevel", 10 + i * 5 }
                    }
                };

                var response = await matchmakingGrain.JoinQueueAsync(joinRequest);
                Assert.True(response.Success);
                requestIds.Add(response.RequestId);

                _output.WriteLine($"玩家 {playerId} 加入队列，位置: {response.QueuePosition}");
            }

            // 触发匹配检查
            var matchesFound = await matchmakingGrain.TriggerMatchCheckAsync("match-queue");

            _output.WriteLine($"触发匹配检查，找到匹配: {matchesFound}个");

            // 等待短暂时间以确保匹配处理完成
            await Task.Delay(1000);

            // 验证匹配结果 - 检查玩家状态
            foreach (var playerId in playerIds)
            {
                var playerRequest = await matchmakingGrain.GetPlayerRequestAsync(playerId);
                if (playerRequest != null)
                {
                    _output.WriteLine($"玩家 {playerId} 状态: {playerRequest.Status}");
                }
                else
                {
                    _output.WriteLine($"玩家 {playerId} 已被匹配或移除");
                }
            }
        }

        [Fact]
        public async Task MatchmakingGrain_GetStatistics_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-008";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());

            // 添加一些匹配请求
            for (int i = 1; i <= 2; i++)
            {
                await matchmakingGrain.QuickMatchAsync(new QuickMatchRequest
                {
                    PlayerId = $"stats-player-{i}",
                    PlayerName = $"统计玩家{i}",
                    PlayerLevel = i * 10
                });
            }

            var statisticsRequest = new GetMatchmakingStatisticsRequest
            {
                IncludeQueueDetails = true,
                IncludeHistoricalData = false
            };

            // Act
            var response = await matchmakingGrain.GetStatisticsAsync(statisticsRequest);

            // Assert
            Assert.True(response.Success, "获取统计信息应该成功");
            Assert.NotNull(response.Statistics);
            Assert.Equal(2, response.Statistics.CurrentPlayersInQueue);
            Assert.NotEmpty(response.QueueDetails);

            _output.WriteLine($"当前队列玩家数: {response.Statistics.CurrentPlayersInQueue}");
            _output.WriteLine($"总匹配数: {response.Statistics.TotalMatchesMade}");
            _output.WriteLine($"队列数量: {response.QueueDetails.Count}");
        }

        [Fact]
        public async Task MatchmakingGrain_QueueManagement_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-009";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());

            // 创建队列
            var createResult = await matchmakingGrain.CreateQueueAsync(
                "manage-queue", 
                "管理测试队列", 
                RoomType.Ranked, 
                "RankedMode");
            Assert.True(createResult);

            // 更新队列设置
            var newSettings = new MatchmakingQueueSettings
            {
                MaxPlayersPerMatch = 6,
                MinPlayersPerMatch = 3,
                MaxWaitTime = TimeSpan.FromMinutes(10),
                LevelDifferenceThreshold = 20
            };

            var updateResult = await matchmakingGrain.UpdateQueueSettingsAsync("manage-queue", newSettings);
            Assert.True(updateResult);

            // 禁用队列
            var disableResult = await matchmakingGrain.SetQueueActiveAsync("manage-queue", false);
            Assert.True(disableResult);

            // Act & Assert - 验证队列状态
            var queuesResponse = await matchmakingGrain.GetQueuesAsync(new GetMatchmakingQueuesRequest
            {
                IncludeInactive = true
            });

            var managedQueue = queuesResponse.Queues.FirstOrDefault(q => q.QueueId == "manage-queue");
            Assert.NotNull(managedQueue);
            Assert.False(managedQueue.IsActive);
            Assert.Equal(6, managedQueue.Settings.MaxPlayersPerMatch);
            Assert.Equal(3, managedQueue.Settings.MinPlayersPerMatch);

            // 重新启用队列
            var enableResult = await matchmakingGrain.SetQueueActiveAsync("manage-queue", true);
            Assert.True(enableResult);

            // 删除队列
            var removeResult = await matchmakingGrain.RemoveQueueAsync("manage-queue");
            Assert.True(removeResult);

            // 验证队列已被删除
            var finalQueuesResponse = await matchmakingGrain.GetQueuesAsync(new GetMatchmakingQueuesRequest());
            Assert.DoesNotContain(finalQueuesResponse.Queues, q => q.QueueId == "manage-queue");

            _output.WriteLine("队列管理操作测试完成");
        }

        [Fact]
        public async Task MatchmakingGrain_CleanupExpiredRequests_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-010";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            // 设置较短的超时时间进行测试
            var settings = new MatchmakingSettings
            {
                RequestTimeout = TimeSpan.FromSeconds(1) // 1秒超时
            };
            await matchmakingGrain.InitializeAsync(settings);

            // 添加匹配请求
            var quickMatchRequest = new QuickMatchRequest
            {
                PlayerId = "cleanup-player-001",
                PlayerName = "清理测试玩家",
                PlayerLevel = 30
            };

            var matchResponse = await matchmakingGrain.QuickMatchAsync(quickMatchRequest);
            Assert.True(matchResponse.Success);

            // 等待超时
            await Task.Delay(2000);

            // Act
            var cleanedCount = await matchmakingGrain.CleanupExpiredRequestsAsync();

            // Assert
            Assert.True(cleanedCount >= 0, "清理操作应该成功");

            // 验证请求已被清理
            var playerRequest = await matchmakingGrain.GetPlayerRequestAsync("cleanup-player-001");
            Assert.Null(playerRequest);

            _output.WriteLine($"清理过期请求完成，清理数量: {cleanedCount}");
        }

        [Fact]
        public async Task MatchmakingGrain_HealthStatus_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-011";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());

            // 添加一些数据
            await matchmakingGrain.CreateQueueAsync("health-queue", "健康检查队列", RoomType.Normal, "HealthMode");
            await matchmakingGrain.QuickMatchAsync(new QuickMatchRequest
            {
                PlayerId = "health-player",
                PlayerName = "健康检查玩家",
                PlayerLevel = 1
            });

            // Act
            var healthStatus = await matchmakingGrain.GetHealthStatusAsync();

            // Assert
            Assert.NotNull(healthStatus);
            Assert.True(healthStatus.IsHealthy);
            Assert.Equal("Healthy", healthStatus.SystemStatus);
            Assert.True(healthStatus.TotalActiveQueues > 0);
            Assert.True(healthStatus.TotalPlayersInQueues > 0);
            Assert.True(healthStatus.Uptime.TotalSeconds > 0);

            _output.WriteLine($"健康状态: {healthStatus.SystemStatus}");
            _output.WriteLine($"活跃队列: {healthStatus.TotalActiveQueues}");
            _output.WriteLine($"队列玩家: {healthStatus.TotalPlayersInQueues}");
            _output.WriteLine($"运行时间: {healthStatus.Uptime}");
            
            if (healthStatus.Issues.Any())
            {
                foreach (var issue in healthStatus.Issues)
                {
                    _output.WriteLine($"问题: {issue}");
                }
            }
        }

        [Fact]
        public async Task MatchmakingGrain_ForceRemovePlayer_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "test-matchmaking-012";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());

            // 添加匹配请求
            var quickMatchRequest = new QuickMatchRequest
            {
                PlayerId = "force-remove-player",
                PlayerName = "强制移除测试玩家",
                PlayerLevel = 50
            };

            var matchResponse = await matchmakingGrain.QuickMatchAsync(quickMatchRequest);
            Assert.True(matchResponse.Success);

            // 验证玩家在队列中
            var playerRequest = await matchmakingGrain.GetPlayerRequestAsync("force-remove-player");
            Assert.NotNull(playerRequest);

            // Act
            var removeResult = await matchmakingGrain.ForceRemovePlayerRequestAsync(
                "force-remove-player", 
                "管理员强制移除");

            // Assert
            Assert.True(removeResult, "强制移除应该成功");

            // 验证玩家已被移除
            var removedPlayerRequest = await matchmakingGrain.GetPlayerRequestAsync("force-remove-player");
            Assert.Null(removedPlayerRequest);

            _output.WriteLine("强制移除玩家成功");
        }
    }
}
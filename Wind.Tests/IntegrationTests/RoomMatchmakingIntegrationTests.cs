using Microsoft.Extensions.Logging;
using Orleans.TestingHost;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Tests.TestFixtures;
using Xunit.Abstractions;

namespace Wind.Tests.IntegrationTests
{
    /// <summary>
    /// 房间匹配系统端到端集成测试
    /// 验证玩家、房间、匹配系统的完整交互流程
    /// </summary>
    public class RoomMatchmakingIntegrationTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _output;

        public RoomMatchmakingIntegrationTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task PlayerRoomWorkflow_Should_Work_End_To_End()
        {
            // Arrange
            var playerId1 = "integration-player-001";
            var playerId2 = "integration-player-002";
            var roomId = $"integration-room-{Guid.NewGuid()}";

            var playerGrain1 = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId1);
            var playerGrain2 = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId2);
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // Act & Assert - 完整的游戏流程

            // 1. 玩家登录
            _output.WriteLine("=== 1. 玩家登录阶段 ===");
            
            var loginResponse1 = await playerGrain1.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId1,
                DisplayName = "集成测试玩家1",
                ClientVersion = "1.0.0",
                Platform = "Windows"
            });
            Assert.True(loginResponse1.Success, "玩家1登录应该成功");
            _output.WriteLine($"玩家1登录成功: {loginResponse1.PlayerInfo?.DisplayName}");

            var loginResponse2 = await playerGrain2.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId2,
                DisplayName = "集成测试玩家2",
                ClientVersion = "1.0.0",
                Platform = "Windows"
            });
            Assert.True(loginResponse2.Success, "玩家2登录应该成功");
            _output.WriteLine($"玩家2登录成功: {loginResponse2.PlayerInfo?.DisplayName}");

            // 2. 创建房间
            _output.WriteLine("=== 2. 房间创建阶段 ===");
            
            var createRoomResponse = await roomGrain.CreateRoomAsync(new CreateRoomRequest
            {
                CreatorId = playerId1,
                RoomName = "集成测试房间",
                RoomType = RoomType.Normal,
                MaxPlayerCount = 4,
                Settings = new RoomSettings
                {
                    GameMode = "TestMode",
                    MapId = "TestMap",
                    MinPlayersToStart = 2,
                    AutoStart = false
                }
            });
            Assert.True(createRoomResponse.Success, "房间创建应该成功");
            _output.WriteLine($"房间创建成功: {createRoomResponse.RoomInfo?.RoomName}");

            // 3. 玩家加入房间
            _output.WriteLine("=== 3. 玩家加入房间阶段 ===");
            
            var joinResponse1 = await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = playerId1,
                RoomId = roomId,
                PlayerData = new Dictionary<string, object>
                {
                    { "DisplayName", "集成测试玩家1" },
                    { "Level", 10 }
                }
            });
            Assert.True(joinResponse1.Success, "玩家1加入房间应该成功");
            _output.WriteLine($"玩家1加入房间: {joinResponse1.PlayerInfo?.DisplayName}");

            var joinResponse2 = await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = playerId2,
                RoomId = roomId,
                PlayerData = new Dictionary<string, object>
                {
                    { "DisplayName", "集成测试玩家2" },
                    { "Level", 12 }
                }
            });
            Assert.True(joinResponse2.Success, "玩家2加入房间应该成功");
            _output.WriteLine($"玩家2加入房间: {joinResponse2.PlayerInfo?.DisplayName}");

            // 验证房间状态
            var roomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
            Assert.Equal(2, roomInfo.RoomInfo?.CurrentPlayerCount);

            // 4. 更新玩家房间状态
            _output.WriteLine("=== 4. 更新玩家状态阶段 ===");
            
            await playerGrain1.UpdatePlayerAsync(new PlayerUpdateRequest
            {
                PlayerId = playerId1,
                CurrentRoomId = roomId,
                OnlineStatus = PlayerOnlineStatus.InGame
            });

            await playerGrain2.UpdatePlayerAsync(new PlayerUpdateRequest
            {
                PlayerId = playerId2,
                CurrentRoomId = roomId,
                OnlineStatus = PlayerOnlineStatus.InGame
            });

            // 验证玩家状态更新
            var playerInfo1 = await playerGrain1.GetPlayerInfoAsync(true);
            Assert.NotNull(playerInfo1);
            Assert.Equal(roomId, playerInfo1.CurrentRoomId);
            Assert.Equal(PlayerOnlineStatus.InGame, playerInfo1.OnlineStatus);

            // 5. 玩家准备
            _output.WriteLine("=== 5. 玩家准备阶段 ===");
            
            var readyResponse1 = await roomGrain.SetPlayerReadyAsync(new PlayerReadyRequest
            {
                PlayerId = playerId1,
                RoomId = roomId,
                ReadyStatus = PlayerReadyStatus.Ready
            });
            Assert.True(readyResponse1.Success, "玩家1准备应该成功");

            var readyResponse2 = await roomGrain.SetPlayerReadyAsync(new PlayerReadyRequest
            {
                PlayerId = playerId2,
                RoomId = roomId,
                ReadyStatus = PlayerReadyStatus.Ready
            });
            Assert.True(readyResponse2.Success, "玩家2准备应该成功");

            // 验证游戏可以开始
            var canStart = await roomGrain.CanStartGameAsync();
            Assert.True(canStart, "游戏应该可以开始");

            // 6. 开始游戏
            _output.WriteLine("=== 6. 游戏开始阶段 ===");
            
            var startGameResponse = await roomGrain.StartGameAsync(new StartGameRequest
            {
                PlayerId = playerId1, // 房主开始游戏
                RoomId = roomId
            });
            Assert.True(startGameResponse.Success, "开始游戏应该成功");
            _output.WriteLine($"游戏开始成功，开始时间: {startGameResponse.GameStartTime}");

            // 验证房间状态
            roomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
            Assert.Equal(RoomStatus.InGame, roomInfo.RoomInfo?.Status);

            // 7. 游戏过程中的操作
            _output.WriteLine("=== 7. 游戏进行阶段 ===");
            
            // 更新玩家位置
            await roomGrain.UpdatePlayerPositionAsync(playerId1, new PlayerPosition
            {
                X = 100.0f,
                Y = 200.0f,
                Z = 0.0f,
                MapId = "TestMap"
            });

            // 更新玩家分数
            await roomGrain.UpdatePlayerScoreAsync(playerId1, 50);
            await roomGrain.UpdatePlayerScoreAsync(playerId2, 30);

            // 验证分数更新
            roomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
            var player1 = roomInfo.RoomInfo?.Players.FirstOrDefault(p => p.PlayerId == playerId1);
            var player2 = roomInfo.RoomInfo?.Players.FirstOrDefault(p => p.PlayerId == playerId2);
            
            Assert.Equal(50, player1?.Score);
            Assert.Equal(30, player2?.Score);

            // 8. 结束游戏
            _output.WriteLine("=== 8. 游戏结束阶段 ===");
            
            var endGameResponse = await roomGrain.EndGameAsync(new EndGameRequest
            {
                PlayerId = playerId1,
                RoomId = roomId,
                FinalScores = new Dictionary<string, int>
                {
                    { playerId1, 100 },
                    { playerId2, 80 }
                }
            });
            Assert.True(endGameResponse.Success, "结束游戏应该成功");
            Assert.Equal(playerId1, endGameResponse.Winner);
            _output.WriteLine($"游戏结束，获胜者: {endGameResponse.Winner}");

            // 9. 查看房间事件
            _output.WriteLine("=== 9. 房间事件历史 ===");
            
            var events = await roomGrain.GetRecentEventsAsync(20);
            Assert.NotEmpty(events);
            
            foreach (var evt in events.OrderBy(e => e.Timestamp))
            {
                _output.WriteLine($"事件: {evt.EventType} - {evt.Description} ({evt.Timestamp:HH:mm:ss})");
            }

            // 10. 玩家离开房间
            _output.WriteLine("=== 10. 玩家离开阶段 ===");
            
            var leaveResponse1 = await roomGrain.LeaveRoomAsync(new LeaveRoomRequest
            {
                PlayerId = playerId1,
                RoomId = roomId,
                Reason = "游戏结束"
            });
            Assert.True(leaveResponse1.Success, "玩家1离开房间应该成功");

            var leaveResponse2 = await roomGrain.LeaveRoomAsync(new LeaveRoomRequest
            {
                PlayerId = playerId2,
                RoomId = roomId,
                Reason = "游戏结束"
            });
            Assert.True(leaveResponse2.Success, "玩家2离开房间应该成功");

            // 11. 玩家登出
            _output.WriteLine("=== 11. 玩家登出阶段 ===");
            
            var logoutResponse1 = await playerGrain1.LogoutAsync(new PlayerLogoutRequest
            {
                PlayerId = playerId1,
                Reason = "游戏结束"
            });
            Assert.True(logoutResponse1.Success, "玩家1登出应该成功");

            var logoutResponse2 = await playerGrain2.LogoutAsync(new PlayerLogoutRequest
            {
                PlayerId = playerId2,
                Reason = "游戏结束"
            });
            Assert.True(logoutResponse2.Success, "玩家2登出应该成功");

            _output.WriteLine("=== 端到端测试完成 ===");
        }

        [Fact]
        public async Task MatchmakingToGameWorkflow_Should_Work_End_To_End()
        {
            // Arrange
            var matchmakingId = "integration-matchmaking";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            var playerId1 = "match-player-001";
            var playerId2 = "match-player-002";
            var playerId3 = "match-player-003";

            // Act & Assert - 匹配到游戏的完整流程

            // 1. 初始化匹配系统
            _output.WriteLine("=== 1. 匹配系统初始化 ===");
            
            await matchmakingGrain.InitializeAsync(new MatchmakingSettings
            {
                EnableMatchmaking = true,
                MaxConcurrentMatches = 100,
                MaxQueueSize = 1000
            });

            var healthStatus = await matchmakingGrain.GetHealthStatusAsync();
            Assert.True(healthStatus.IsHealthy, "匹配系统应该健康");
            _output.WriteLine($"匹配系统状态: {healthStatus.SystemStatus}");

            // 2. 创建匹配队列
            _output.WriteLine("=== 2. 创建匹配队列 ===");
            
            var queueCreated = await matchmakingGrain.CreateQueueAsync(
                "integration-queue",
                "集成测试队列",
                RoomType.Normal,
                "IntegrationMode",
                new MatchmakingQueueSettings
                {
                    MinPlayersPerMatch = 2,
                    MaxPlayersPerMatch = 4,
                    MaxWaitTime = TimeSpan.FromMinutes(5)
                });
            Assert.True(queueCreated, "匹配队列创建应该成功");

            // 3. 玩家快速匹配
            _output.WriteLine("=== 3. 玩家匹配阶段 ===");
            
            var quickMatchRequests = new List<(string PlayerId, QuickMatchRequest Request)>
            {
                (playerId1, new QuickMatchRequest
                {
                    PlayerId = playerId1,
                    PlayerName = "匹配玩家1",
                    PlayerLevel = 10,
                    Criteria = new MatchmakingCriteria
                    {
                        PreferredRoomType = RoomType.Normal,
                        PreferredGameMode = "IntegrationMode"
                    }
                }),
                (playerId2, new QuickMatchRequest
                {
                    PlayerId = playerId2,
                    PlayerName = "匹配玩家2",
                    PlayerLevel = 12,
                    Criteria = new MatchmakingCriteria
                    {
                        PreferredRoomType = RoomType.Normal,
                        PreferredGameMode = "IntegrationMode"
                    }
                }),
                (playerId3, new QuickMatchRequest
                {
                    PlayerId = playerId3,
                    PlayerName = "匹配玩家3",
                    PlayerLevel = 15,
                    Criteria = new MatchmakingCriteria
                    {
                        PreferredRoomType = RoomType.Normal,
                        PreferredGameMode = "IntegrationMode"
                    }
                })
            };

            var matchResponses = new List<QuickMatchResponse>();
            foreach (var (playerId, request) in quickMatchRequests)
            {
                var response = await matchmakingGrain.QuickMatchAsync(request);
                Assert.True(response.Success, $"玩家{playerId}快速匹配应该成功");
                matchResponses.Add(response);
                _output.WriteLine($"玩家{playerId}加入匹配队列，请求ID: {response.RequestId}");
            }

            // 4. 检查匹配状态
            _output.WriteLine("=== 4. 检查匹配状态 ===");
            
            foreach (var (playerId, _) in quickMatchRequests)
            {
                var statusResponse = await matchmakingGrain.GetMatchmakingStatusAsync(new GetMatchmakingStatusRequest
                {
                    PlayerId = playerId
                });
                
                if (statusResponse.Success)
                {
                    _output.WriteLine($"玩家{playerId}状态: {statusResponse.Request?.Status}, 队列位置: {statusResponse.QueuePosition}");
                }
                else
                {
                    _output.WriteLine($"玩家{playerId}状态查询失败或已被匹配");
                }
            }

            // 5. 触发匹配检查
            _output.WriteLine("=== 5. 触发匹配检查 ===");
            
            var matchesFound = await matchmakingGrain.TriggerMatchCheckAsync("integration-queue");
            _output.WriteLine($"找到匹配: {matchesFound}个");

            // 等待匹配处理
            await Task.Delay(2000);

            // 6. 验证匹配结果
            _output.WriteLine("=== 6. 验证匹配结果 ===");
            
            var statistics = await matchmakingGrain.GetStatisticsAsync(new GetMatchmakingStatisticsRequest
            {
                IncludeQueueDetails = true
            });
            
            _output.WriteLine($"匹配统计: 总匹配数={statistics.Statistics?.TotalMatchesMade}, 队列玩家数={statistics.Statistics?.CurrentPlayersInQueue}");

            // 检查玩家状态
            foreach (var (playerId, _) in quickMatchRequests)
            {
                var playerRequest = await matchmakingGrain.GetPlayerRequestAsync(playerId);
                if (playerRequest != null)
                {
                    _output.WriteLine($"玩家{playerId}状态: {playerRequest.Status}");
                    if (playerRequest.Status == MatchmakingRequestStatus.Matched && !string.IsNullOrEmpty(playerRequest.MatchedRoomId))
                    {
                        _output.WriteLine($"玩家{playerId}已匹配到房间: {playerRequest.MatchedRoomId}");
                        
                        // 验证房间是否存在
                        var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(playerRequest.MatchedRoomId);
                        var roomExists = await roomGrain.IsExistsAsync();
                        Assert.True(roomExists, "匹配创建的房间应该存在");
                        
                        var roomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest 
                        { 
                            RoomId = playerRequest.MatchedRoomId 
                        });
                        if (roomInfo.Success)
                        {
                            _output.WriteLine($"房间信息: {roomInfo.RoomInfo?.RoomName}, 玩家数: {roomInfo.RoomInfo?.CurrentPlayerCount}");
                        }
                    }
                }
                else
                {
                    _output.WriteLine($"玩家{playerId}请求已被移除（已匹配或取消）");
                }
            }

            // 7. 清理和健康检查
            _output.WriteLine("=== 7. 系统清理和健康检查 ===");
            
            await matchmakingGrain.CleanupExpiredRequestsAsync();
            
            var finalHealthStatus = await matchmakingGrain.GetHealthStatusAsync();
            _output.WriteLine($"最终健康状态: {finalHealthStatus.SystemStatus}");
            _output.WriteLine($"活跃队列: {finalHealthStatus.TotalActiveQueues}");
            _output.WriteLine($"队列玩家: {finalHealthStatus.TotalPlayersInQueues}");

            if (finalHealthStatus.Issues.Any())
            {
                foreach (var issue in finalHealthStatus.Issues)
                {
                    _output.WriteLine($"系统问题: {issue}");
                }
            }

            _output.WriteLine("=== 匹配到游戏流程测试完成 ===");
        }

        [Fact]
        public async Task CompletePlayerJourney_Should_Work_End_To_End()
        {
            // Arrange - 完整的玩家游戏旅程
            var playerId = "journey-player";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>("journey-matchmaking");

            _output.WriteLine("=== 完整玩家游戏旅程测试 ===");

            // 1. 玩家登录
            _output.WriteLine("1. 玩家登录");
            var loginResponse = await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "旅程测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Windows"
            });
            Assert.True(loginResponse.Success);

            // 2. 初始化匹配系统
            _output.WriteLine("2. 初始化匹配系统");
            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());

            // 3. 快速匹配
            _output.WriteLine("3. 开始快速匹配");
            var matchResponse = await matchmakingGrain.QuickMatchAsync(new QuickMatchRequest
            {
                PlayerId = playerId,
                PlayerName = "旅程测试玩家",
                PlayerLevel = 25
            });
            Assert.True(matchResponse.Success);

            // 4. 检查匹配状态
            _output.WriteLine("4. 检查匹配状态");
            var statusResponse = await matchmakingGrain.GetMatchmakingStatusAsync(new GetMatchmakingStatusRequest
            {
                PlayerId = playerId
            });
            Assert.True(statusResponse.Success);
            _output.WriteLine($"匹配状态: {statusResponse.Request?.Status}");

            // 5. 如果没有立即匹配，等待或创建新房间进行测试
            string? roomId = null;
            if (statusResponse.Request?.Status == MatchmakingRequestStatus.Queued)
            {
                _output.WriteLine("5. 创建测试房间（模拟匹配成功）");
                roomId = $"journey-room-{Guid.NewGuid()}";
                var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);
                
                await roomGrain.CreateRoomAsync(new CreateRoomRequest
                {
                    CreatorId = playerId,
                    RoomName = "旅程测试房间",
                    MaxPlayerCount = 4
                });

                await roomGrain.JoinRoomAsync(new JoinRoomRequest
                {
                    PlayerId = playerId,
                    RoomId = roomId
                });

                // 取消原有匹配
                await matchmakingGrain.CancelMatchmakingAsync(new CancelMatchmakingRequest
                {
                    PlayerId = playerId
                });
            }

            // 6. 更新玩家状态到游戏中
            if (!string.IsNullOrEmpty(roomId))
            {
                _output.WriteLine("6. 更新玩家状态");
                await playerGrain.UpdatePlayerAsync(new PlayerUpdateRequest
                {
                    PlayerId = playerId,
                    CurrentRoomId = roomId,
                    OnlineStatus = PlayerOnlineStatus.InGame
                });

                // 7. 更新位置
                _output.WriteLine("7. 更新玩家位置");
                await playerGrain.UpdatePositionAsync(new PlayerPosition
                {
                    X = 150.0f,
                    Y = 250.0f,
                    Z = 10.0f,
                    MapId = "JourneyMap"
                });

                // 8. 验证最终状态
                _output.WriteLine("8. 验证最终玩家状态");
                var finalPlayerInfo = await playerGrain.GetPlayerInfoAsync(true);
                
                Assert.NotNull(finalPlayerInfo);
                Assert.Equal(roomId, finalPlayerInfo.CurrentRoomId);
                Assert.Equal(PlayerOnlineStatus.InGame, finalPlayerInfo.OnlineStatus);
                
                _output.WriteLine($"最终状态: 房间={finalPlayerInfo.CurrentRoomId}, 状态={finalPlayerInfo.OnlineStatus}");
            }

            // 9. 玩家登出
            _output.WriteLine("9. 玩家登出");
            var logoutResponse = await playerGrain.LogoutAsync(new PlayerLogoutRequest
            {
                PlayerId = playerId,
                Reason = "会话结束"
            });
            Assert.True(logoutResponse.Success);

            _output.WriteLine("=== 完整玩家游戏旅程测试完成 ===");
        }
    }
}
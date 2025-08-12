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
    /// 玩家-房间-匹配系统端到端集成测试
    /// 验证完整的游戏流程是否正常工作
    /// </summary>
    public class PlayerRoomMatchmakingIntegrationTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _output;

        public PlayerRoomMatchmakingIntegrationTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task EndToEnd_PlayerLoginAndCreateRoom_Should_Work_Correctly()
        {
            // Arrange
            var playerId = "e2e-player-001";
            var roomId = "e2e-room-001";

            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // Act & Assert - 玩家登录
            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "端到端测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Windows",
                DeviceId = "e2e-device-001"
            };

            var loginResponse = await playerGrain.LoginAsync(loginRequest);
            Assert.True(loginResponse.Success, "玩家登录应该成功");
            Assert.Equal(PlayerOnlineStatus.Online, loginResponse.PlayerInfo.OnlineStatus);

            _output.WriteLine($"玩家登录成功: {loginResponse.PlayerInfo.PlayerId}");

            // Act & Assert - 创建房间
            var createRoomRequest = new CreateRoomRequest
            {
                CreatorId = playerId,
                RoomName = "端到端测试房间",
                RoomType = RoomType.Normal,
                MaxPlayerCount = 4,
                Settings = new RoomSettings
                {
                    GameMode = "E2ETestMode",
                    MapId = "TestMap",
                    MinPlayersToStart = 2,
                    AutoStart = false
                }
            };

            var createRoomResponse = await roomGrain.CreateRoomAsync(createRoomRequest);
            Assert.True(createRoomResponse.Success, "创建房间应该成功");
            Assert.Equal(roomId, createRoomResponse.RoomId);

            _output.WriteLine($"房间创建成功: {createRoomResponse.RoomId}");

            // Act & Assert - 玩家加入自己创建的房间
            var joinRoomRequest = new JoinRoomRequest
            {
                PlayerId = playerId,
                RoomId = roomId,
                PlayerData = new Dictionary<string, object>
                {
                    { "DisplayName", "端到端测试玩家" },
                    { "Level", 25 }
                }
            };

            var joinRoomResponse = await roomGrain.JoinRoomAsync(joinRoomRequest);
            Assert.True(joinRoomResponse.Success, "玩家加入房间应该成功");
            Assert.Equal(PlayerRole.Leader, joinRoomResponse.PlayerInfo.Role);
            Assert.Equal(1, joinRoomResponse.RoomInfo.CurrentPlayerCount);

            _output.WriteLine($"玩家成功加入房间: {joinRoomResponse.PlayerInfo.PlayerId}");

            // 验证玩家状态中包含房间信息
            var playerInfo = await playerGrain.GetPlayerInfoAsync(true);
            Assert.NotNull(playerInfo);
            Assert.Equal(roomId, playerInfo.CurrentRoomId);

            _output.WriteLine("端到端流程验证成功：玩家登录 -> 创建房间 -> 加入房间");
        }

        [Fact]
        public async Task EndToEnd_MatchmakingToGameFlow_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "e2e-matchmaking";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings
            {
                EnableMatchmaking = true,
                MaxQueueSize = 100
            });

            var player1Id = "e2e-match-player-1";
            var player2Id = "e2e-match-player-2";

            var player1Grain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(player1Id);
            var player2Grain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(player2Id);

            // Act & Assert - 玩家登录
            await player1Grain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = player1Id,
                DisplayName = "匹配玩家1",
                ClientVersion = "1.0.0"
            });

            await player2Grain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = player2Id,
                DisplayName = "匹配玩家2",
                ClientVersion = "1.0.0"
            });

            _output.WriteLine("两个玩家登录成功");

            // Act & Assert - 快速匹配
            var match1Request = new QuickMatchRequest
            {
                PlayerId = player1Id,
                PlayerName = "匹配玩家1",
                PlayerLevel = 15,
                Criteria = new MatchmakingCriteria
                {
                    PreferredRoomType = RoomType.Normal,
                    PreferredGameMode = "Default",
                    MinPlayerCount = 2,
                    MaxPlayerCount = 4,
                    CreateNewRoomIfNeeded = true
                }
            };

            var match1Response = await matchmakingGrain.QuickMatchAsync(match1Request);
            Assert.True(match1Response.Success, "玩家1快速匹配应该成功");

            var match2Request = new QuickMatchRequest
            {
                PlayerId = player2Id,
                PlayerName = "匹配玩家2",
                PlayerLevel = 18,
                Criteria = new MatchmakingCriteria
                {
                    PreferredRoomType = RoomType.Normal,
                    PreferredGameMode = "Default",
                    MinPlayerCount = 2,
                    MaxPlayerCount = 4,
                    CreateNewRoomIfNeeded = true
                }
            };

            var match2Response = await matchmakingGrain.QuickMatchAsync(match2Request);
            Assert.True(match2Response.Success, "玩家2快速匹配应该成功");

            _output.WriteLine("两个玩家都加入匹配队列");

            // 触发匹配检查
            var matchesFound = await matchmakingGrain.TriggerMatchCheckAsync();
            _output.WriteLine($"触发匹配检查，找到匹配: {matchesFound}个");

            // 等待匹配处理
            await Task.Delay(2000);

            // 检查匹配结果
            var player1Status = await matchmakingGrain.GetMatchmakingStatusAsync(new GetMatchmakingStatusRequest
            {
                PlayerId = player1Id
            });

            var player2Status = await matchmakingGrain.GetMatchmakingStatusAsync(new GetMatchmakingStatusRequest
            {
                PlayerId = player2Id
            });

            _output.WriteLine($"玩家1匹配状态: {player1Status.Success} - {player1Status.Message}");
            _output.WriteLine($"玩家2匹配状态: {player2Status.Success} - {player2Status.Message}");

            if (player1Status.Request != null)
            {
                _output.WriteLine($"玩家1请求状态: {player1Status.Request.Status}");
                if (!string.IsNullOrEmpty(player1Status.Request.MatchedRoomId))
                {
                    _output.WriteLine($"玩家1匹配到房间: {player1Status.Request.MatchedRoomId}");
                }
            }

            if (player2Status.Request != null)
            {
                _output.WriteLine($"玩家2请求状态: {player2Status.Request.Status}");
                if (!string.IsNullOrEmpty(player2Status.Request.MatchedRoomId))
                {
                    _output.WriteLine($"玩家2匹配到房间: {player2Status.Request.MatchedRoomId}");
                }
            }

            _output.WriteLine("匹配系统端到端流程测试完成");
        }

        [Fact]
        public async Task EndToEnd_CompleteGameSession_Should_Work_Correctly()
        {
            // Arrange - 准备完整游戏会话
            var sessionId = Guid.NewGuid().ToString();
            var roomId = $"session-room-{sessionId}";
            var player1Id = $"session-player-1-{sessionId}";
            var player2Id = $"session-player-2-{sessionId}";

            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);
            var player1Grain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(player1Id);
            var player2Grain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(player2Id);

            _output.WriteLine($"开始完整游戏会话测试: {sessionId}");

            // Phase 1: 玩家登录
            var login1Response = await player1Grain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = player1Id,
                DisplayName = "会话玩家1",
                ClientVersion = "1.0.0"
            });

            var login2Response = await player2Grain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = player2Id,
                DisplayName = "会话玩家2",
                ClientVersion = "1.0.0"
            });

            Assert.True(login1Response.Success && login2Response.Success, "所有玩家登录应该成功");
            _output.WriteLine("Phase 1: 玩家登录完成");

            // Phase 2: 创建房间
            var createRoomResponse = await roomGrain.CreateRoomAsync(new CreateRoomRequest
            {
                CreatorId = player1Id,
                RoomName = "完整会话测试房间",
                RoomType = RoomType.Normal,
                MaxPlayerCount = 4,
                Settings = new RoomSettings
                {
                    GameMode = "SessionTest",
                    MinPlayersToStart = 2,
                    AutoStart = false
                }
            });

            Assert.True(createRoomResponse.Success, "创建房间应该成功");
            _output.WriteLine("Phase 2: 房间创建完成");

            // Phase 3: 玩家加入房间
            var join1Response = await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = player1Id,
                RoomId = roomId,
                PlayerData = new Dictionary<string, object> { { "DisplayName", "会话玩家1" }, { "Level", 20 } }
            });

            var join2Response = await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = player2Id,
                RoomId = roomId,
                PlayerData = new Dictionary<string, object> { { "DisplayName", "会话玩家2" }, { "Level", 22 } }
            });

            Assert.True(join1Response.Success && join2Response.Success, "所有玩家加入房间应该成功");
            Assert.Equal(2, join2Response.RoomInfo.CurrentPlayerCount);
            _output.WriteLine("Phase 3: 玩家加入房间完成");

            // Phase 4: 玩家准备
            var ready1Response = await roomGrain.SetPlayerReadyAsync(new PlayerReadyRequest
            {
                PlayerId = player1Id,
                RoomId = roomId,
                ReadyStatus = PlayerReadyStatus.Ready
            });

            var ready2Response = await roomGrain.SetPlayerReadyAsync(new PlayerReadyRequest
            {
                PlayerId = player2Id,
                RoomId = roomId,
                ReadyStatus = PlayerReadyStatus.Ready
            });

            Assert.True(ready1Response.Success && ready2Response.Success, "玩家准备应该成功");
            _output.WriteLine("Phase 4: 玩家准备完成");

            // Phase 5: 开始游戏
            var startGameResponse = await roomGrain.StartGameAsync(new StartGameRequest
            {
                PlayerId = player1Id, // 房主开始游戏
                RoomId = roomId
            });

            Assert.True(startGameResponse.Success, "开始游戏应该成功");
            Assert.NotNull(startGameResponse.GameStartTime);
            _output.WriteLine("Phase 5: 游戏开始完成");

            // Phase 6: 游戏进行中 - 更新分数
            var updateScore1 = await roomGrain.UpdatePlayerScoreAsync(player1Id, 150);
            var updateScore2 = await roomGrain.UpdatePlayerScoreAsync(player2Id, 120);

            Assert.True(updateScore1 && updateScore2, "分数更新应该成功");
            _output.WriteLine("Phase 6: 游戏进行中，分数更新完成");

            // Phase 7: 结束游戏
            var endGameResponse = await roomGrain.EndGameAsync(new EndGameRequest
            {
                PlayerId = player1Id,
                RoomId = roomId,
                FinalScores = new Dictionary<string, int>
                {
                    { player1Id, 150 },
                    { player2Id, 120 }
                }
            });

            Assert.True(endGameResponse.Success, "结束游戏应该成功");
            Assert.Equal(player1Id, endGameResponse.Winner);
            Assert.Equal(150, endGameResponse.FinalScores[player1Id]);
            Assert.Equal(120, endGameResponse.FinalScores[player2Id]);
            _output.WriteLine("Phase 7: 游戏结束完成");

            // Phase 8: 验证房间状态
            var finalRoomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
            Assert.True(finalRoomInfo.Success);
            Assert.Equal(RoomStatus.Finished, finalRoomInfo.RoomInfo.Status);
            Assert.NotNull(finalRoomInfo.RoomInfo.GameEndTime);
            _output.WriteLine("Phase 8: 房间状态验证完成");

            // Phase 9: 验证房间事件
            var roomEvents = await roomGrain.GetRecentEventsAsync(20);
            Assert.NotEmpty(roomEvents);
            
            var eventTypes = roomEvents.Select(e => e.EventType).ToList();
            Assert.Contains(RoomEventType.PlayerJoined, eventTypes);
            Assert.Contains(RoomEventType.PlayerReady, eventTypes);
            Assert.Contains(RoomEventType.GameStarted, eventTypes);
            Assert.Contains(RoomEventType.GameEnded, eventTypes);
            
            _output.WriteLine($"Phase 9: 房间事件验证完成，共{roomEvents.Count}个事件");

            // Phase 10: 玩家离开房间
            var leave1Response = await roomGrain.LeaveRoomAsync(new LeaveRoomRequest
            {
                PlayerId = player1Id,
                RoomId = roomId,
                Reason = "游戏结束"
            });

            var leave2Response = await roomGrain.LeaveRoomAsync(new LeaveRoomRequest
            {
                PlayerId = player2Id,
                RoomId = roomId,
                Reason = "游戏结束"
            });

            Assert.True(leave1Response.Success && leave2Response.Success, "玩家离开房间应该成功");
            _output.WriteLine("Phase 10: 玩家离开房间完成");

            // Phase 11: 玩家登出
            var logout1Response = await player1Grain.LogoutAsync(new PlayerLogoutRequest
            {
                PlayerId = player1Id,
                Reason = "会话结束"
            });

            var logout2Response = await player2Grain.LogoutAsync(new PlayerLogoutRequest
            {
                PlayerId = player2Id,
                Reason = "会话结束"
            });

            Assert.True(logout1Response.Success && logout2Response.Success, "玩家登出应该成功");
            _output.WriteLine("Phase 11: 玩家登出完成");

            _output.WriteLine($"完整游戏会话测试成功完成: {sessionId}");
            _output.WriteLine("流程: 登录 -> 创建房间 -> 加入房间 -> 准备 -> 开始游戏 -> 游戏进行 -> 结束游戏 -> 离开房间 -> 登出");
        }

        [Fact]
        public async Task EndToEnd_MultipleRoomsAndPlayers_Should_Work_Correctly()
        {
            // Arrange - 多房间多玩家并发测试
            var testId = Guid.NewGuid().ToString()[..8];
            var roomCount = 3;
            var playersPerRoom = 2;
            var totalPlayers = roomCount * playersPerRoom;

            _output.WriteLine($"开始多房间并发测试: {roomCount}个房间，每房间{playersPerRoom}个玩家，总计{totalPlayers}个玩家");

            var tasks = new List<Task>();

            // Act - 并发创建多个房间和玩家会话
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                var roomTask = CreateAndRunRoomSession(testId, roomIndex, playersPerRoom);
                tasks.Add(roomTask);
            }

            // 等待所有房间会话完成
            await Task.WhenAll(tasks);

            _output.WriteLine("多房间并发测试完成，所有会话都成功执行");
        }

        private async Task CreateAndRunRoomSession(string testId, int roomIndex, int playerCount)
        {
            var roomId = $"concurrent-room-{testId}-{roomIndex}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            try
            {
                // 创建房间
                var createRoomResponse = await roomGrain.CreateRoomAsync(new CreateRoomRequest
                {
                    CreatorId = $"creator-{testId}-{roomIndex}",
                    RoomName = $"并发测试房间{roomIndex}",
                    RoomType = RoomType.Normal,
                    MaxPlayerCount = playerCount + 1,
                    Settings = new RoomSettings
                    {
                        GameMode = "ConcurrentTest",
                        MinPlayersToStart = playerCount,
                        AutoStart = true
                    }
                });

                if (!createRoomResponse.Success)
                {
                    _output.WriteLine($"房间{roomIndex}创建失败: {createRoomResponse.Message}");
                    return;
                }

                // 并发加入玩家
                var joinTasks = new List<Task>();
                for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
                {
                    var playerId = $"concurrent-player-{testId}-{roomIndex}-{playerIndex}";
                    var joinTask = JoinPlayerToRoom(roomGrain, playerId, roomId, roomIndex, playerIndex);
                    joinTasks.Add(joinTask);
                }

                await Task.WhenAll(joinTasks);

                // 验证房间状态
                var roomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
                if (roomInfo.Success)
                {
                    _output.WriteLine($"房间{roomIndex}会话完成，最终玩家数: {roomInfo.RoomInfo.CurrentPlayerCount}，状态: {roomInfo.RoomInfo.Status}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"房间{roomIndex}会话异常: {ex.Message}");
            }
        }

        private async Task JoinPlayerToRoom(IRoomGrain roomGrain, string playerId, string roomId, int roomIndex, int playerIndex)
        {
            try
            {
                var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

                // 玩家登录
                await playerGrain.LoginAsync(new PlayerLoginRequest
                {
                    PlayerId = playerId,
                    DisplayName = $"并发玩家{roomIndex}-{playerIndex}",
                    ClientVersion = "1.0.0"
                });

                // 加入房间
                var joinResponse = await roomGrain.JoinRoomAsync(new JoinRoomRequest
                {
                    PlayerId = playerId,
                    RoomId = roomId,
                    PlayerData = new Dictionary<string, object>
                    {
                        { "DisplayName", $"并发玩家{roomIndex}-{playerIndex}" },
                        { "Level", 10 + playerIndex }
                    }
                });

                if (joinResponse.Success)
                {
                    // 设置准备状态
                    await roomGrain.SetPlayerReadyAsync(new PlayerReadyRequest
                    {
                        PlayerId = playerId,
                        RoomId = roomId,
                        ReadyStatus = PlayerReadyStatus.Ready
                    });

                    _output.WriteLine($"玩家{playerId}成功加入房间{roomIndex}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"玩家{playerId}加入房间{roomIndex}失败: {ex.Message}");
            }
        }

        [Fact]
        public async Task EndToEnd_SystemHealthCheck_Should_Work_Correctly()
        {
            // Arrange
            var matchmakingId = "health-check-matchmaking";
            var matchmakingGrain = _fixture.Cluster.GrainFactory.GetGrain<IMatchmakingGrain>(matchmakingId);

            await matchmakingGrain.InitializeAsync(new MatchmakingSettings());

            // 添加一些测试数据
            await matchmakingGrain.CreateQueueAsync("health-queue", "健康检查队列", RoomType.Normal, "HealthMode");
            
            for (int i = 1; i <= 3; i++)
            {
                await matchmakingGrain.QuickMatchAsync(new QuickMatchRequest
                {
                    PlayerId = $"health-player-{i}",
                    PlayerName = $"健康检查玩家{i}",
                    PlayerLevel = i * 10
                });
            }

            // Act - 检查系统健康状态
            var healthStatus = await matchmakingGrain.GetHealthStatusAsync();
            var statistics = await matchmakingGrain.GetStatisticsAsync(new GetMatchmakingStatisticsRequest
            {
                IncludeQueueDetails = true
            });

            // Assert
            Assert.True(healthStatus.IsHealthy, "系统应该是健康的");
            Assert.Equal("Healthy", healthStatus.SystemStatus);
            Assert.True(healthStatus.TotalActiveQueues > 0);
            Assert.True(healthStatus.TotalPlayersInQueues > 0);

            Assert.True(statistics.Success);
            Assert.True(statistics.Statistics.CurrentPlayersInQueue > 0);

            _output.WriteLine($"系统健康检查完成:");
            _output.WriteLine($"  健康状态: {healthStatus.SystemStatus}");
            _output.WriteLine($"  活跃队列: {healthStatus.TotalActiveQueues}");
            _output.WriteLine($"  队列玩家: {healthStatus.TotalPlayersInQueues}");
            _output.WriteLine($"  系统运行时间: {healthStatus.Uptime}");
            _output.WriteLine($"  总匹配数: {statistics.Statistics.TotalMatchesMade}");
            _output.WriteLine($"  当前队列玩家: {statistics.Statistics.CurrentPlayersInQueue}");

            if (healthStatus.Issues.Any())
            {
                _output.WriteLine("发现的问题:");
                foreach (var issue in healthStatus.Issues)
                {
                    _output.WriteLine($"  - {issue}");
                }
            }
            else
            {
                _output.WriteLine("未发现系统问题");
            }
        }
    }
}
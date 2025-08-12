using Microsoft.Extensions.Logging;
using Orleans.TestingHost;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Tests.TestFixtures;
using Xunit.Abstractions;

namespace Wind.Tests.RoomGrainTests
{
    /// <summary>
    /// RoomGrain功能验证测试
    /// 验证所有RoomGrain的核心功能是否正常工作
    /// </summary>
    public class RoomGrainFunctionalTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _output;

        public RoomGrainFunctionalTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task RoomGrain_CreateRoom_Should_Work_Correctly()
        {
            // Arrange
            var roomId = $"test-room-{Guid.NewGuid()}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);
            
            var createRequest = new CreateRoomRequest
            {
                CreatorId = "test-creator-001",
                RoomName = "测试房间",
                RoomType = RoomType.Normal,
                MaxPlayerCount = 4,
                Settings = new RoomSettings
                {
                    GameMode = "TestMode",
                    MapId = "TestMap",
                    MinPlayersToStart = 2
                }
            };

            // Act
            var response = await roomGrain.CreateRoomAsync(createRequest);

            // Assert
            Assert.True(response.Success, "房间创建应该成功");
            Assert.Equal(roomId, response.RoomId);
            Assert.NotNull(response.RoomInfo);
            Assert.Equal("test-creator-001", response.RoomInfo.CreatorId);
            Assert.Equal("测试房间", response.RoomInfo.RoomName);
            Assert.Equal(RoomStatus.Waiting, response.RoomInfo.Status);
            Assert.Equal(0, response.RoomInfo.CurrentPlayerCount);

            _output.WriteLine($"房间创建成功: {response.RoomId}");
        }

        [Fact]
        public async Task RoomGrain_JoinRoom_Should_Work_Correctly()
        {
            // Arrange
            var roomId = $"test-room-{Guid.NewGuid()}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // 先创建房间
            var createRequest = new CreateRoomRequest
            {
                CreatorId = "test-creator-002",
                RoomName = "加入测试房间",
                MaxPlayerCount = 4
            };
            var createResponse = await roomGrain.CreateRoomAsync(createRequest);
            Assert.True(createResponse.Success);

            var joinRequest = new JoinRoomRequest
            {
                PlayerId = "test-player-001",
                RoomId = roomId,
                PlayerData = new Dictionary<string, object>
                {
                    { "DisplayName", "测试玩家1" },
                    { "Level", 10 }
                }
            };

            // Act
            var response = await roomGrain.JoinRoomAsync(joinRequest);

            // Assert
            Assert.True(response.Success, "玩家加入房间应该成功");
            Assert.NotNull(response.RoomInfo);
            Assert.NotNull(response.PlayerInfo);
            Assert.Equal("test-player-001", response.PlayerInfo.PlayerId);
            Assert.Equal("测试玩家1", response.PlayerInfo.DisplayName);
            Assert.Equal(10, response.PlayerInfo.Level);
            Assert.Equal(PlayerRole.Leader, response.PlayerInfo.Role); // 第一个加入的玩家是房主
            Assert.Equal(1, response.RoomInfo.CurrentPlayerCount);

            _output.WriteLine($"玩家成功加入房间: {response.PlayerInfo.PlayerId}");
        }

        [Fact]
        public async Task RoomGrain_MultiplePlayersJoin_Should_Work_Correctly()
        {
            // Arrange
            var roomId = $"test-room-{Guid.NewGuid()}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // 创建房间
            var createRequest = new CreateRoomRequest
            {
                CreatorId = "test-creator-003",
                RoomName = "多人测试房间",
                MaxPlayerCount = 4
            };
            await roomGrain.CreateRoomAsync(createRequest);

            // Act & Assert - 多个玩家加入
            for (int i = 1; i <= 3; i++)
            {
                var joinRequest = new JoinRoomRequest
                {
                    PlayerId = $"test-player-{i:D3}",
                    RoomId = roomId,
                    PlayerData = new Dictionary<string, object>
                    {
                        { "DisplayName", $"测试玩家{i}" },
                        { "Level", i * 5 }
                    }
                };

                var response = await roomGrain.JoinRoomAsync(joinRequest);
                
                Assert.True(response.Success, $"玩家{i}加入应该成功");
                Assert.Equal(i, response.RoomInfo.CurrentPlayerCount);
                
                // 第一个玩家是房主，其他是成员
                var expectedRole = i == 1 ? PlayerRole.Leader : PlayerRole.Member;
                Assert.Equal(expectedRole, response.PlayerInfo.Role);

                _output.WriteLine($"玩家{i}成功加入，当前房间人数: {response.RoomInfo.CurrentPlayerCount}");
            }
        }

        [Fact]
        public async Task RoomGrain_LeaveRoom_Should_Work_Correctly()
        {
            // Arrange
            var roomId = $"test-room-{Guid.NewGuid()}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // 创建房间并加入玩家
            await roomGrain.CreateRoomAsync(new CreateRoomRequest
            {
                CreatorId = "test-creator-004",
                RoomName = "离开测试房间",
                MaxPlayerCount = 4
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-player-001",
                RoomId = roomId
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-player-002",
                RoomId = roomId
            });

            var leaveRequest = new LeaveRoomRequest
            {
                PlayerId = "test-player-002",
                RoomId = roomId,
                Reason = "测试离开"
            };

            // Act
            var response = await roomGrain.LeaveRoomAsync(leaveRequest);

            // Assert
            Assert.True(response.Success, "玩家离开房间应该成功");
            
            // 验证房间状态
            var roomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
            Assert.True(roomInfo.Success);
            Assert.Equal(1, roomInfo.RoomInfo.CurrentPlayerCount);
            Assert.Equal("test-player-001", roomInfo.RoomInfo.Players[0].PlayerId);

            _output.WriteLine($"玩家成功离开房间，剩余人数: {roomInfo.RoomInfo.CurrentPlayerCount}");
        }

        [Fact]
        public async Task RoomGrain_LeaderTransfer_Should_Work_When_Leader_Leaves()
        {
            // Arrange
            var roomId = $"test-room-{Guid.NewGuid()}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // 创建房间并加入多个玩家
            await roomGrain.CreateRoomAsync(new CreateRoomRequest
            {
                CreatorId = "test-creator-005",
                RoomName = "房主转移测试",
                MaxPlayerCount = 4
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-leader",
                RoomId = roomId
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-member",
                RoomId = roomId
            });

            // Act - 房主离开
            await roomGrain.LeaveRoomAsync(new LeaveRoomRequest
            {
                PlayerId = "test-leader",
                RoomId = roomId
            });

            // Assert - 验证房主权限转移
            var roomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
            Assert.True(roomInfo.Success);
            Assert.Equal(1, roomInfo.RoomInfo.CurrentPlayerCount);
            
            var newLeader = roomInfo.RoomInfo.Players[0];
            Assert.Equal("test-member", newLeader.PlayerId);
            Assert.Equal(PlayerRole.Leader, newLeader.Role);

            _output.WriteLine($"房主权限成功转移给: {newLeader.PlayerId}");
        }

        [Fact]
        public async Task RoomGrain_PlayerReady_Should_Work_Correctly()
        {
            // Arrange
            var roomId = $"test-room-{Guid.NewGuid()}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // 创建房间并加入玩家
            await roomGrain.CreateRoomAsync(new CreateRoomRequest
            {
                CreatorId = "test-creator-006",
                RoomName = "准备测试房间",
                MaxPlayerCount = 4
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-player-ready",
                RoomId = roomId
            });

            var readyRequest = new PlayerReadyRequest
            {
                PlayerId = "test-player-ready",
                RoomId = roomId,
                ReadyStatus = PlayerReadyStatus.Ready
            };

            // Act
            var response = await roomGrain.SetPlayerReadyAsync(readyRequest);

            // Assert
            Assert.True(response.Success, "设置玩家准备状态应该成功");
            Assert.Equal(PlayerReadyStatus.Ready, response.ReadyStatus);

            // 验证房间中玩家状态
            var roomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
            var player = roomInfo.RoomInfo.Players[0];
            Assert.Equal(PlayerReadyStatus.Ready, player.ReadyStatus);

            _output.WriteLine($"玩家准备状态设置成功: {player.ReadyStatus}");
        }

        [Fact]
        public async Task RoomGrain_StartGame_Should_Work_With_Ready_Players()
        {
            // Arrange
            var roomId = $"test-room-{Guid.NewGuid()}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // 创建房间
            await roomGrain.CreateRoomAsync(new CreateRoomRequest
            {
                CreatorId = "test-creator-007",
                RoomName = "游戏开始测试",
                MaxPlayerCount = 4,
                Settings = new RoomSettings
                {
                    MinPlayersToStart = 2
                }
            });

            // 加入两个玩家
            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-player-1",
                RoomId = roomId
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-player-2",
                RoomId = roomId
            });

            // 设置玩家准备
            await roomGrain.SetPlayerReadyAsync(new PlayerReadyRequest
            {
                PlayerId = "test-player-1",
                RoomId = roomId,
                ReadyStatus = PlayerReadyStatus.Ready
            });

            await roomGrain.SetPlayerReadyAsync(new PlayerReadyRequest
            {
                PlayerId = "test-player-2",
                RoomId = roomId,
                ReadyStatus = PlayerReadyStatus.Ready
            });

            var startRequest = new StartGameRequest
            {
                PlayerId = "test-player-1", // 房主开始游戏
                RoomId = roomId
            };

            // Act
            var response = await roomGrain.StartGameAsync(startRequest);

            // Assert
            Assert.True(response.Success, "开始游戏应该成功");
            Assert.NotNull(response.GameStartTime);
            Assert.NotNull(response.GameState);

            // 验证房间状态
            var roomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
            Assert.Equal(RoomStatus.InGame, roomInfo.RoomInfo.Status);
            Assert.Equal(1, roomInfo.RoomInfo.GameState.RoundNumber);

            _output.WriteLine($"游戏成功开始，状态: {roomInfo.RoomInfo.Status}");
        }

        [Fact]
        public async Task RoomGrain_EndGame_Should_Work_Correctly()
        {
            // Arrange
            var roomId = $"test-room-{Guid.NewGuid()}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // 创建房间、加入玩家并开始游戏
            await roomGrain.CreateRoomAsync(new CreateRoomRequest
            {
                CreatorId = "test-creator-008",
                RoomName = "游戏结束测试",
                MaxPlayerCount = 4
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-player-1",
                RoomId = roomId
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-player-2",
                RoomId = roomId
            });

            // 强制开始游戏（跳过准备检查）
            await roomGrain.StartGameAsync(new StartGameRequest
            {
                PlayerId = "test-player-1",
                RoomId = roomId,
                ForceStart = true
            });

            var endRequest = new EndGameRequest
            {
                PlayerId = "test-player-1",
                RoomId = roomId,
                FinalScores = new Dictionary<string, int>
                {
                    { "test-player-1", 100 },
                    { "test-player-2", 80 }
                }
            };

            // Act
            var response = await roomGrain.EndGameAsync(endRequest);

            // Assert
            Assert.True(response.Success, "结束游戏应该成功");
            Assert.NotNull(response.GameEndTime);
            Assert.Equal("test-player-1", response.Winner);
            Assert.Equal(100, response.FinalScores["test-player-1"]);
            Assert.Equal(80, response.FinalScores["test-player-2"]);

            // 验证房间状态
            var roomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
            Assert.Equal(RoomStatus.Finished, roomInfo.RoomInfo.Status);

            _output.WriteLine($"游戏成功结束，获胜者: {response.Winner}");
        }

        [Fact]
        public async Task RoomGrain_KickPlayer_Should_Work_Correctly()
        {
            // Arrange
            var roomId = $"test-room-{Guid.NewGuid()}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // 创建房间并加入玩家
            await roomGrain.CreateRoomAsync(new CreateRoomRequest
            {
                CreatorId = "test-creator-009",
                RoomName = "踢人测试房间",
                MaxPlayerCount = 4
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-leader",
                RoomId = roomId
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-target",
                RoomId = roomId
            });

            var kickRequest = new KickPlayerRequest
            {
                RoomId = roomId,
                OperatorId = "test-leader", // 房主踢人
                TargetPlayerId = "test-target",
                Reason = "测试踢出"
            };

            // Act
            var response = await roomGrain.KickPlayerAsync(kickRequest);

            // Assert
            Assert.True(response.Success, "踢出玩家应该成功");

            // 验证玩家已被移除
            var roomInfo = await roomGrain.GetRoomInfoAsync(new GetRoomInfoRequest { RoomId = roomId });
            Assert.Equal(1, roomInfo.RoomInfo.CurrentPlayerCount);
            Assert.DoesNotContain(roomInfo.RoomInfo.Players, p => p.PlayerId == "test-target");

            _output.WriteLine("玩家成功被踢出房间");
        }

        [Fact]
        public async Task RoomGrain_UpdateRoomSettings_Should_Work_Correctly()
        {
            // Arrange
            var roomId = $"test-room-{Guid.NewGuid()}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // 创建房间
            await roomGrain.CreateRoomAsync(new CreateRoomRequest
            {
                CreatorId = "test-creator-010",
                RoomName = "设置更新测试",
                MaxPlayerCount = 4
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-leader",
                RoomId = roomId
            });

            var updateRequest = new UpdateRoomSettingsRequest
            {
                RoomId = roomId,
                PlayerId = "test-leader",
                Settings = new RoomSettings
                {
                    GameMode = "UpdatedMode",
                    MapId = "UpdatedMap",
                    GameDuration = 600,
                    MaxScore = 200
                }
            };

            // Act
            var response = await roomGrain.UpdateRoomSettingsAsync(updateRequest);

            // Assert
            Assert.True(response.Success, "更新房间设置应该成功");
            Assert.NotNull(response.UpdatedSettings);
            Assert.Equal("UpdatedMode", response.UpdatedSettings.GameMode);
            Assert.Equal("UpdatedMap", response.UpdatedSettings.MapId);
            Assert.Equal(600, response.UpdatedSettings.GameDuration);
            Assert.Equal(200, response.UpdatedSettings.MaxScore);

            _output.WriteLine("房间设置更新成功");
        }

        [Fact]
        public async Task RoomGrain_GetRoomEvents_Should_Work_Correctly()
        {
            // Arrange
            var roomId = $"test-room-{Guid.NewGuid()}";
            var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(roomId);

            // 创建房间并进行一些操作来生成事件
            await roomGrain.CreateRoomAsync(new CreateRoomRequest
            {
                CreatorId = "test-creator-011",
                RoomName = "事件测试房间",
                MaxPlayerCount = 4
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-player-1",
                RoomId = roomId
            });

            await roomGrain.JoinRoomAsync(new JoinRoomRequest
            {
                PlayerId = "test-player-2",
                RoomId = roomId
            });

            await roomGrain.LeaveRoomAsync(new LeaveRoomRequest
            {
                PlayerId = "test-player-2",
                RoomId = roomId
            });

            // Act
            var events = await roomGrain.GetRecentEventsAsync(10);

            // Assert
            Assert.NotEmpty(events);
            Assert.Contains(events, e => e.EventType == RoomEventType.PlayerJoined);
            Assert.Contains(events, e => e.EventType == RoomEventType.PlayerLeft);

            _output.WriteLine($"房间事件记录数量: {events.Count}");
            foreach (var evt in events)
            {
                _output.WriteLine($"事件: {evt.EventType} - {evt.Description} - {evt.Timestamp}");
            }
        }
    }
}
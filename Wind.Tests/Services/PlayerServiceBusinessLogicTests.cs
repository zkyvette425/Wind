using Microsoft.Extensions.Logging;
using Orleans.TestingHost;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.GrainInterfaces;
using Wind.Tests.TestFixtures;
using Xunit.Abstractions;
using Orleans;

namespace Wind.Tests.Services
{
    /// <summary>
    /// PlayerService业务逻辑测试
    /// 通过直接调用PlayerGrain验证PlayerService所依赖的核心业务逻辑
    /// </summary>
    public class PlayerServiceBusinessLogicTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _output;

        public PlayerServiceBusinessLogicTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task LoginAsync_Should_Work_Successfully()
        {
            // Arrange
            var playerId = $"api-test-player-{Guid.NewGuid()}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "API测试玩家",
                ClientVersion = "1.0.0",
                Platform = "TestPlatform",
                DeviceId = "test-device-001"
            };

            // Act
            var response = await playerGrain.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"登录失败: {response.Message}");
            Assert.Equal(playerId, response.PlayerInfo?.PlayerId);
            Assert.Equal("API测试玩家", response.PlayerInfo?.DisplayName);
            Assert.NotNull(response.SessionId);
            Assert.NotNull(response.AuthToken);

            _output.WriteLine($"登录成功: PlayerId={response.PlayerInfo?.PlayerId}, SessionId={response.SessionId}");
        }

        [Fact]
        public async Task GetPlayerInfoAsync_Should_Return_Player_Info()
        {
            // Arrange
            var playerId = $"api-test-player-{Guid.NewGuid()}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 先登录玩家
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "测试玩家",
                ClientVersion = "1.0.0"
            });

            // Act
            var playerInfo = await playerGrain.GetPlayerInfoAsync(true, false);

            // Assert
            Assert.NotNull(playerInfo);
            Assert.Equal(playerId, playerInfo.PlayerId);
            Assert.Equal("测试玩家", playerInfo.DisplayName);
            Assert.Equal(PlayerOnlineStatus.Online, playerInfo.OnlineStatus);

            _output.WriteLine($"获取玩家信息成功: PlayerId={playerInfo.PlayerId}, Status={playerInfo.OnlineStatus}");
        }

        [Fact]
        public async Task UpdatePlayerPositionAsync_Should_Update_Position()
        {
            // Arrange
            var playerId = $"api-test-player-{Guid.NewGuid()}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 先登录玩家
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "位置测试玩家",
                ClientVersion = "1.0.0"
            });

            var newPosition = new PlayerPosition
            {
                X = 100.5f,
                Y = 200.3f,
                Z = 50.0f,
                MapId = "test-map",
                Rotation = 45.0f
            };

            // Act
            var success = await playerGrain.UpdatePositionAsync(newPosition);

            // Assert
            Assert.True(success);

            // 验证位置是否正确更新
            var playerInfo = await playerGrain.GetPlayerInfoAsync(false, false);
            Assert.NotNull(playerInfo);
            Assert.Equal(100.5f, playerInfo.Position.X);
            Assert.Equal(200.3f, playerInfo.Position.Y);
            Assert.Equal("test-map", playerInfo.Position.MapId);

            _output.WriteLine($"位置更新成功: ({playerInfo.Position.X}, {playerInfo.Position.Y}, {playerInfo.Position.Z})");
        }

        [Fact]
        public async Task SetOnlineStatusAsync_Should_Update_Status()
        {
            // Arrange
            var playerId = $"api-test-player-{Guid.NewGuid()}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 先登录玩家
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "状态测试玩家",
                ClientVersion = "1.0.0"
            });

            // Act
            var success = await playerGrain.SetOnlineStatusAsync(PlayerOnlineStatus.Away);

            // Assert
            Assert.True(success);

            // 验证状态是否正确更新
            var playerInfo = await playerGrain.GetPlayerInfoAsync(false, false);
            Assert.NotNull(playerInfo);
            Assert.Equal(PlayerOnlineStatus.Away, playerInfo.OnlineStatus);

            _output.WriteLine($"在线状态设置成功: {playerInfo.OnlineStatus}");
        }

        [Fact]
        public async Task JoinRoomAsync_Should_Join_Room_Successfully()
        {
            // Arrange
            var playerId = $"api-test-player-{Guid.NewGuid()}";
            var roomId = $"api-test-room-{Guid.NewGuid()}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 先登录玩家
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "房间测试玩家",
                ClientVersion = "1.0.0"
            });

            // Act
            var success = await playerGrain.JoinRoomAsync(roomId);

            // Assert
            Assert.True(success);

            // 验证房间ID是否正确设置
            var currentRoom = await playerGrain.GetCurrentRoomAsync();
            Assert.Equal(roomId, currentRoom);

            _output.WriteLine($"加入房间成功: RoomId={currentRoom}");
        }

        [Fact]
        public async Task LeaveRoomAsync_Should_Leave_Room_Successfully()
        {
            // Arrange
            var playerId = $"api-test-player-{Guid.NewGuid()}";
            var roomId = $"api-test-room-{Guid.NewGuid()}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 先登录玩家并加入房间
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "离开房间测试玩家",
                ClientVersion = "1.0.0"
            });

            await playerGrain.JoinRoomAsync(roomId);

            // Act
            var success = await playerGrain.LeaveRoomAsync();

            // Assert
            Assert.True(success);

            // 验证房间ID是否被清空
            var currentRoom = await playerGrain.GetCurrentRoomAsync();
            Assert.Null(currentRoom);

            _output.WriteLine($"离开房间成功: CurrentRoom={currentRoom ?? "null"}");
        }

        [Fact]
        public async Task IsOnlineAsync_Should_Return_Online_Status()
        {
            // Arrange
            var playerId = $"api-test-player-{Guid.NewGuid()}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 先登录玩家
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "在线检查测试玩家",
                ClientVersion = "1.0.0"
            });

            // Act
            var isOnline = await playerGrain.IsOnlineAsync();
            var lastActiveTime = await playerGrain.GetLastActiveTimeAsync();

            // Assert
            Assert.True(isOnline);
            Assert.True(lastActiveTime > DateTime.MinValue);

            _output.WriteLine($"在线状态检查成功: IsOnline={isOnline}, LastActiveTime={lastActiveTime}");
        }

        [Fact]
        public async Task HeartbeatAsync_Should_Update_Last_Active_Time()
        {
            // Arrange
            var playerId = $"api-test-player-{Guid.NewGuid()}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 先登录玩家
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "心跳测试玩家",
                ClientVersion = "1.0.0"
            });

            // 获取初始的最后活跃时间
            var initialTime = await playerGrain.GetLastActiveTimeAsync();

            // 等待一小段时间
            await Task.Delay(100);

            // Act
            var success = await playerGrain.HeartbeatAsync();

            // Assert
            Assert.True(success);

            var updatedTime = await playerGrain.GetLastActiveTimeAsync();
            Assert.True(updatedTime > initialTime);

            _output.WriteLine($"心跳更新成功: InitialTime={initialTime}, UpdatedTime={updatedTime}");
        }

        [Fact]
        public async Task ValidateSessionAsync_Should_Validate_Session()
        {
            // Arrange
            var playerId = $"api-test-player-{Guid.NewGuid()}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 先登录玩家获取会话ID
            var loginResponse = await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "会话验证测试玩家",
                ClientVersion = "1.0.0"
            });

            var sessionId = loginResponse.SessionId!;

            // Act
            var isValid = await playerGrain.ValidateSessionAsync(sessionId);

            // Assert
            Assert.True(isValid);

            _output.WriteLine($"会话验证成功: SessionId={sessionId}, IsValid={isValid}");
        }

        [Fact]
        public async Task LogoutAsync_Should_Logout_Successfully()
        {
            // Arrange
            var playerId = $"api-test-player-{Guid.NewGuid()}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 先登录玩家
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "登出测试玩家",
                ClientVersion = "1.0.0"
            });

            // Act
            var response = await playerGrain.LogoutAsync(new PlayerLogoutRequest
            {
                PlayerId = playerId,
                Reason = "测试登出"
            });

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success, $"登出失败: {response.Message}");

            // 验证玩家已离线
            var isOnline = await playerGrain.IsOnlineAsync();
            Assert.False(isOnline);

            _output.WriteLine($"登出成功: PlayerId={playerId}");
        }

        [Fact]
        public async Task PlayerService_Should_Handle_Invalid_Parameters()
        {
            // Arrange - 测试PlayerGrain本身的业务逻辑验证
            var playerId = $"test-validation-{Guid.NewGuid()}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // Act & Assert - 测试空DisplayName或其他业务验证
            var loginResponse = await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId, // 有效的PlayerId
                DisplayName = "", // 空的DisplayName
                ClientVersion = "1.0.0"
            });

            // PlayerGrain应该处理业务逻辑，我们主要验证它不会崩溃
            Assert.NotNull(loginResponse);
            // 根据实际的PlayerGrain实现，这可能成功也可能失败
            // 重要的是系统不应该崩溃

            _output.WriteLine($"参数验证测试通过: Success={loginResponse.Success}, Message={loginResponse.Message}");
        }

        [Fact]
        public async Task PlayerService_Should_Support_Concurrent_Operations()
        {
            // Arrange
            var playerCount = 5;
            var tasks = new List<Task<PlayerLoginResponse>>();

            // Act - 并发登录多个玩家
            for (int i = 0; i < playerCount; i++)
            {
                var playerId = $"concurrent-player-{i}-{Guid.NewGuid()}";
                var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
                var task = playerGrain.LoginAsync(new PlayerLoginRequest
                {
                    PlayerId = playerId,
                    DisplayName = $"并发测试玩家{i}",
                    ClientVersion = "1.0.0"
                });
                tasks.Add(task);
            }

            var responses = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(playerCount, responses.Length);
            foreach (var response in responses)
            {
                Assert.NotNull(response);
                Assert.True(response.Success, $"并发登录失败: {response.Message}");
                Assert.NotNull(response.PlayerInfo);
            }

            _output.WriteLine($"并发测试成功: {playerCount}个玩家同时登录");
        }
    }
}
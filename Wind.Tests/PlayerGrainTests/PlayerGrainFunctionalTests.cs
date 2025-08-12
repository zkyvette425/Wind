using Microsoft.Extensions.Logging;
using Orleans.TestingHost;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Tests.TestFixtures;
using Xunit.Abstractions;

namespace Wind.Tests.PlayerGrainTests
{
    /// <summary>
    /// PlayerGrain功能验证测试
    /// 验证所有PlayerGrain的核心功能是否正常工作
    /// </summary>
    public class PlayerGrainFunctionalTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _output;

        public PlayerGrainFunctionalTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task PlayerGrain_Login_Should_Work_Correctly()
        {
            // Arrange
            var playerId = "test-player-001";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
            
            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Windows",
                DeviceId = "test-device-001"
            };

            // Act
            var response = await playerGrain.LoginAsync(loginRequest);

            // Assert
            Assert.True(response.Success, "登录应该成功");
            Assert.NotNull(response.SessionId);
            Assert.NotNull(response.AuthToken);
            Assert.NotNull(response.PlayerInfo);
            Assert.Equal(playerId, response.PlayerInfo.PlayerId);
            Assert.Equal("测试玩家", response.PlayerInfo.DisplayName);
            Assert.Equal(PlayerOnlineStatus.Online, response.PlayerInfo.OnlineStatus);
            
            _output.WriteLine($"✅ 玩家登录成功: {response.PlayerInfo.PlayerId}");
        }

        [Fact]
        public async Task PlayerGrain_GetPlayerInfo_Should_Return_Valid_Data()
        {
            // Arrange
            var playerId = "test-player-002";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
            
            // 先登录
            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "信息测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Windows",
                DeviceId = "test-device-002"
            };
            await playerGrain.LoginAsync(loginRequest);

            // Act
            var playerInfo = await playerGrain.GetPlayerInfoAsync(includeStats: true, includeSettings: false);

            // Assert
            Assert.NotNull(playerInfo);
            Assert.Equal(playerId, playerInfo.PlayerId);
            Assert.Equal("信息测试玩家", playerInfo.DisplayName);
            Assert.Equal(PlayerOnlineStatus.Online, playerInfo.OnlineStatus);
            Assert.NotNull(playerInfo.Stats);
            Assert.NotNull(playerInfo.Position);
            
            _output.WriteLine($"✅ 获取玩家信息成功: {playerInfo.PlayerId} - {playerInfo.DisplayName}");
        }

        [Fact]
        public async Task PlayerGrain_UpdatePlayer_Should_Handle_Version_Control()
        {
            // Arrange
            var playerId = "test-player-003";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
            
            // 先登录
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "版本控制测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Windows",
                DeviceId = "test-device-003"
            });

            var playerInfo = await playerGrain.GetPlayerInfoAsync();
            var currentVersion = playerInfo?.Stats != null ? 1 : 0; // 简化版本获取

            // Act - 正确版本的更新
            var updateRequest = new PlayerUpdateRequest
            {
                DisplayName = "更新后的名称",
                Version = currentVersion,
                OnlineStatus = PlayerOnlineStatus.Away
            };
            
            var response = await playerGrain.UpdatePlayerAsync(updateRequest);

            // Assert
            Assert.True(response.Success, "正确版本的更新应该成功");
            Assert.NotNull(response.UpdatedPlayerInfo);
            Assert.Equal("更新后的名称", response.UpdatedPlayerInfo.DisplayName);
            Assert.Equal(PlayerOnlineStatus.Away, response.UpdatedPlayerInfo.OnlineStatus);
            
            _output.WriteLine($"✅ 玩家信息更新成功，新版本: {response.NewVersion}");

            // Act - 错误版本的更新（应该失败）
            var badUpdateRequest = new PlayerUpdateRequest
            {
                DisplayName = "这个更新应该失败",
                Version = currentVersion, // 使用旧版本号
                OnlineStatus = PlayerOnlineStatus.Busy
            };
            
            var badResponse = await playerGrain.UpdatePlayerAsync(badUpdateRequest);

            // Assert
            Assert.False(badResponse.Success, "错误版本的更新应该失败");
            Assert.Contains("版本不匹配", badResponse.Message);
            
            _output.WriteLine($"✅ 版本控制正常工作: {badResponse.Message}");
        }

        [Fact]
        public async Task PlayerGrain_Position_Update_Should_Work()
        {
            // Arrange
            var playerId = "test-player-004";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
            
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "位置测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Windows",
                DeviceId = "test-device-004"
            });

            // Act
            var newPosition = new PlayerPosition
            {
                X = 100.5f,
                Y = 200.3f,
                Z = 50.7f,
                Rotation = 45.0f,
                MapId = "test-map-001"
            };

            var result = await playerGrain.UpdatePositionAsync(newPosition);

            // Assert
            Assert.True(result, "位置更新应该成功");

            var playerInfo = await playerGrain.GetPlayerInfoAsync();
            Assert.NotNull(playerInfo?.Position);
            Assert.Equal(100.5f, playerInfo.Position.X);
            Assert.Equal(200.3f, playerInfo.Position.Y);
            Assert.Equal(50.7f, playerInfo.Position.Z);
            Assert.Equal(45.0f, playerInfo.Position.Rotation);
            Assert.Equal("test-map-001", playerInfo.Position.MapId);
            
            _output.WriteLine($"✅ 位置更新成功: ({newPosition.X}, {newPosition.Y}, {newPosition.Z})");
        }

        [Fact]
        public async Task PlayerGrain_Room_Management_Should_Work()
        {
            // Arrange
            var playerId = "test-player-005";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
            
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "房间测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Windows",
                DeviceId = "test-device-005"
            });

            // Act & Assert - 加入房间
            var roomId = "test-room-001";
            var joinResult = await playerGrain.JoinRoomAsync(roomId);
            Assert.True(joinResult, "加入房间应该成功");

            var currentRoom = await playerGrain.GetCurrentRoomAsync();
            Assert.Equal(roomId, currentRoom);
            
            var playerInfo = await playerGrain.GetPlayerInfoAsync();
            Assert.Equal(PlayerOnlineStatus.InGame, playerInfo?.OnlineStatus);
            
            _output.WriteLine($"✅ 加入房间成功: {roomId}");

            // Act & Assert - 离开房间
            var leaveResult = await playerGrain.LeaveRoomAsync();
            Assert.True(leaveResult, "离开房间应该成功");

            currentRoom = await playerGrain.GetCurrentRoomAsync();
            Assert.Null(currentRoom);
            
            playerInfo = await playerGrain.GetPlayerInfoAsync();
            Assert.Equal(PlayerOnlineStatus.Online, playerInfo?.OnlineStatus);
            
            _output.WriteLine($"✅ 离开房间成功");
        }

        [Fact]
        public async Task PlayerGrain_Session_Validation_Should_Work()
        {
            // Arrange
            var playerId = "test-player-006";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
            
            var loginResponse = await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "会话测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Windows",
                DeviceId = "test-device-006"
            });

            // Act & Assert - 有效会话
            var validSession = await playerGrain.ValidateSessionAsync(loginResponse.SessionId!);
            Assert.True(validSession, "有效的会话ID应该通过验证");
            
            _output.WriteLine($"✅ 会话验证成功: {loginResponse.SessionId}");

            // Act & Assert - 无效会话
            var invalidSession = await playerGrain.ValidateSessionAsync("invalid-session-id");
            Assert.False(invalidSession, "无效的会话ID应该验证失败");
            
            _output.WriteLine($"✅ 无效会话正确被拒绝");
        }

        [Fact]
        public async Task PlayerGrain_Logout_Should_Work()
        {
            // Arrange
            var playerId = "test-player-007";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
            
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "登出测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Windows",
                DeviceId = "test-device-007"
            });

            // Act
            var logoutResponse = await playerGrain.LogoutAsync(new PlayerLogoutRequest
            {
                Reason = "用户主动登出"
            });

            // Assert
            Assert.True(logoutResponse.Success, "登出应该成功");
            
            var playerInfo = await playerGrain.GetPlayerInfoAsync();
            Assert.Equal(PlayerOnlineStatus.Offline, playerInfo?.OnlineStatus);
            
            _output.WriteLine($"✅ 玩家登出成功: {logoutResponse.Message}");
        }

        [Fact]
        public async Task PlayerGrain_Heartbeat_Should_Update_Activity()
        {
            // Arrange
            var playerId = "test-player-008";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
            
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "心跳测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Windows",
                DeviceId = "test-device-008"
            });

            var initialTime = await playerGrain.GetLastActiveTimeAsync();
            
            // 等待一小段时间确保时间差异
            await Task.Delay(100);

            // Act
            var heartbeatResult = await playerGrain.HeartbeatAsync();
            
            // Assert
            Assert.True(heartbeatResult, "心跳更新应该成功");
            
            var newTime = await playerGrain.GetLastActiveTimeAsync();
            Assert.True(newTime > initialTime, "心跳后活跃时间应该更新");
            
            _output.WriteLine($"✅ 心跳更新成功: {initialTime} -> {newTime}");
        }
    }
}
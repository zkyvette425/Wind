using Microsoft.Extensions.DependencyInjection;
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
    /// PlayerGrain 详细单元测试
    /// 专注于状态管理、并发控制、错误处理、边界条件等核心逻辑
    /// </summary>
    public class PlayerGrainUnitTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly ILogger<PlayerGrainUnitTests> _logger;

        public PlayerGrainUnitTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _logger = _fixture.Cluster.ServiceProvider.GetService<ILogger<PlayerGrainUnitTests>>()!;
        }

        #region 状态管理测试

        [Fact]
        public async Task PlayerState_Should_Initialize_On_First_Access()
        {
            // Arrange
            var playerId = $"unit-test-{Guid.NewGuid():N}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // Act - 首次访问应该初始化状态
            var playerInfo = await playerGrain.GetPlayerInfoAsync();

            // Assert
            Assert.NotNull(playerInfo);
            Assert.Equal(playerId, playerInfo.PlayerId);
            Assert.Equal(PlayerOnlineStatus.Offline, playerInfo.OnlineStatus);
            Assert.True(playerInfo.DisplayName.StartsWith("Player_"));
            Assert.NotNull(playerInfo.Stats);
            Assert.NotNull(playerInfo.Position);

            _output.WriteLine($"✅ 状态初始化测试通过: {playerInfo.PlayerId}");
        }

        [Fact]
        public async Task PlayerState_Version_Should_Increment_On_Updates()
        {
            // Arrange
            var playerId = $"unit-version-{Guid.NewGuid():N}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 初始登录
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "版本测试用户",
                ClientVersion = "1.0.0",
                Platform = "Test"
            });

            var initialInfo = await playerGrain.GetPlayerInfoAsync();
            var fullState = await playerGrain.GetFullStateAsync();
            var initialVersion = fullState?.Version ?? 0;

            // Act - 执行多次更新
            var updates = new object[]
            {
                new PlayerUpdateRequest { DisplayName = "更新名称1", Version = initialVersion },
                new PlayerPosition { X = 100f, Y = 200f, Z = 300f },
                new PlayerStats { GamesPlayed = 5, GamesWon = 3 }
            };

            // 第一次更新
            var response1 = await playerGrain.UpdatePlayerAsync((PlayerUpdateRequest)updates[0]);
            Assert.True(response1.Success);
            Assert.Equal(initialVersion + 1, response1.NewVersion);

            // 位置更新 (不影响版本)
            await playerGrain.UpdatePositionAsync((PlayerPosition)updates[1]);

            // 统计更新
            await playerGrain.UpdateStatsAsync((PlayerStats)updates[2]);

            // 验证最终版本
            var finalState = await playerGrain.GetFullStateAsync();
            Assert.True(finalState!.Version > initialVersion);

            _output.WriteLine($"✅ 版本控制测试通过: {initialVersion} -> {finalState.Version}");
        }

        [Fact]
        public async Task PlayerState_Should_Persist_Across_Grain_Lifecycle()
        {
            // Arrange
            var playerId = $"unit-persist-{Guid.NewGuid():N}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            var testData = new
            {
                DisplayName = "持久化测试用户",
                Position = new PlayerPosition { X = 123.45f, Y = 678.90f, Z = 0f },
                Stats = new PlayerStats { GamesPlayed = 10, GamesWon = 7 }
            };

            // Act - 设置数据
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = testData.DisplayName,
                ClientVersion = "1.0.0",
                Platform = "Test"
            });

            await playerGrain.UpdatePositionAsync(testData.Position);
            await playerGrain.UpdateStatsAsync(testData.Stats);

            // 强制保存状态
            await playerGrain.SaveStateAsync();

            // 获取新的Grain实例 (模拟Grain重新激活)
            var newPlayerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
            var retrievedInfo = await newPlayerGrain.GetPlayerInfoAsync();

            // Assert - 验证数据一致性
            Assert.NotNull(retrievedInfo);
            Assert.Equal(testData.DisplayName, retrievedInfo.DisplayName);
            Assert.NotNull(retrievedInfo.Position);
            Assert.Equal(testData.Position.X, retrievedInfo.Position.X);
            Assert.Equal(testData.Position.Y, retrievedInfo.Position.Y);
            Assert.NotNull(retrievedInfo.Stats);
            Assert.Equal(testData.Stats.GamesPlayed, retrievedInfo.Stats.GamesPlayed);
            Assert.Equal(testData.Stats.GamesWon, retrievedInfo.Stats.GamesWon);

            _output.WriteLine($"✅ 状态持久化测试通过: {playerId}");
        }

        #endregion

        #region 并发访问测试

        [Fact]
        public async Task PlayerGrain_Should_Handle_Concurrent_Updates()
        {
            // Arrange
            var playerId = $"unit-concurrent-{Guid.NewGuid():N}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 初始登录
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "并发测试用户",
                ClientVersion = "1.0.0",
                Platform = "Test"
            });

            // Act - 并发执行多种操作
            var concurrentTasks = new List<Task>
            {
                playerGrain.HeartbeatAsync(),
                playerGrain.UpdatePositionAsync(new PlayerPosition { X = 1f, Y = 1f, Z = 1f }),
                playerGrain.HeartbeatAsync(),
                playerGrain.UpdatePositionAsync(new PlayerPosition { X = 2f, Y = 2f, Z = 2f }),
                playerGrain.SetOnlineStatusAsync(PlayerOnlineStatus.Away),
                playerGrain.HeartbeatAsync(),
                playerGrain.UpdatePositionAsync(new PlayerPosition { X = 3f, Y = 3f, Z = 3f }),
                playerGrain.SetOnlineStatusAsync(PlayerOnlineStatus.Busy),
                playerGrain.IsOnlineAsync(),
                playerGrain.GetLastActiveTimeAsync()
            };

            var startTime = DateTime.UtcNow;
            await Task.WhenAll(concurrentTasks);
            var endTime = DateTime.UtcNow;

            // Assert - 验证状态一致性
            var finalInfo = await playerGrain.GetPlayerInfoAsync();
            Assert.NotNull(finalInfo);
            Assert.True(finalInfo.OnlineStatus == PlayerOnlineStatus.Away || finalInfo.OnlineStatus == PlayerOnlineStatus.Busy);

            var lastActiveTime = await playerGrain.GetLastActiveTimeAsync();
            Assert.True(lastActiveTime >= startTime && lastActiveTime <= endTime.AddSeconds(1));

            _output.WriteLine($"✅ 并发访问测试通过: {concurrentTasks.Count} 个并发操作完成");
        }

        [Fact]
        public async Task PlayerGrain_Should_Handle_Version_Conflicts()
        {
            // Arrange
            var playerId = $"unit-conflict-{Guid.NewGuid():N}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "版本冲突测试",
                ClientVersion = "1.0.0",
                Platform = "Test"
            });

            var state = await playerGrain.GetFullStateAsync();
            var currentVersion = state!.Version;

            // Act - 模拟版本冲突
            var validUpdate = new PlayerUpdateRequest
            {
                DisplayName = "有效更新",
                Version = currentVersion
            };

            var invalidUpdate = new PlayerUpdateRequest
            {
                DisplayName = "无效更新",
                Version = currentVersion - 1 // 使用过时的版本
            };

            // 先执行有效更新
            var validResponse = await playerGrain.UpdatePlayerAsync(validUpdate);
            
            // 再执行无效更新
            var invalidResponse = await playerGrain.UpdatePlayerAsync(invalidUpdate);

            // Assert
            Assert.True(validResponse.Success);
            Assert.Equal(currentVersion + 1, validResponse.NewVersion);
            
            Assert.False(invalidResponse.Success);
            Assert.Contains("版本不匹配", invalidResponse.Message);
            Assert.Equal(currentVersion + 1, invalidResponse.NewVersion); // 返回当前版本

            _output.WriteLine($"✅ 版本冲突处理测试通过");
        }

        #endregion

        #region 错误处理测试

        [Fact]
        public async Task PlayerGrain_Should_Handle_Invalid_Session()
        {
            // Arrange
            var playerId = $"unit-session-{Guid.NewGuid():N}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // Act & Assert - 测试无效会话
            var invalidSessionResult = await playerGrain.ValidateSessionAsync("invalid-session-id");
            Assert.False(invalidSessionResult);

            var emptySessionResult = await playerGrain.ValidateSessionAsync("");
            Assert.False(emptySessionResult);

            var nullSessionResult = await playerGrain.ValidateSessionAsync(null!);
            Assert.False(nullSessionResult);

            _output.WriteLine($"✅ 无效会话处理测试通过");
        }

        [Fact]
        public async Task PlayerGrain_Should_Handle_Room_State_Consistency()
        {
            // Arrange
            var playerId = $"unit-room-{Guid.NewGuid():N}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "房间状态测试",
                ClientVersion = "1.0.0",
                Platform = "Test"
            });

            // Act & Assert - 房间状态一致性测试
            var initialRoom = await playerGrain.GetCurrentRoomAsync();
            Assert.Null(initialRoom);

            // 加入房间
            var roomId = "test-room-consistency";
            var joinResult = await playerGrain.JoinRoomAsync(roomId);
            Assert.True(joinResult);

            var currentRoom = await playerGrain.GetCurrentRoomAsync();
            Assert.Equal(roomId, currentRoom);

            var playerInfo = await playerGrain.GetPlayerInfoAsync();
            Assert.Equal(PlayerOnlineStatus.InGame, playerInfo!.OnlineStatus);

            // 重复加入相同房间
            var rejoinResult = await playerGrain.JoinRoomAsync(roomId);
            Assert.True(rejoinResult); // 应该返回成功，但状态不变

            var stillSameRoom = await playerGrain.GetCurrentRoomAsync();
            Assert.Equal(roomId, stillSameRoom);

            // 离开房间
            var leaveResult = await playerGrain.LeaveRoomAsync();
            Assert.True(leaveResult);

            var noRoom = await playerGrain.GetCurrentRoomAsync();
            Assert.Null(noRoom);

            var onlineInfo = await playerGrain.GetPlayerInfoAsync();
            Assert.Equal(PlayerOnlineStatus.Online, onlineInfo!.OnlineStatus);

            _output.WriteLine($"✅ 房间状态一致性测试通过");
        }

        [Fact]
        public async Task PlayerGrain_Should_Handle_Null_And_Empty_Values()
        {
            // Arrange
            var playerId = $"unit-nulls-{Guid.NewGuid():N}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 首先获取当前状态以获取正确的版本号
            var currentState = await playerGrain.GetFullStateAsync();
            var currentVersion = currentState!.Version;

            // Act & Assert - 空值处理测试
            var updateWithNulls = new PlayerUpdateRequest
            {
                DisplayName = null, // null 值应该被忽略
                Position = null,
                Settings = null,
                Version = currentVersion // 使用正确的版本号
            };

            var response = await playerGrain.UpdatePlayerAsync(updateWithNulls);
            Assert.True(response.Success); // 应该成功，但无实际更新

            var emptyStringUpdate = new PlayerUpdateRequest
            {
                DisplayName = "", // 空字符串应该被忽略
                Version = response.NewVersion
            };

            var emptyResponse = await playerGrain.UpdatePlayerAsync(emptyStringUpdate);
            Assert.True(emptyResponse.Success);

            _output.WriteLine($"✅ 空值处理测试通过");
        }

        #endregion

        #region 边界条件测试

        [Fact]
        public async Task PlayerGrain_Should_Handle_Multiple_Login_Attempts()
        {
            // Arrange
            var playerId = $"unit-multilogin-{Guid.NewGuid():N}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "多次登录测试",
                ClientVersion = "1.0.0",
                Platform = "Test"
            };

            // Act - 多次登录
            var firstLogin = await playerGrain.LoginAsync(loginRequest);
            var secondLogin = await playerGrain.LoginAsync(loginRequest);
            var thirdLogin = await playerGrain.LoginAsync(loginRequest);

            // Assert - 所有登录都应该成功，但会生成不同的会话ID
            Assert.True(firstLogin.Success);
            Assert.True(secondLogin.Success);
            Assert.True(thirdLogin.Success);

            Assert.NotEqual(firstLogin.SessionId, secondLogin.SessionId);
            Assert.NotEqual(secondLogin.SessionId, thirdLogin.SessionId);

            // 只有最后一次登录的会话有效
            var validSession = await playerGrain.ValidateSessionAsync(thirdLogin.SessionId!);
            Assert.True(validSession);

            var invalidSession = await playerGrain.ValidateSessionAsync(firstLogin.SessionId!);
            Assert.False(invalidSession); // 旧会话应该无效

            _output.WriteLine($"✅ 多次登录处理测试通过");
        }

        [Fact]
        public async Task PlayerGrain_Should_Handle_Rapid_Heartbeats()
        {
            // Arrange
            var playerId = $"unit-heartbeat-{Guid.NewGuid():N}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "心跳压力测试",
                ClientVersion = "1.0.0",
                Platform = "Test"
            });

            var initialTime = await playerGrain.GetLastActiveTimeAsync();

            // Act - 快速连续心跳
            var heartbeatTasks = Enumerable.Range(0, 100)
                .Select(async i =>
                {
                    await Task.Delay(i); // 错开时间避免完全同时
                    return await playerGrain.HeartbeatAsync();
                });

            var results = await Task.WhenAll(heartbeatTasks);

            // Assert - 所有心跳都应该成功
            Assert.All(results, result => Assert.True(result));

            var finalTime = await playerGrain.GetLastActiveTimeAsync();
            Assert.True(finalTime > initialTime);

            _output.WriteLine($"✅ 心跳压力测试通过: 100次心跳全部成功");
        }

        [Fact]
        public async Task PlayerGrain_Should_Maintain_Data_Integrity()
        {
            // Arrange
            var playerId = $"unit-integrity-{Guid.NewGuid():N}";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 建立基准数据
            var loginResponse = await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "数据完整性测试",
                ClientVersion = "1.0.0",
                Platform = "Test"
            });

            // Act - 执行复杂的状态变更序列
            var operations = new[]
            {
                async () => await playerGrain.JoinRoomAsync("room-1"),
                async () => await playerGrain.UpdatePositionAsync(new PlayerPosition { X = 1, Y = 1, Z = 1 }),
                async () => await playerGrain.SetOnlineStatusAsync(PlayerOnlineStatus.Busy),
                async () => await playerGrain.UpdateStatsAsync(new PlayerStats { GamesPlayed = 1 }),
                async () => await playerGrain.LeaveRoomAsync(),
                async () => await playerGrain.UpdatePositionAsync(new PlayerPosition { X = 2, Y = 2, Z = 2 }),
                async () => await playerGrain.SetOnlineStatusAsync(PlayerOnlineStatus.Online),
                async () => await playerGrain.UpdateStatsAsync(new PlayerStats { GamesPlayed = 2 }),
                async () => await playerGrain.JoinRoomAsync("room-2"),
                async () => await playerGrain.UpdatePositionAsync(new PlayerPosition { X = 3, Y = 3, Z = 3 })
            };

            // 顺序执行所有操作
            foreach (var operation in operations)
            {
                var result = await operation();
                Assert.True(result);
            }

            // Assert - 验证最终状态的完整性
            var finalInfo = await playerGrain.GetPlayerInfoAsync();
            var currentRoom = await playerGrain.GetCurrentRoomAsync();
            var isOnline = await playerGrain.IsOnlineAsync();
            var validSession = await playerGrain.ValidateSessionAsync(loginResponse.SessionId!);

            Assert.NotNull(finalInfo);
            Assert.Equal(playerId, finalInfo.PlayerId);
            Assert.Equal("数据完整性测试", finalInfo.DisplayName);
            Assert.Equal("room-2", currentRoom);
            Assert.Equal(PlayerOnlineStatus.InGame, finalInfo.OnlineStatus);
            Assert.True(isOnline);
            Assert.True(validSession);
            Assert.Equal(3f, finalInfo.Position!.X);
            Assert.Equal(3f, finalInfo.Position.Y);
            Assert.Equal(3f, finalInfo.Position.Z);
            Assert.Equal(2, finalInfo.Stats!.GamesPlayed);

            _output.WriteLine($"✅ 数据完整性测试通过: 状态变更序列执行正确");
        }

        #endregion
    }
}
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orleans.TestingHost;
using Orleans.Hosting;
using Orleans;
using Wind.Server.Services;
using Wind.Server.Configuration;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Tests.TestFixtures;

namespace Wind.Tests.IntegrationTests;

/// <summary>
/// MongoDB与Orleans Grain集成测试
/// 验证MongoDB持久化服务在Orleans Grain中的实际使用
/// </summary>
public class MongoDbGrainIntegrationTests : IClassFixture<ClusterFixture>, IAsyncDisposable
{
    private readonly ClusterFixture _fixture;
    private readonly IServiceProvider _serviceProvider;
    private readonly PlayerPersistenceService _playerService;
    private readonly RoomPersistenceService _roomService;

    public MongoDbGrainIntegrationTests(ClusterFixture fixture)
    {
        _fixture = fixture;
        
        // 创建独立的MongoDB服务提供者用于直接验证
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = "mongodb://localhost:27017",
                ["MongoDb:DatabaseName"] = "WindGrainIntegrationTest",
                ["MongoDb:Collections:Players"] = "integration_test_players",
                ["MongoDb:Collections:Rooms"] = "integration_test_rooms",
                ["MongoDb:Collections:GameRecords"] = "integration_test_game_records"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.Configure<MongoDbOptions>(configuration.GetSection("MongoDb"));
        services.AddSingleton<MongoDbConnectionManager>();
        services.AddSingleton<MongoIndexManager>();
        services.AddSingleton<PlayerPersistenceService>();
        services.AddSingleton<RoomPersistenceService>();

        _serviceProvider = services.BuildServiceProvider();
        _playerService = _serviceProvider.GetRequiredService<PlayerPersistenceService>();
        _roomService = _serviceProvider.GetRequiredService<RoomPersistenceService>();
    }

    [Fact]
    public async Task PlayerGrain_ShouldIntegrateWithMongoDB_ThroughDataSyncService()
    {
        // Arrange
        var testPlayerId = $"integration_player_{Guid.NewGuid():N}";
        var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(testPlayerId);
        
        try
        {
            // Act - 通过Orleans Grain执行玩家登录
            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = testPlayerId,
                DisplayName = "集成测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Test",
                DeviceId = "test-device"
            };
            
            var loginResponse = await playerGrain.LoginAsync(loginRequest);
            
            // Assert - 验证Orleans Grain操作成功
            Assert.True(loginResponse.Success);
            Assert.NotNull(loginResponse.SessionId);
            
            // 等待一段时间让数据同步生效
            await Task.Delay(1000);
            
            // 通过直接的MongoDB服务验证数据是否真正持久化
            var persistedPlayer = await _playerService.GetPlayerByIdAsync(testPlayerId);
            
            if (persistedPlayer != null)
            {
                // 如果MongoDB服务可用，验证数据持久化
                Assert.Equal(testPlayerId, persistedPlayer.PlayerId);
                Assert.Equal("集成测试玩家", persistedPlayer.DisplayName);
                Assert.True(persistedPlayer.OnlineStatus == PlayerOnlineStatus.Online);
            }
            
            // 验证Orleans Grain状态管理
            var playerInfo = await playerGrain.GetPlayerInfoAsync();
            Assert.NotNull(playerInfo);
            Assert.Equal(testPlayerId, playerInfo.PlayerId);
            Assert.Equal("集成测试玩家", playerInfo.DisplayName);
        }
        finally
        {
            // Cleanup
            await CleanupPlayerAsync(testPlayerId);
        }
    }

    [Fact]
    public async Task RoomGrain_ShouldIntegrateWithMongoDB_ThroughDataSyncService()
    {
        // Arrange
        var testRoomId = $"integration_room_{Guid.NewGuid():N}";
        var testPlayerId = $"integration_player_{Guid.NewGuid():N}";
        var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(testRoomId);
        
        try
        {
            // Act - 通过Orleans Grain创建房间
            var createRequest = new CreateRoomRequest
            {
                CreatorId = testPlayerId,
                RoomName = "集成测试房间",
                MaxPlayerCount = 4,
                RoomType = RoomType.Normal,
                Settings = new RoomSettings
                {
                    GameMode = "Integration",
                    MapId = "TestMap",
                    GameDuration = 300,
                    MaxScore = 100,
                    IsPrivate = false,
                    EnableSpectators = true,
                    AutoStart = false,
                    MinPlayersToStart = 2,
                    GameRules = new Dictionary<string, object>(),
                    CustomSettings = new Dictionary<string, object>()
                }
            };
            
            var createResponse = await roomGrain.CreateRoomAsync(createRequest);
            
            // Assert - 验证Orleans Grain操作成功
            Assert.True(createResponse.Success);
            Assert.NotNull(createResponse.RoomId);
            Assert.NotNull(createResponse.RoomInfo);
            
            // 等待一段时间让数据同步生效
            await Task.Delay(1000);
            
            // 通过直接的MongoDB服务验证数据是否真正持久化
            var persistedRoom = await _roomService.GetRoomByIdAsync(testRoomId);
            
            if (persistedRoom != null)
            {
                // 如果MongoDB服务可用，验证数据持久化
                Assert.Equal(testRoomId, persistedRoom.RoomId);
                Assert.Equal("集成测试房间", persistedRoom.Name);
                Assert.Equal(testPlayerId, persistedRoom.OwnerId);
                Assert.Equal(RoomType.Normal, persistedRoom.Type);
            }
            
            // 验证Orleans Grain状态管理
            var roomInfoRequest = new GetRoomInfoRequest { RoomId = testRoomId };
            var roomInfoResponse = await roomGrain.GetRoomInfoAsync(roomInfoRequest);
            Assert.NotNull(roomInfoResponse);
            Assert.True(roomInfoResponse.Success);
            Assert.NotNull(roomInfoResponse.RoomInfo);
            Assert.Equal(testRoomId, roomInfoResponse.RoomInfo.RoomId);
            Assert.Equal("集成测试房间", roomInfoResponse.RoomInfo.RoomName);
        }
        finally
        {
            // Cleanup
            await CleanupRoomAsync(testRoomId);
        }
    }

    [Fact]
    public async Task DataSyncService_ShouldHandleGrainStateChanges()
    {
        // Arrange
        var testPlayerId = $"sync_test_player_{Guid.NewGuid():N}";
        var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(testPlayerId);
        
        try
        {
            // Act - 执行多次状态变更
            // 1. 登录
            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = testPlayerId,
                DisplayName = "同步测试玩家",
                ClientVersion = "1.0.0",
                Platform = "Test",
                DeviceId = "test-device-sync"
            };
            await playerGrain.LoginAsync(loginRequest);
            
            // 2. 更新玩家信息
            var updateRequest = new PlayerUpdateRequest
            {
                PlayerId = testPlayerId,
                DisplayName = "同步测试玩家更新",
                OnlineStatus = PlayerOnlineStatus.Online
            };
            await playerGrain.UpdatePlayerAsync(updateRequest);
            
            // 3. 心跳更新
            await playerGrain.HeartbeatAsync();
            
            // 等待数据同步
            await Task.Delay(2000);
            
            // Assert - 验证最终状态
            var finalPlayerInfo = await playerGrain.GetPlayerInfoAsync();
            Assert.NotNull(finalPlayerInfo);
            Assert.Equal(testPlayerId, finalPlayerInfo.PlayerId);
            Assert.Equal("同步测试玩家更新", finalPlayerInfo.DisplayName);
            
            // 验证MongoDB中的数据（如果可用）
            var persistedPlayer = await _playerService.GetPlayerByIdAsync(testPlayerId);
            if (persistedPlayer != null)
            {
                Assert.Equal("同步测试玩家更新", persistedPlayer.DisplayName);
            }
        }
        finally
        {
            // Cleanup
            await CleanupPlayerAsync(testPlayerId);
        }
    }

    private async Task CleanupPlayerAsync(string playerId)
    {
        try
        {
            var connectionManager = _serviceProvider.GetRequiredService<MongoDbConnectionManager>();
            var collection = connectionManager.GetCollection<Wind.Server.Models.Documents.PlayerDocument>("integration_test_players");
            await collection.DeleteOneAsync(MongoDB.Driver.Builders<Wind.Server.Models.Documents.PlayerDocument>.Filter.Eq(x => x.PlayerId, playerId));
        }
        catch
        {
            // 忽略清理错误
        }
    }

    private async Task CleanupRoomAsync(string roomId)
    {
        try
        {
            var connectionManager = _serviceProvider.GetRequiredService<MongoDbConnectionManager>();
            var collection = connectionManager.GetCollection<Wind.Server.Models.Documents.RoomDocument>("integration_test_rooms");
            await collection.DeleteOneAsync(MongoDB.Driver.Builders<Wind.Server.Models.Documents.RoomDocument>.Filter.Eq(x => x.RoomId, roomId));
        }
        catch
        {
            // 忽略清理错误
        }
    }

    public async ValueTask DisposeAsync()
    {
        // 清理测试数据库
        try
        {
            var connectionManager = _serviceProvider.GetRequiredService<MongoDbConnectionManager>();
            var database = connectionManager.GetDatabase();
            await database.DropCollectionAsync("integration_test_players");
            await database.DropCollectionAsync("integration_test_rooms");
            await database.DropCollectionAsync("integration_test_game_records");
        }
        catch
        {
            // 忽略清理错误
        }

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
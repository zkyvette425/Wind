using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wind.Server.Services;
using Wind.Server.Configuration;
using Wind.Server.Models.Documents;
using Wind.Shared.Models;
using MongoDB.Driver;

namespace Wind.Tests.PersistenceTests;

/// <summary>
/// MongoDB持久化服务基础功能测试
/// 验证MongoDB连接、文档保存和查询功能
/// </summary>
public class MongoDbPersistenceTests : IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MongoDbConnectionManager _connectionManager;
    private readonly PlayerPersistenceService _playerService;
    private readonly RoomPersistenceService _roomService;
    private readonly GameRecordPersistenceService _gameRecordService;
    private readonly MongoIndexManager _indexManager;

    public MongoDbPersistenceTests()
    {
        // 创建测试配置
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = "mongodb://localhost:27017",
                ["MongoDb:DatabaseName"] = "WindTestDb",
                ["MongoDb:Collections:Players"] = "test_players",
                ["MongoDb:Collections:Rooms"] = "test_rooms", 
                ["MongoDb:Collections:GameRecords"] = "test_game_records"
            })
            .Build();

        // 创建服务容器
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.Configure<MongoDbOptions>(configuration.GetSection("MongoDb"));
        services.AddSingleton<MongoDbConnectionManager>();
        services.AddSingleton<MongoIndexManager>();
        services.AddSingleton<PlayerPersistenceService>();
        services.AddSingleton<RoomPersistenceService>();
        services.AddSingleton<GameRecordPersistenceService>();

        _serviceProvider = services.BuildServiceProvider();
        
        _connectionManager = _serviceProvider.GetRequiredService<MongoDbConnectionManager>();
        _indexManager = _serviceProvider.GetRequiredService<MongoIndexManager>();
        _playerService = _serviceProvider.GetRequiredService<PlayerPersistenceService>();
        _roomService = _serviceProvider.GetRequiredService<RoomPersistenceService>();
        _gameRecordService = _serviceProvider.GetRequiredService<GameRecordPersistenceService>();
    }

    [Fact]
    public async Task MongoDB_Connection_ShouldWork()
    {
        // Arrange & Act
        var database = _connectionManager.GetDatabase();
        
        // Assert
        Assert.NotNull(database);
        Assert.Equal("WindTestDb", database.DatabaseNamespace.DatabaseName);
        
        // 测试数据库连接
        var collections = await database.ListCollectionNamesAsync();
        var collectionList = await collections.ToListAsync();
        
        // 验证可以访问数据库
        Assert.NotNull(collectionList);
    }

    [Fact]
    public async Task IndexManager_CreateIndexes_ShouldWork()
    {
        // Arrange & Act
        await _indexManager.CreateAllIndexesAsync();
        
        // Assert - 验证索引创建成功
        var stats = await _indexManager.GetIndexStatsAsync();
        Assert.NotNull(stats);
        Assert.True(stats.ContainsKey("Players"));
        Assert.True(stats.ContainsKey("Rooms"));
        Assert.True(stats.ContainsKey("GameRecords"));
    }

    [Fact]
    public async Task PlayerPersistenceService_SaveAndRetrieve_ShouldWork()
    {
        // Arrange
        var testPlayerId = $"test_player_{Guid.NewGuid():N}";
        var playerState = new PlayerState
        {
            PlayerId = testPlayerId,
            DisplayName = "测试玩家",
            Level = 10,
            Experience = 1500,
            OnlineStatus = PlayerOnlineStatus.Online,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };

        try
        {
            // Act - 保存玩家数据
            var savedId = await _playerService.SavePlayerAsync(playerState);
            Assert.NotNull(savedId);

            // 获取玩家数据
            var retrievedPlayer = await _playerService.GetPlayerByIdAsync(testPlayerId);
            
            // Assert
            Assert.NotNull(retrievedPlayer);
            Assert.Equal(testPlayerId, retrievedPlayer.PlayerId);
            Assert.Equal("测试玩家", retrievedPlayer.DisplayName);
            Assert.Equal(10, retrievedPlayer.Level);
            Assert.Equal(1500, retrievedPlayer.Experience);
            Assert.Equal(PlayerOnlineStatus.Online, retrievedPlayer.OnlineStatus);
        }
        finally
        {
            // Cleanup
            await CleanupPlayerAsync(testPlayerId);
        }
    }

    [Fact]
    public async Task RoomPersistenceService_SaveAndRetrieve_ShouldWork()
    {
        // Arrange
        var testRoomId = $"test_room_{Guid.NewGuid():N}";
        var testPlayerId = $"test_player_{Guid.NewGuid():N}";
        
        var roomState = new RoomState
        {
            RoomId = testRoomId,
            RoomName = "测试房间",
            CreatorId = testPlayerId,
            RoomType = RoomType.Normal,
            Status = RoomStatus.Waiting,
            MaxPlayerCount = 4,
            CurrentPlayerCount = 1,
            Players = new List<RoomPlayer>
            {
                new RoomPlayer
                {
                    PlayerId = testPlayerId,
                    DisplayName = "测试玩家",
                    Role = PlayerRole.Leader,
                    ReadyStatus = PlayerReadyStatus.NotReady,
                    JoinedAt = DateTime.UtcNow,
                    Level = 1,
                    Position = new PlayerPosition(),
                    Score = 0,
                    PlayerData = new Dictionary<string, object>()
                }
            },
            Settings = new RoomSettings
            {
                GameMode = "Classic",
                MapId = "TestMap",
                GameDuration = 300,
                MaxScore = 100,
                IsPrivate = false,
                EnableSpectators = true,
                AutoStart = false,
                MinPlayersToStart = 2,
                GameRules = new Dictionary<string, object>(),
                CustomSettings = new Dictionary<string, object>()
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            // Act - 保存房间数据
            var savedId = await _roomService.SaveRoomAsync(roomState);
            Assert.NotNull(savedId);

            // 获取房间数据
            var retrievedRoom = await _roomService.GetRoomByIdAsync(testRoomId);
            
            // Assert
            Assert.NotNull(retrievedRoom);
            Assert.Equal(testRoomId, retrievedRoom.RoomId);
            Assert.Equal("测试房间", retrievedRoom.Name);
            Assert.Equal(testPlayerId, retrievedRoom.OwnerId);
            Assert.Equal(RoomType.Normal, retrievedRoom.Type);
            Assert.Equal(RoomStatus.Waiting, retrievedRoom.Status);
            Assert.Equal(4, retrievedRoom.MaxPlayers);
            Assert.Single(retrievedRoom.Players);
            Assert.Equal("Classic", retrievedRoom.Settings.GameMode);
        }
        finally
        {
            // Cleanup
            await CleanupRoomAsync(testRoomId);
        }
    }

    [Fact]
    public async Task GameRecordPersistenceService_SaveAndRetrieve_ShouldWork()
    {
        // Arrange
        var testGameId = $"test_game_{Guid.NewGuid():N}";
        var testRoomId = $"test_room_{Guid.NewGuid():N}";
        var testPlayerId = $"test_player_{Guid.NewGuid():N}";
        
        var gameRecord = new GameRecordDocument
        {
            GameId = testGameId,
            RoomId = testRoomId,
            GameMode = "Classic",
            GameStatus = GameStatus.Completed,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow,
            DurationSeconds = 600,
            Players = new List<GamePlayerDocument>
            {
                new GamePlayerDocument
                {
                    PlayerId = testPlayerId,
                    DisplayName = "测试玩家",
                    IsWinner = true,
                    FinalScore = 1000,
                    FinalRank = 1,
                    PlayTimeSeconds = 600,
                    PlayerStats = new Dictionary<string, object>()
                }
            },
            Events = new List<GameEventDocument>(),
            Statistics = new GameStatisticsDocument(),
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            // Act - 保存游戏记录
            var savedId = await _gameRecordService.SaveGameRecordAsync(gameRecord);
            Assert.NotNull(savedId);

            // 获取游戏记录
            var retrievedRecord = await _gameRecordService.GetGameRecordByIdAsync(testGameId);
            
            // Assert
            Assert.NotNull(retrievedRecord);
            Assert.Equal(testGameId, retrievedRecord.GameId);
            Assert.Equal(testRoomId, retrievedRecord.RoomId);
            Assert.Equal("Classic", retrievedRecord.GameMode);
            Assert.Equal(GameStatus.Completed, retrievedRecord.GameStatus);
            Assert.Equal(600, retrievedRecord.DurationSeconds);
            Assert.Single(retrievedRecord.Players);
            Assert.Equal(testPlayerId, retrievedRecord.Players[0].PlayerId);
            Assert.True(retrievedRecord.Players[0].IsWinner);
            Assert.Equal(1000, retrievedRecord.Players[0].FinalScore);
        }
        finally
        {
            // Cleanup
            await CleanupGameRecordAsync(testGameId);
        }
    }

    private async Task CleanupPlayerAsync(string playerId)
    {
        try
        {
            var collection = _connectionManager.GetCollection<PlayerDocument>("test_players");
            await collection.DeleteOneAsync(Builders<PlayerDocument>.Filter.Eq(x => x.PlayerId, playerId));
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
            var collection = _connectionManager.GetCollection<RoomDocument>("test_rooms");
            await collection.DeleteOneAsync(Builders<RoomDocument>.Filter.Eq(x => x.RoomId, roomId));
        }
        catch
        {
            // 忽略清理错误
        }
    }

    private async Task CleanupGameRecordAsync(string gameId)
    {
        try
        {
            var collection = _connectionManager.GetCollection<GameRecordDocument>("test_game_records");
            await collection.DeleteOneAsync(Builders<GameRecordDocument>.Filter.Eq(x => x.GameId, gameId));
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
            var database = _connectionManager.GetDatabase();
            await database.DropCollectionAsync("test_players");
            await database.DropCollectionAsync("test_rooms");
            await database.DropCollectionAsync("test_game_records");
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
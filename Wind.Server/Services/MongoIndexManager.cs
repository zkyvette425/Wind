using MongoDB.Driver;
using MongoDB.Bson;
using Wind.Server.Models.Documents;
using Microsoft.Extensions.Options;
using Wind.Server.Configuration;

namespace Wind.Server.Services;

/// <summary>
/// MongoDB索引管理服务
/// 负责创建和维护所有集合的索引，优化查询性能
/// </summary>
public class MongoIndexManager
{
    private readonly MongoDbConnectionManager _connectionManager;
    private readonly MongoDbOptions _options;
    private readonly ILogger<MongoIndexManager> _logger;

    public MongoIndexManager(
        MongoDbConnectionManager connectionManager,
        IOptions<MongoDbOptions> options,
        ILogger<MongoIndexManager> logger)
    {
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 创建所有集合的索引
    /// </summary>
    public async Task CreateAllIndexesAsync()
    {
        _logger.LogInformation("开始创建MongoDB索引...");

        try
        {
            await CreatePlayerIndexesAsync();
            await CreateRoomIndexesAsync();
            await CreateGameRecordIndexesAsync();

            _logger.LogInformation("✅ 所有MongoDB索引创建完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ MongoDB索引创建失败");
            throw;
        }
    }

    /// <summary>
    /// 创建玩家集合索引
    /// </summary>
    public async Task CreatePlayerIndexesAsync()
    {
        var collection = _connectionManager.GetCollection<PlayerDocument>(_options.Collections.Players);
        
        _logger.LogInformation("创建玩家集合索引...");

        var indexes = new List<CreateIndexModel<PlayerDocument>>
        {
            // 1. 玩家ID唯一索引（最重要）
            new CreateIndexModel<PlayerDocument>(
                Builders<PlayerDocument>.IndexKeys.Ascending(x => x.PlayerId),
                new CreateIndexOptions 
                { 
                    Unique = true, 
                    Name = "idx_playerId_unique",
                    Background = true 
                }),

            // 2. 显示名称索引（支持搜索）
            new CreateIndexModel<PlayerDocument>(
                Builders<PlayerDocument>.IndexKeys.Ascending(x => x.DisplayName),
                new CreateIndexOptions 
                { 
                    Name = "idx_displayName",
                    Background = true 
                }),

            // 3. 在线状态 + 最后活跃时间复合索引（查找在线玩家）
            new CreateIndexModel<PlayerDocument>(
                Builders<PlayerDocument>.IndexKeys
                    .Ascending(x => x.OnlineStatus)
                    .Descending(x => x.LastActiveAt),
                new CreateIndexOptions 
                { 
                    Name = "idx_onlineStatus_lastActive",
                    Background = true 
                }),

            // 4. 等级索引（排行榜查询）
            new CreateIndexModel<PlayerDocument>(
                Builders<PlayerDocument>.IndexKeys.Descending(x => x.Level),
                new CreateIndexOptions 
                { 
                    Name = "idx_level_desc",
                    Background = true 
                }),

            // 5. 经验值索引（排行榜查询）
            new CreateIndexModel<PlayerDocument>(
                Builders<PlayerDocument>.IndexKeys.Descending(x => x.Experience),
                new CreateIndexOptions 
                { 
                    Name = "idx_experience_desc",
                    Background = true 
                }),

            // 6. 创建时间索引（新玩家分析）
            new CreateIndexModel<PlayerDocument>(
                Builders<PlayerDocument>.IndexKeys.Descending(x => x.CreatedAt),
                new CreateIndexOptions 
                { 
                    Name = "idx_createdAt_desc",
                    Background = true 
                }),

            // 7. 最后登录时间索引（活跃度分析）
            new CreateIndexModel<PlayerDocument>(
                Builders<PlayerDocument>.IndexKeys.Descending(x => x.LastLoginAt),
                new CreateIndexOptions 
                { 
                    Name = "idx_lastLoginAt_desc",
                    Background = true 
                }),

            // 8. 当前房间ID索引（房间成员查询）
            new CreateIndexModel<PlayerDocument>(
                Builders<PlayerDocument>.IndexKeys.Ascending(x => x.CurrentRoomId),
                new CreateIndexOptions 
                { 
                    Name = "idx_currentRoomId",
                    Background = true,
                    Sparse = true // 只索引非null值
                }),

            // 9. 游戏统计复合索引（胜率排行）
            new CreateIndexModel<PlayerDocument>(
                Builders<PlayerDocument>.IndexKeys
                    .Descending("stats.gamesWon")
                    .Descending("stats.gamesPlayed"),
                new CreateIndexOptions 
                { 
                    Name = "idx_gameStats_winRate",
                    Background = true 
                }),

            // 10. 数据更新时间索引（数据同步查询）
            new CreateIndexModel<PlayerDocument>(
                Builders<PlayerDocument>.IndexKeys.Descending(x => x.UpdatedAt),
                new CreateIndexOptions 
                { 
                    Name = "idx_updatedAt_desc",
                    Background = true 
                })
        };

        await collection.Indexes.CreateManyAsync(indexes);
        _logger.LogInformation("✅ 玩家集合索引创建完成 ({Count}个)", indexes.Count);
    }

    /// <summary>
    /// 创建房间集合索引
    /// </summary>
    public async Task CreateRoomIndexesAsync()
    {
        var collection = _connectionManager.GetCollection<RoomDocument>(_options.Collections.Rooms);
        
        _logger.LogInformation("创建房间集合索引...");

        var indexes = new List<CreateIndexModel<RoomDocument>>
        {
            // 1. 房间ID唯一索引
            new CreateIndexModel<RoomDocument>(
                Builders<RoomDocument>.IndexKeys.Ascending(x => x.RoomId),
                new CreateIndexOptions 
                { 
                    Unique = true, 
                    Name = "idx_roomId_unique",
                    Background = true 
                }),

            // 2. 房间状态 + 类型复合索引（房间列表查询）
            new CreateIndexModel<RoomDocument>(
                Builders<RoomDocument>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.Type),
                new CreateIndexOptions 
                { 
                    Name = "idx_status_type",
                    Background = true 
                }),

            // 3. 房主ID索引（查找用户创建的房间）
            new CreateIndexModel<RoomDocument>(
                Builders<RoomDocument>.IndexKeys.Ascending(x => x.OwnerId),
                new CreateIndexOptions 
                { 
                    Name = "idx_ownerId",
                    Background = true 
                }),

            // 4. 房间名称文本索引（搜索功能）
            new CreateIndexModel<RoomDocument>(
                Builders<RoomDocument>.IndexKeys.Text(x => x.Name).Text(x => x.Description),
                new CreateIndexOptions 
                { 
                    Name = "idx_name_description_text",
                    Background = true,
                    DefaultLanguage = "none" // 禁用语言特定处理
                }),

            // 5. 创建时间索引（房间历史分析）
            new CreateIndexModel<RoomDocument>(
                Builders<RoomDocument>.IndexKeys.Descending(x => x.CreatedAt),
                new CreateIndexOptions 
                { 
                    Name = "idx_createdAt_desc",
                    Background = true 
                }),

            // 6. 游戏开始时间索引（活跃房间分析）
            new CreateIndexModel<RoomDocument>(
                Builders<RoomDocument>.IndexKeys.Descending(x => x.StartedAt),
                new CreateIndexOptions 
                { 
                    Name = "idx_startedAt_desc",
                    Background = true,
                    Sparse = true
                }),

            // 7. 游戏持续时间索引（游戏时长分析）
            new CreateIndexModel<RoomDocument>(
                Builders<RoomDocument>.IndexKeys.Descending(x => x.DurationSeconds),
                new CreateIndexOptions 
                { 
                    Name = "idx_durationSeconds_desc",
                    Background = true,
                    Sparse = true
                }),

            // 8. 玩家列表索引（查找玩家参与的房间）
            new CreateIndexModel<RoomDocument>(
                Builders<RoomDocument>.IndexKeys.Ascending("players.playerId"),
                new CreateIndexOptions 
                { 
                    Name = "idx_players_playerId",
                    Background = true 
                }),

            // 9. 游戏模式索引（模式统计）
            new CreateIndexModel<RoomDocument>(
                Builders<RoomDocument>.IndexKeys.Ascending("settings.gameMode"),
                new CreateIndexOptions 
                { 
                    Name = "idx_gameMode",
                    Background = true 
                }),

            // 10. 数据更新时间索引
            new CreateIndexModel<RoomDocument>(
                Builders<RoomDocument>.IndexKeys.Descending(x => x.UpdatedAt),
                new CreateIndexOptions 
                { 
                    Name = "idx_updatedAt_desc",
                    Background = true 
                })
        };

        await collection.Indexes.CreateManyAsync(indexes);
        _logger.LogInformation("✅ 房间集合索引创建完成 ({Count}个)", indexes.Count);
    }

    /// <summary>
    /// 创建游戏记录集合索引
    /// </summary>
    public async Task CreateGameRecordIndexesAsync()
    {
        var collection = _connectionManager.GetCollection<GameRecordDocument>(_options.Collections.GameRecords);
        
        _logger.LogInformation("创建游戏记录集合索引...");

        var indexes = new List<CreateIndexModel<GameRecordDocument>>
        {
            // 1. 游戏ID唯一索引
            new CreateIndexModel<GameRecordDocument>(
                Builders<GameRecordDocument>.IndexKeys.Ascending(x => x.GameId),
                new CreateIndexOptions 
                { 
                    Unique = true, 
                    Name = "idx_gameId_unique",
                    Background = true 
                }),

            // 2. 关联房间ID索引
            new CreateIndexModel<GameRecordDocument>(
                Builders<GameRecordDocument>.IndexKeys.Ascending(x => x.RoomId),
                new CreateIndexOptions 
                { 
                    Name = "idx_roomId",
                    Background = true 
                }),

            // 3. 游戏开始时间索引（时间序列查询）
            new CreateIndexModel<GameRecordDocument>(
                Builders<GameRecordDocument>.IndexKeys.Descending(x => x.StartTime),
                new CreateIndexOptions 
                { 
                    Name = "idx_startTime_desc",
                    Background = true 
                }),

            // 4. 游戏模式 + 开始时间复合索引（游戏统计）
            new CreateIndexModel<GameRecordDocument>(
                Builders<GameRecordDocument>.IndexKeys
                    .Ascending(x => x.GameMode)
                    .Descending(x => x.StartTime),
                new CreateIndexOptions 
                { 
                    Name = "idx_gameMode_startTime",
                    Background = true 
                }),

            // 5. 游戏状态索引
            new CreateIndexModel<GameRecordDocument>(
                Builders<GameRecordDocument>.IndexKeys.Ascending(x => x.GameStatus),
                new CreateIndexOptions 
                { 
                    Name = "idx_gameStatus",
                    Background = true 
                }),

            // 6. 游戏持续时间索引（性能分析）
            new CreateIndexModel<GameRecordDocument>(
                Builders<GameRecordDocument>.IndexKeys.Descending(x => x.DurationSeconds),
                new CreateIndexOptions 
                { 
                    Name = "idx_durationSeconds_desc",
                    Background = true 
                }),

            // 7. 参与玩家索引（玩家游戏历史）
            new CreateIndexModel<GameRecordDocument>(
                Builders<GameRecordDocument>.IndexKeys.Ascending("players.playerId"),
                new CreateIndexOptions 
                { 
                    Name = "idx_players_playerId",
                    Background = true 
                }),

            // 8. 获胜玩家索引（胜负统计）
            new CreateIndexModel<GameRecordDocument>(
                Builders<GameRecordDocument>.IndexKeys
                    .Ascending("players.playerId")
                    .Ascending("players.isWinner"),
                new CreateIndexOptions 
                { 
                    Name = "idx_players_winner",
                    Background = true 
                }),

            // 9. 游戏事件时间索引（事件查询）
            new CreateIndexModel<GameRecordDocument>(
                Builders<GameRecordDocument>.IndexKeys.Ascending("events.timestamp"),
                new CreateIndexOptions 
                { 
                    Name = "idx_events_timestamp",
                    Background = true 
                }),

            // 10. 游戏事件类型索引（事件分析）
            new CreateIndexModel<GameRecordDocument>(
                Builders<GameRecordDocument>.IndexKeys.Ascending("events.eventType"),
                new CreateIndexOptions 
                { 
                    Name = "idx_events_eventType",
                    Background = true 
                }),

            // 11. TTL索引（自动清理旧记录，保留90天）
            new CreateIndexModel<GameRecordDocument>(
                Builders<GameRecordDocument>.IndexKeys.Ascending(x => x.CreatedAt),
                new CreateIndexOptions 
                { 
                    Name = "idx_createdAt_ttl",
                    Background = true,
                    ExpireAfter = TimeSpan.FromDays(90) // 90天后自动删除
                })
        };

        await collection.Indexes.CreateManyAsync(indexes);
        _logger.LogInformation("✅ 游戏记录集合索引创建完成 ({Count}个)", indexes.Count);
    }

    /// <summary>
    /// 获取索引统计信息
    /// </summary>
    public async Task<Dictionary<string, object>> GetIndexStatsAsync()
    {
        var stats = new Dictionary<string, object>();

        try
        {
            var database = _connectionManager.GetDatabase();
            var collections = new[] { 
                ("Players", _options.Collections.Players),
                ("Rooms", _options.Collections.Rooms), 
                ("GameRecords", _options.Collections.GameRecords)
            };

            foreach (var (displayName, collectionName) in collections)
            {
                var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                var indexes = await collection.Indexes.ListAsync();
                var indexList = await indexes.ToListAsync();

                stats[displayName] = new
                {
                    CollectionName = collectionName,
                    IndexCount = indexList.Count,
                    Indexes = indexList.Select(idx => new
                    {
                        Name = idx.GetValue("name", "").AsString,
                        Keys = idx.GetValue("key", new BsonDocument()).ToString(),
                        Unique = idx.GetValue("unique", false).AsBoolean,
                        Background = idx.GetValue("background", false).AsBoolean,
                        Sparse = idx.GetValue("sparse", false).AsBoolean
                    }).ToList()
                };
            }

            _logger.LogInformation("索引统计信息已生成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取索引统计信息失败");
            stats["error"] = ex.Message;
        }

        return stats;
    }
}
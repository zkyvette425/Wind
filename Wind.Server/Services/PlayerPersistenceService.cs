using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Options;
using Wind.Server.Models.Documents;
using Wind.Server.Configuration;
using Wind.Shared.Models;

namespace Wind.Server.Services;

/// <summary>
/// 玩家数据持久化服务实现
/// 提供完整的玩家数据MongoDB操作
/// </summary>
public class PlayerPersistenceService : IPlayerPersistenceService
{
    private readonly MongoDbConnectionManager _connectionManager;
    private readonly IMongoCollection<PlayerDocument> _collection;
    private readonly MongoDbOptions _options;
    private readonly ILogger<PlayerPersistenceService> _logger;

    public PlayerPersistenceService(
        MongoDbConnectionManager connectionManager,
        IOptions<MongoDbOptions> options,
        ILogger<PlayerPersistenceService> logger)
    {
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = logger;
        _collection = _connectionManager.GetCollection<PlayerDocument>(_options.Collections.Players);
    }

    /// <summary>
    /// 保存玩家数据到MongoDB
    /// </summary>
    public async Task<string> SavePlayerAsync(PlayerState playerState)
    {
        try
        {
            var existingPlayer = await GetPlayerByIdAsync(playerState.PlayerId);
            var playerDoc = PlayerDocument.FromPlayerState(playerState, existingPlayer?.Id);

            if (existingPlayer != null)
            {
                // 更新现有玩家
                var filter = Builders<PlayerDocument>.Filter.Eq(x => x.PlayerId, playerState.PlayerId);
                await _collection.ReplaceOneAsync(filter, playerDoc);
                _logger.LogDebug("玩家数据已更新: {PlayerId}", playerState.PlayerId);
                return existingPlayer.Id!;
            }
            else
            {
                // 插入新玩家
                await _collection.InsertOneAsync(playerDoc);
                _logger.LogDebug("新玩家数据已保存: {PlayerId}", playerState.PlayerId);
                return playerDoc.Id!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存玩家数据失败: {PlayerId}", playerState.PlayerId);
            throw;
        }
    }

    /// <summary>
    /// 批量保存玩家数据
    /// </summary>
    public async Task<List<string>> SavePlayersAsync(IEnumerable<PlayerState> playerStates)
    {
        var results = new List<string>();
        var playerList = playerStates.ToList();

        try
        {
            // 获取现有玩家数据
            var playerIds = playerList.Select(p => p.PlayerId).ToList();
            var existingPlayers = await _collection
                .Find(Builders<PlayerDocument>.Filter.In(x => x.PlayerId, playerIds))
                .ToListAsync();

            var existingPlayerDict = existingPlayers.ToDictionary(p => p.PlayerId, p => p);

            var bulkOps = new List<WriteModel<PlayerDocument>>();

            foreach (var playerState in playerList)
            {
                var existingPlayer = existingPlayerDict.GetValueOrDefault(playerState.PlayerId);
                var playerDoc = PlayerDocument.FromPlayerState(playerState, existingPlayer?.Id);

                if (existingPlayer != null)
                {
                    // 更新操作
                    var filter = Builders<PlayerDocument>.Filter.Eq(x => x.PlayerId, playerState.PlayerId);
                    bulkOps.Add(new ReplaceOneModel<PlayerDocument>(filter, playerDoc));
                    results.Add(existingPlayer.Id!);
                }
                else
                {
                    // 插入操作
                    bulkOps.Add(new InsertOneModel<PlayerDocument>(playerDoc));
                    results.Add(playerDoc.Id ?? ObjectId.GenerateNewId().ToString());
                }
            }

            if (bulkOps.Count > 0)
            {
                await _collection.BulkWriteAsync(bulkOps);
                _logger.LogInformation("批量保存玩家数据完成: {Count}个", bulkOps.Count);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量保存玩家数据失败");
            throw;
        }
    }

    /// <summary>
    /// 根据玩家ID获取玩家数据
    /// </summary>
    public async Task<PlayerDocument?> GetPlayerByIdAsync(string playerId)
    {
        try
        {
            var filter = Builders<PlayerDocument>.Filter.Eq(x => x.PlayerId, playerId);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取玩家数据失败: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// 根据显示名称搜索玩家
    /// </summary>
    public async Task<List<PlayerDocument>> SearchPlayersByNameAsync(string displayName, int limit = 20)
    {
        try
        {
            var filter = Builders<PlayerDocument>.Filter.Regex(x => x.DisplayName, 
                new BsonRegularExpression(displayName, "i")); // 不区分大小写
            
            return await _collection
                .Find(filter)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索玩家失败: {DisplayName}", displayName);
            throw;
        }
    }

    /// <summary>
    /// 获取在线玩家列表
    /// </summary>
    public async Task<List<PlayerDocument>> GetOnlinePlayersAsync(int limit = 100)
    {
        try
        {
            var filter = Builders<PlayerDocument>.Filter.Ne(x => x.OnlineStatus, PlayerOnlineStatus.Offline);
            var sort = Builders<PlayerDocument>.Sort.Descending(x => x.LastActiveAt);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取在线玩家列表失败");
            throw;
        }
    }

    /// <summary>
    /// 获取玩家排行榜（按等级）
    /// </summary>
    public async Task<List<PlayerDocument>> GetPlayerRankingByLevelAsync(int page = 1, int pageSize = 50)
    {
        try
        {
            var sort = Builders<PlayerDocument>.Sort
                .Descending(x => x.Level)
                .Descending(x => x.Experience);

            return await _collection
                .Find(Builders<PlayerDocument>.Filter.Empty)
                .Sort(sort)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取等级排行榜失败");
            throw;
        }
    }

    /// <summary>
    /// 获取玩家排行榜（按经验值）
    /// </summary>
    public async Task<List<PlayerDocument>> GetPlayerRankingByExperienceAsync(int page = 1, int pageSize = 50)
    {
        try
        {
            var sort = Builders<PlayerDocument>.Sort.Descending(x => x.Experience);

            return await _collection
                .Find(Builders<PlayerDocument>.Filter.Empty)
                .Sort(sort)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取经验排行榜失败");
            throw;
        }
    }

    /// <summary>
    /// 获取玩家游戏统计排行榜
    /// </summary>
    public async Task<List<PlayerDocument>> GetPlayerRankingByWinRateAsync(int page = 1, int pageSize = 50)
    {
        try
        {
            // 使用聚合管道计算胜率
            var pipeline = new[]
            {
                new BsonDocument("$addFields", new BsonDocument
                {
                    ["winRate"] = new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gt", new BsonArray { "$stats.gamesPlayed", 0 }),
                        new BsonDocument("$divide", new BsonArray { "$stats.gamesWon", "$stats.gamesPlayed" }),
                        0
                    })
                }),
                new BsonDocument("$match", new BsonDocument("stats.gamesPlayed", new BsonDocument("$gte", 5))), // 至少5场游戏
                new BsonDocument("$sort", new BsonDocument
                {
                    ["winRate"] = -1,
                    ["stats.gamesWon"] = -1
                }),
                new BsonDocument("$skip", (page - 1) * pageSize),
                new BsonDocument("$limit", pageSize)
            };

            return await _collection.Aggregate<PlayerDocument>(pipeline).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取胜率排行榜失败");
            throw;
        }
    }

    /// <summary>
    /// 根据房间ID获取房间内玩家
    /// </summary>
    public async Task<List<PlayerDocument>> GetPlayersByRoomIdAsync(string roomId)
    {
        try
        {
            var filter = Builders<PlayerDocument>.Filter.Eq(x => x.CurrentRoomId, roomId);
            return await _collection.Find(filter).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取房间玩家失败: {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// 获取最近活跃的玩家
    /// </summary>
    public async Task<List<PlayerDocument>> GetRecentActivePlayersAsync(TimeSpan timeRange, int limit = 100)
    {
        try
        {
            var since = DateTime.UtcNow - timeRange;
            var filter = Builders<PlayerDocument>.Filter.Gte(x => x.LastActiveAt, since);
            var sort = Builders<PlayerDocument>.Sort.Descending(x => x.LastActiveAt);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最近活跃玩家失败");
            throw;
        }
    }

    /// <summary>
    /// 获取新注册玩家
    /// </summary>
    public async Task<List<PlayerDocument>> GetNewPlayersAsync(DateTime since, int limit = 100)
    {
        try
        {
            var filter = Builders<PlayerDocument>.Filter.Gte(x => x.CreatedAt, since);
            var sort = Builders<PlayerDocument>.Sort.Descending(x => x.CreatedAt);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取新注册玩家失败");
            throw;
        }
    }

    /// <summary>
    /// 更新玩家在线状态
    /// </summary>
    public async Task<bool> UpdatePlayerOnlineStatusAsync(string playerId, PlayerOnlineStatus status)
    {
        try
        {
            var filter = Builders<PlayerDocument>.Filter.Eq(x => x.PlayerId, playerId);
            var update = Builders<PlayerDocument>.Update
                .Set(x => x.OnlineStatus, status)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            if (status != PlayerOnlineStatus.Offline)
            {
                update = update.Set(x => x.LastActiveAt, DateTime.UtcNow);
            }

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新玩家在线状态失败: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// 更新玩家当前房间
    /// </summary>
    public async Task<bool> UpdatePlayerCurrentRoomAsync(string playerId, string? roomId)
    {
        try
        {
            var filter = Builders<PlayerDocument>.Filter.Eq(x => x.PlayerId, playerId);
            var update = Builders<PlayerDocument>.Update
                .Set(x => x.CurrentRoomId, roomId)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新玩家房间失败: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// 更新玩家统计数据
    /// </summary>
    public async Task<bool> UpdatePlayerStatsAsync(string playerId, PlayerStatsDocument stats)
    {
        try
        {
            var filter = Builders<PlayerDocument>.Filter.Eq(x => x.PlayerId, playerId);
            var update = Builders<PlayerDocument>.Update
                .Set(x => x.Stats, stats)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新玩家统计数据失败: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// 删除玩家数据（软删除）
    /// </summary>
    public async Task<bool> DeletePlayerAsync(string playerId)
    {
        try
        {
            var filter = Builders<PlayerDocument>.Filter.Eq(x => x.PlayerId, playerId);
            var result = await _collection.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除玩家数据失败: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// 获取玩家数据统计信息
    /// </summary>
    public async Task<PlayerDataStatistics> GetPlayerStatisticsAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var stats = new PlayerDataStatistics();

            // 总玩家数
            stats.TotalPlayers = await _collection.CountDocumentsAsync(Builders<PlayerDocument>.Filter.Empty);

            // 在线玩家数
            var onlineFilter = Builders<PlayerDocument>.Filter.Ne(x => x.OnlineStatus, PlayerOnlineStatus.Offline);
            stats.OnlinePlayers = await _collection.CountDocumentsAsync(onlineFilter);

            // 今日新玩家
            var newPlayerFilter = Builders<PlayerDocument>.Filter.Gte(x => x.CreatedAt, today);
            stats.NewPlayersToday = await _collection.CountDocumentsAsync(newPlayerFilter);

            // 今日活跃玩家
            var activePlayerFilter = Builders<PlayerDocument>.Filter.Gte(x => x.LastActiveAt, today);
            stats.ActivePlayersToday = await _collection.CountDocumentsAsync(activePlayerFilter);

            // 按状态统计
            var statusGroupPipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = "$onlineStatus",
                    ["count"] = new BsonDocument("$sum", 1)
                })
            };

            var statusResults = await _collection.Aggregate<BsonDocument>(statusGroupPipeline).ToListAsync();
            foreach (var doc in statusResults)
            {
                if (Enum.TryParse<PlayerOnlineStatus>(doc["_id"].AsString, out var status))
                {
                    stats.PlayersByStatus[status] = doc["count"].AsInt64;
                }
            }

            // 按等级统计
            var levelGroupPipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = "$level",
                    ["count"] = new BsonDocument("$sum", 1)
                }),
                new BsonDocument("$sort", new BsonDocument("_id", 1))
            };

            var levelResults = await _collection.Aggregate<BsonDocument>(levelGroupPipeline).ToListAsync();
            foreach (var doc in levelResults)
            {
                stats.PlayersByLevel[doc["_id"].AsInt32] = doc["count"].AsInt64;
            }

            // 平均等级和总经验
            var avgPipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = BsonNull.Value,
                    ["avgLevel"] = new BsonDocument("$avg", "$level"),
                    ["totalExp"] = new BsonDocument("$sum", "$experience")
                })
            };

            var avgResults = await _collection.Aggregate<BsonDocument>(avgPipeline).FirstOrDefaultAsync();
            if (avgResults != null)
            {
                stats.AverageLevel = (float)avgResults["avgLevel"].AsDouble;
                stats.TotalExperience = avgResults["totalExp"].AsInt64;
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取玩家统计信息失败");
            throw;
        }
    }

    /// <summary>
    /// 清理过期的离线玩家数据
    /// </summary>
    public async Task<int> CleanupInactivePlayersAsync(TimeSpan inactiveThreshold)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - inactiveThreshold;
            var filter = Builders<PlayerDocument>.Filter.And(
                Builders<PlayerDocument>.Filter.Eq(x => x.OnlineStatus, PlayerOnlineStatus.Offline),
                Builders<PlayerDocument>.Filter.Lt(x => x.LastActiveAt, cutoffDate)
            );

            var result = await _collection.DeleteManyAsync(filter);
            _logger.LogInformation("清理了 {Count} 个长期离线玩家", result.DeletedCount);
            
            return (int)result.DeletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理离线玩家数据失败");
            throw;
        }
    }
}
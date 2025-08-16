using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Options;
using Wind.Server.Models.Documents;
using Wind.Server.Configuration;
using Wind.Shared.Models;

namespace Wind.Server.Services;

/// <summary>
/// 房间数据持久化服务实现
/// 提供完整的房间数据MongoDB操作
/// </summary>
public class RoomPersistenceService : IRoomPersistenceService
{
    private readonly MongoDbConnectionManager _connectionManager;
    private readonly IMongoCollection<RoomDocument> _collection;
    private readonly MongoDbOptions _options;
    private readonly ILogger<RoomPersistenceService> _logger;

    public RoomPersistenceService(
        MongoDbConnectionManager connectionManager,
        IOptions<MongoDbOptions> options,
        ILogger<RoomPersistenceService> logger)
    {
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = logger;
        _collection = _connectionManager.GetCollection<RoomDocument>(_options.Collections.Rooms);
    }

    /// <summary>
    /// 保存房间数据到MongoDB
    /// </summary>
    public async Task<string> SaveRoomAsync(RoomState roomState)
    {
        try
        {
            var existingRoom = await GetRoomByIdAsync(roomState.RoomId);
            var roomDoc = RoomDocument.FromRoomState(roomState, existingRoom?.Id);

            if (existingRoom != null)
            {
                // 更新现有房间
                var filter = Builders<RoomDocument>.Filter.Eq(x => x.RoomId, roomState.RoomId);
                await _collection.ReplaceOneAsync(filter, roomDoc);
                _logger.LogDebug("房间数据已更新: {RoomId}", roomState.RoomId);
                return existingRoom.Id!;
            }
            else
            {
                // 插入新房间
                await _collection.InsertOneAsync(roomDoc);
                _logger.LogDebug("新房间数据已保存: {RoomId}", roomState.RoomId);
                return roomDoc.Id!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存房间数据失败: {RoomId}", roomState.RoomId);
            throw;
        }
    }

    /// <summary>
    /// 批量保存房间数据
    /// </summary>
    public async Task<List<string>> SaveRoomsAsync(IEnumerable<RoomState> roomStates)
    {
        var results = new List<string>();
        var roomList = roomStates.ToList();

        try
        {
            var roomIds = roomList.Select(r => r.RoomId).ToList();
            var existingRooms = await _collection
                .Find(Builders<RoomDocument>.Filter.In(x => x.RoomId, roomIds))
                .ToListAsync();

            var existingRoomDict = existingRooms.ToDictionary(r => r.RoomId, r => r);
            var bulkOps = new List<WriteModel<RoomDocument>>();

            foreach (var roomState in roomList)
            {
                var existingRoom = existingRoomDict.GetValueOrDefault(roomState.RoomId);
                var roomDoc = RoomDocument.FromRoomState(roomState, existingRoom?.Id);

                if (existingRoom != null)
                {
                    var filter = Builders<RoomDocument>.Filter.Eq(x => x.RoomId, roomState.RoomId);
                    bulkOps.Add(new ReplaceOneModel<RoomDocument>(filter, roomDoc));
                    results.Add(existingRoom.Id!);
                }
                else
                {
                    bulkOps.Add(new InsertOneModel<RoomDocument>(roomDoc));
                    results.Add(roomDoc.Id ?? ObjectId.GenerateNewId().ToString());
                }
            }

            if (bulkOps.Count > 0)
            {
                await _collection.BulkWriteAsync(bulkOps);
                _logger.LogInformation("批量保存房间数据完成: {Count}个", bulkOps.Count);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量保存房间数据失败");
            throw;
        }
    }

    /// <summary>
    /// 根据房间ID获取房间数据
    /// </summary>
    public async Task<RoomDocument?> GetRoomByIdAsync(string roomId)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.Eq(x => x.RoomId, roomId);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取房间数据失败: {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// 搜索房间（按名称或描述）
    /// </summary>
    public async Task<List<RoomDocument>> SearchRoomsAsync(string searchText, int limit = 20)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.Text(searchText);
            return await _collection
                .Find(filter)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索房间失败: {SearchText}", searchText);
            throw;
        }
    }

    /// <summary>
    /// 获取活跃房间列表
    /// </summary>
    public async Task<List<RoomDocument>> GetActiveRoomsAsync(int limit = 100)
    {
        try
        {
            var activeStatuses = new[] { RoomStatus.Waiting, RoomStatus.Ready, RoomStatus.InGame };
            var filter = Builders<RoomDocument>.Filter.In(x => x.Status, activeStatuses);
            var sort = Builders<RoomDocument>.Sort.Descending(x => x.CreatedAt);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取活跃房间列表失败");
            throw;
        }
    }

    /// <summary>
    /// 获取指定玩家创建的房间
    /// </summary>
    public async Task<List<RoomDocument>> GetRoomsByOwnerAsync(string ownerId, int limit = 50)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.Eq(x => x.OwnerId, ownerId);
            var sort = Builders<RoomDocument>.Sort.Descending(x => x.CreatedAt);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取玩家创建的房间失败: {OwnerId}", ownerId);
            throw;
        }
    }

    /// <summary>
    /// 获取指定玩家参与的房间历史
    /// </summary>
    public async Task<List<RoomDocument>> GetRoomsByPlayerAsync(string playerId, int limit = 50)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.ElemMatch(x => x.Players, 
                p => p.PlayerId == playerId);
            var sort = Builders<RoomDocument>.Sort.Descending(x => x.CreatedAt);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取玩家参与的房间失败: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// 获取指定游戏模式的房间
    /// </summary>
    public async Task<List<RoomDocument>> GetRoomsByGameModeAsync(string gameMode, int page = 1, int pageSize = 50)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.Eq("settings.gameMode", gameMode);
            var sort = Builders<RoomDocument>.Sort.Descending(x => x.CreatedAt);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏模式房间失败: {GameMode}", gameMode);
            throw;
        }
    }

    /// <summary>
    /// 获取房间统计排行榜（按游戏时长）
    /// </summary>
    public async Task<List<RoomDocument>> GetRoomRankingByDurationAsync(int page = 1, int pageSize = 50)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.And(
                Builders<RoomDocument>.Filter.Ne(x => x.DurationSeconds, null),
                Builders<RoomDocument>.Filter.Gt(x => x.DurationSeconds, 0)
            );
            var sort = Builders<RoomDocument>.Sort.Descending(x => x.DurationSeconds);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取房间时长排行榜失败");
            throw;
        }
    }

    /// <summary>
    /// 获取最近完成的游戏房间
    /// </summary>
    public async Task<List<RoomDocument>> GetRecentCompletedRoomsAsync(TimeSpan timeRange, int limit = 100)
    {
        try
        {
            var since = DateTime.UtcNow - timeRange;
            var filter = Builders<RoomDocument>.Filter.And(
                Builders<RoomDocument>.Filter.Eq(x => x.Status, RoomStatus.Finished),
                Builders<RoomDocument>.Filter.Gte(x => x.EndedAt, since)
            );
            var sort = Builders<RoomDocument>.Sort.Descending(x => x.EndedAt);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最近完成房间失败");
            throw;
        }
    }

    /// <summary>
    /// 获取今日创建的房间
    /// </summary>
    public async Task<List<RoomDocument>> GetTodayRoomsAsync(int limit = 100)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var filter = Builders<RoomDocument>.Filter.Gte(x => x.CreatedAt, today);
            var sort = Builders<RoomDocument>.Sort.Descending(x => x.CreatedAt);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取今日房间失败");
            throw;
        }
    }

    /// <summary>
    /// 更新房间状态
    /// </summary>
    public async Task<bool> UpdateRoomStatusAsync(string roomId, RoomStatus status)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.Eq(x => x.RoomId, roomId);
            var update = Builders<RoomDocument>.Update
                .Set(x => x.Status, status)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新房间状态失败: {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// 更新房间玩家数量
    /// </summary>
    public async Task<bool> UpdateRoomPlayerCountAsync(string roomId, int playerCount)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.Eq(x => x.RoomId, roomId);
            var update = Builders<RoomDocument>.Update
                .Set(x => x.CurrentPlayerCount, playerCount)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新房间玩家数量失败: {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// 更新房间开始时间
    /// </summary>
    public async Task<bool> UpdateRoomStartTimeAsync(string roomId, DateTime startTime)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.Eq(x => x.RoomId, roomId);
            var update = Builders<RoomDocument>.Update
                .Set(x => x.StartedAt, startTime)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新房间开始时间失败: {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// 更新房间结束时间并计算持续时间
    /// </summary>
    public async Task<bool> UpdateRoomEndTimeAsync(string roomId, DateTime endTime)
    {
        try
        {
            var room = await GetRoomByIdAsync(roomId);
            if (room == null) return false;

            var filter = Builders<RoomDocument>.Filter.Eq(x => x.RoomId, roomId);
            var update = Builders<RoomDocument>.Update
                .Set(x => x.EndedAt, endTime)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            // 如果有开始时间，计算持续时间
            if (room.StartedAt.HasValue)
            {
                var duration = (int)(endTime - room.StartedAt.Value).TotalSeconds;
                update = update.Set(x => x.DurationSeconds, duration);
            }

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新房间结束时间失败: {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// 添加玩家到房间
    /// </summary>
    public async Task<bool> AddPlayerToRoomAsync(string roomId, RoomPlayerDocument player)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.Eq(x => x.RoomId, roomId);
            var update = Builders<RoomDocument>.Update
                .Push(x => x.Players, player)
                .Inc(x => x.CurrentPlayerCount, 1)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加玩家到房间失败: {RoomId}, {PlayerId}", roomId, player.PlayerId);
            throw;
        }
    }

    /// <summary>
    /// 从房间移除玩家
    /// </summary>
    public async Task<bool> RemovePlayerFromRoomAsync(string roomId, string playerId)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.Eq(x => x.RoomId, roomId);
            var update = Builders<RoomDocument>.Update
                .PullFilter(x => x.Players, p => p.PlayerId == playerId)
                .Inc(x => x.CurrentPlayerCount, -1)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从房间移除玩家失败: {RoomId}, {PlayerId}", roomId, playerId);
            throw;
        }
    }

    /// <summary>
    /// 删除房间数据
    /// </summary>
    public async Task<bool> DeleteRoomAsync(string roomId)
    {
        try
        {
            var filter = Builders<RoomDocument>.Filter.Eq(x => x.RoomId, roomId);
            var result = await _collection.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除房间数据失败: {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// 获取房间数据统计信息
    /// </summary>
    public async Task<RoomDataStatistics> GetRoomStatisticsAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var stats = new RoomDataStatistics();

            // 总房间数
            stats.TotalRooms = await _collection.CountDocumentsAsync(Builders<RoomDocument>.Filter.Empty);

            // 活跃房间数
            var activeStatuses = new[] { RoomStatus.Waiting, RoomStatus.Ready, RoomStatus.InGame };
            var activeFilter = Builders<RoomDocument>.Filter.In(x => x.Status, activeStatuses);
            stats.ActiveRooms = await _collection.CountDocumentsAsync(activeFilter);

            // 完成的房间数
            var completedFilter = Builders<RoomDocument>.Filter.Eq(x => x.Status, RoomStatus.Finished);
            stats.CompletedRooms = await _collection.CountDocumentsAsync(completedFilter);

            // 今日创建房间数
            var todayCreatedFilter = Builders<RoomDocument>.Filter.Gte(x => x.CreatedAt, today);
            stats.RoomsCreatedToday = await _collection.CountDocumentsAsync(todayCreatedFilter);

            // 今日完成房间数
            var todayCompletedFilter = Builders<RoomDocument>.Filter.And(
                Builders<RoomDocument>.Filter.Eq(x => x.Status, RoomStatus.Finished),
                Builders<RoomDocument>.Filter.Gte(x => x.EndedAt, today)
            );
            stats.RoomsCompletedToday = await _collection.CountDocumentsAsync(todayCompletedFilter);

            // 按状态统计
            var statusPipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = "$status",
                    ["count"] = new BsonDocument("$sum", 1)
                })
            };

            var statusResults = await _collection.Aggregate<BsonDocument>(statusPipeline).ToListAsync();
            foreach (var doc in statusResults)
            {
                if (Enum.TryParse<RoomStatus>(doc["_id"].AsString, out var status))
                {
                    stats.RoomsByStatus[status] = doc["count"].AsInt64;
                }
            }

            // 按类型统计
            var typePipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = "$type",
                    ["count"] = new BsonDocument("$sum", 1)
                })
            };

            var typeResults = await _collection.Aggregate<BsonDocument>(typePipeline).ToListAsync();
            foreach (var doc in typeResults)
            {
                if (Enum.TryParse<RoomType>(doc["_id"].AsString, out var type))
                {
                    stats.RoomsByType[type] = doc["count"].AsInt64;
                }
            }

            // 平均游戏时长和玩家数
            var avgPipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument("durationSeconds", new BsonDocument("$ne", BsonNull.Value))),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = BsonNull.Value,
                    ["avgDuration"] = new BsonDocument("$avg", "$durationSeconds"),
                    ["avgPlayers"] = new BsonDocument("$avg", "$currentPlayerCount"),
                    ["maxPlayers"] = new BsonDocument("$max", "$currentPlayerCount")
                })
            };

            var avgResults = await _collection.Aggregate<BsonDocument>(avgPipeline).FirstOrDefaultAsync();
            if (avgResults != null)
            {
                stats.AverageGameDuration = (float)avgResults["avgDuration"].AsDouble;
                stats.AveragePlayersPerRoom = (float)avgResults["avgPlayers"].AsDouble;
                stats.MaxPlayersInRoom = avgResults["maxPlayers"].AsInt32;
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取房间统计信息失败");
            throw;
        }
    }

    /// <summary>
    /// 获取游戏模式统计
    /// </summary>
    public async Task<Dictionary<string, GameModeStatistics>> GetGameModeStatisticsAsync()
    {
        try
        {
            var pipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = "$settings.gameMode",
                    ["totalRooms"] = new BsonDocument("$sum", 1),
                    ["completedRooms"] = new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$status", "Finished" }),
                        1,
                        0
                    })),
                    ["avgDuration"] = new BsonDocument("$avg", "$durationSeconds"),
                    ["avgPlayerCount"] = new BsonDocument("$avg", "$currentPlayerCount"),
                    ["totalPlayers"] = new BsonDocument("$sum", "$currentPlayerCount"),
                    ["lastPlayed"] = new BsonDocument("$max", "$createdAt")
                }),
                new BsonDocument("$addFields", new BsonDocument
                {
                    ["completionRate"] = new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gt", new BsonArray { "$totalRooms", 0 }),
                        new BsonDocument("$divide", new BsonArray { "$completedRooms", "$totalRooms" }),
                        0
                    })
                })
            };

            var results = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
            var statistics = new Dictionary<string, GameModeStatistics>();

            foreach (var doc in results)
            {
                var gameMode = doc["_id"].AsString;
                if (!string.IsNullOrEmpty(gameMode))
                {
                    statistics[gameMode] = new GameModeStatistics
                    {
                        GameMode = gameMode,
                        TotalRooms = doc["totalRooms"].AsInt64,
                        CompletedRooms = doc["completedRooms"].AsInt64,
                        CompletionRate = (float)doc["completionRate"].AsDouble,
                        AverageDuration = doc["avgDuration"].IsBsonNull ? 0 : (float)doc["avgDuration"].AsDouble,
                        AveragePlayerCount = (float)doc["avgPlayerCount"].AsDouble,
                        TotalPlayersServed = doc["totalPlayers"].AsInt64,
                        LastPlayed = doc["lastPlayed"].ToUniversalTime()
                    };
                }
            }

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏模式统计失败");
            throw;
        }
    }

    /// <summary>
    /// 清理过期的房间数据
    /// </summary>
    public async Task<int> CleanupExpiredRoomsAsync(TimeSpan expiredThreshold)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - expiredThreshold;
            var filter = Builders<RoomDocument>.Filter.And(
                Builders<RoomDocument>.Filter.In(x => x.Status, new[] { RoomStatus.Finished, RoomStatus.Closed }),
                Builders<RoomDocument>.Filter.Lt(x => x.UpdatedAt, cutoffDate)
            );

            var result = await _collection.DeleteManyAsync(filter);
            _logger.LogInformation("清理了 {Count} 个过期房间", result.DeletedCount);
            
            return (int)result.DeletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期房间数据失败");
            throw;
        }
    }
}
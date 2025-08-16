using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Options;
using Wind.Server.Models.Documents;
using Wind.Server.Configuration;

namespace Wind.Server.Services;

/// <summary>
/// 游戏记录持久化服务实现
/// 提供完整的游戏记录数据MongoDB操作
/// </summary>
public class GameRecordPersistenceService : IGameRecordPersistenceService
{
    private readonly MongoDbConnectionManager _connectionManager;
    private readonly IMongoCollection<GameRecordDocument> _collection;
    private readonly MongoDbOptions _options;
    private readonly ILogger<GameRecordPersistenceService> _logger;

    public GameRecordPersistenceService(
        MongoDbConnectionManager connectionManager,
        IOptions<MongoDbOptions> options,
        ILogger<GameRecordPersistenceService> logger)
    {
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = logger;
        _collection = _connectionManager.GetCollection<GameRecordDocument>(_options.Collections.GameRecords);
    }

    /// <summary>
    /// 保存游戏记录到MongoDB
    /// </summary>
    public async Task<string> SaveGameRecordAsync(GameRecordDocument gameRecord)
    {
        try
        {
            var existingRecord = await GetGameRecordByIdAsync(gameRecord.GameId);
            
            if (existingRecord != null)
            {
                // 更新现有记录
                gameRecord.Id = existingRecord.Id;
                var filter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameId, gameRecord.GameId);
                await _collection.ReplaceOneAsync(filter, gameRecord);
                _logger.LogDebug("游戏记录已更新: {GameId}", gameRecord.GameId);
                return existingRecord.Id!;
            }
            else
            {
                // 插入新记录
                await _collection.InsertOneAsync(gameRecord);
                _logger.LogDebug("新游戏记录已保存: {GameId}", gameRecord.GameId);
                return gameRecord.Id!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存游戏记录失败: {GameId}", gameRecord.GameId);
            throw;
        }
    }

    /// <summary>
    /// 批量保存游戏记录
    /// </summary>
    public async Task<List<string>> SaveGameRecordsAsync(IEnumerable<GameRecordDocument> gameRecords)
    {
        var results = new List<string>();
        var recordList = gameRecords.ToList();

        try
        {
            var gameIds = recordList.Select(r => r.GameId).ToList();
            var existingRecords = await _collection
                .Find(Builders<GameRecordDocument>.Filter.In(x => x.GameId, gameIds))
                .ToListAsync();

            var existingRecordDict = existingRecords.ToDictionary(r => r.GameId, r => r);
            var bulkOps = new List<WriteModel<GameRecordDocument>>();

            foreach (var gameRecord in recordList)
            {
                var existingRecord = existingRecordDict.GetValueOrDefault(gameRecord.GameId);

                if (existingRecord != null)
                {
                    gameRecord.Id = existingRecord.Id;
                    var filter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameId, gameRecord.GameId);
                    bulkOps.Add(new ReplaceOneModel<GameRecordDocument>(filter, gameRecord));
                    results.Add(existingRecord.Id!);
                }
                else
                {
                    bulkOps.Add(new InsertOneModel<GameRecordDocument>(gameRecord));
                    results.Add(gameRecord.Id ?? ObjectId.GenerateNewId().ToString());
                }
            }

            if (bulkOps.Count > 0)
            {
                await _collection.BulkWriteAsync(bulkOps);
                _logger.LogInformation("批量保存游戏记录完成: {Count}个", bulkOps.Count);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量保存游戏记录失败");
            throw;
        }
    }

    /// <summary>
    /// 根据游戏ID获取游戏记录
    /// </summary>
    public async Task<GameRecordDocument?> GetGameRecordByIdAsync(string gameId)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameId, gameId);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏记录失败: {GameId}", gameId);
            throw;
        }
    }

    /// <summary>
    /// 根据房间ID获取游戏记录
    /// </summary>
    public async Task<List<GameRecordDocument>> GetGameRecordsByRoomIdAsync(string roomId)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.Eq(x => x.RoomId, roomId);
            var sort = Builders<GameRecordDocument>.Sort.Descending(x => x.StartTime);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取房间游戏记录失败: {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// 获取指定玩家的游戏记录
    /// </summary>
    public async Task<List<GameRecordDocument>> GetGameRecordsByPlayerAsync(string playerId, int page = 1, int pageSize = 50)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.ElemMatch(x => x.Players, 
                p => p.PlayerId == playerId);
            var sort = Builders<GameRecordDocument>.Sort.Descending(x => x.StartTime);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取玩家游戏记录失败: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// 获取指定玩家的胜利记录
    /// </summary>
    public async Task<List<GameRecordDocument>> GetPlayerWinRecordsAsync(string playerId, int limit = 50)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.ElemMatch(x => x.Players, 
                p => p.PlayerId == playerId && p.IsWinner == true);
            var sort = Builders<GameRecordDocument>.Sort.Descending(x => x.StartTime);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取玩家胜利记录失败: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// 获取指定游戏模式的记录
    /// </summary>
    public async Task<List<GameRecordDocument>> GetGameRecordsByModeAsync(string gameMode, int page = 1, int pageSize = 50)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameMode, gameMode);
            var sort = Builders<GameRecordDocument>.Sort.Descending(x => x.StartTime);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏模式记录失败: {GameMode}", gameMode);
            throw;
        }
    }

    /// <summary>
    /// 获取指定时间范围内的游戏记录
    /// </summary>
    public async Task<List<GameRecordDocument>> GetGameRecordsByTimeRangeAsync(DateTime startTime, DateTime endTime, int limit = 100)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.And(
                Builders<GameRecordDocument>.Filter.Gte(x => x.StartTime, startTime),
                Builders<GameRecordDocument>.Filter.Lte(x => x.StartTime, endTime)
            );
            var sort = Builders<GameRecordDocument>.Sort.Descending(x => x.StartTime);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取时间范围游戏记录失败: {StartTime} - {EndTime}", startTime, endTime);
            throw;
        }
    }

    /// <summary>
    /// 获取最近完成的游戏记录
    /// </summary>
    public async Task<List<GameRecordDocument>> GetRecentCompletedGamesAsync(int limit = 100)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameStatus, GameStatus.Completed);
            var sort = Builders<GameRecordDocument>.Sort.Descending(x => x.EndTime);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最近完成游戏记录失败");
            throw;
        }
    }

    /// <summary>
    /// 获取进行中的游戏记录
    /// </summary>
    public async Task<List<GameRecordDocument>> GetInProgressGamesAsync(int limit = 50)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameStatus, GameStatus.InProgress);
            var sort = Builders<GameRecordDocument>.Sort.Descending(x => x.StartTime);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取进行中游戏记录失败");
            throw;
        }
    }

    /// <summary>
    /// 获取游戏时长排行榜
    /// </summary>
    public async Task<List<GameRecordDocument>> GetGameDurationRankingAsync(int page = 1, int pageSize = 50)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.And(
                Builders<GameRecordDocument>.Filter.Eq(x => x.GameStatus, GameStatus.Completed),
                Builders<GameRecordDocument>.Filter.Gt(x => x.DurationSeconds, 0)
            );
            var sort = Builders<GameRecordDocument>.Sort.Descending(x => x.DurationSeconds);

            return await _collection
                .Find(filter)
                .Sort(sort)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏时长排行榜失败");
            throw;
        }
    }

    /// <summary>
    /// 搜索游戏记录
    /// </summary>
    public async Task<List<GameRecordDocument>> SearchGameRecordsAsync(string searchText, int limit = 20)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.Or(
                Builders<GameRecordDocument>.Filter.Regex(x => x.GameId, new BsonRegularExpression(searchText, "i")),
                Builders<GameRecordDocument>.Filter.Regex(x => x.RoomId, new BsonRegularExpression(searchText, "i")),
                Builders<GameRecordDocument>.Filter.ElemMatch(x => x.Players, 
                    p => p.DisplayName.Contains(searchText))
            );

            return await _collection
                .Find(filter)
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索游戏记录失败: {SearchText}", searchText);
            throw;
        }
    }

    /// <summary>
    /// 添加游戏事件到记录
    /// </summary>
    public async Task<bool> AddGameEventAsync(string gameId, GameEventDocument gameEvent)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameId, gameId);
            var update = Builders<GameRecordDocument>.Update
                .Push(x => x.Events, gameEvent)
                .Inc("statistics.totalEvents", 1);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加游戏事件失败: {GameId}", gameId);
            throw;
        }
    }

    /// <summary>
    /// 批量添加游戏事件
    /// </summary>
    public async Task<bool> AddGameEventsAsync(string gameId, IEnumerable<GameEventDocument> gameEvents)
    {
        try
        {
            var eventList = gameEvents.ToList();
            var filter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameId, gameId);
            var update = Builders<GameRecordDocument>.Update
                .PushEach(x => x.Events, eventList)
                .Inc("statistics.totalEvents", eventList.Count);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量添加游戏事件失败: {GameId}", gameId);
            throw;
        }
    }

    /// <summary>
    /// 更新游戏状态
    /// </summary>
    public async Task<bool> UpdateGameStatusAsync(string gameId, GameStatus status)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameId, gameId);
            var update = Builders<GameRecordDocument>.Update.Set(x => x.GameStatus, status);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新游戏状态失败: {GameId}", gameId);
            throw;
        }
    }

    /// <summary>
    /// 更新游戏结束时间和结果
    /// </summary>
    public async Task<bool> UpdateGameEndAsync(string gameId, DateTime endTime, GameResult result)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameId, gameId);
            var update = Builders<GameRecordDocument>.Update
                .Set(x => x.EndTime, endTime)
                .Set(x => x.GameResult, result)
                .Set(x => x.GameStatus, GameStatus.Completed);

            // 计算游戏持续时间
            var gameRecord = await GetGameRecordByIdAsync(gameId);
            if (gameRecord != null)
            {
                var duration = (int)(endTime - gameRecord.StartTime).TotalSeconds;
                update = update.Set(x => x.DurationSeconds, duration);
            }

            var updateResult = await _collection.UpdateOneAsync(filter, update);
            return updateResult.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新游戏结束时间失败: {GameId}", gameId);
            throw;
        }
    }

    /// <summary>
    /// 更新玩家游戏结果
    /// </summary>
    public async Task<bool> UpdatePlayerGameResultAsync(string gameId, string playerId, bool isWinner, int finalScore, int finalRank)
    {
        try
        {
            var filter = Builders<GameRecordDocument>.Filter.And(
                Builders<GameRecordDocument>.Filter.Eq(x => x.GameId, gameId),
                Builders<GameRecordDocument>.Filter.ElemMatch(x => x.Players, p => p.PlayerId == playerId)
            );

            var update = Builders<GameRecordDocument>.Update
                .Set("players.$.isWinner", isWinner)
                .Set("players.$.finalScore", finalScore)
                .Set("players.$.finalRank", finalRank);

            var result = await _collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新玩家游戏结果失败: {GameId}, {PlayerId}", gameId, playerId);
            throw;
        }
    }

    /// <summary>
    /// 获取游戏统计信息
    /// </summary>
    public async Task<GameRecordStatistics> GetGameStatisticsAsync()
    {
        try
        {
            var stats = new GameRecordStatistics();

            // 总游戏数
            stats.TotalGames = await _collection.CountDocumentsAsync(Builders<GameRecordDocument>.Filter.Empty);

            // 完成的游戏数
            var completedFilter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameStatus, GameStatus.Completed);
            stats.CompletedGames = await _collection.CountDocumentsAsync(completedFilter);

            // 进行中的游戏数
            var inProgressFilter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameStatus, GameStatus.InProgress);
            stats.InProgressGames = await _collection.CountDocumentsAsync(inProgressFilter);

            // 中止的游戏数
            var abortedFilter = Builders<GameRecordDocument>.Filter.Eq(x => x.GameStatus, GameStatus.Aborted);
            stats.AbortedGames = await _collection.CountDocumentsAsync(abortedFilter);

            // 完成率
            if (stats.TotalGames > 0)
            {
                stats.CompletionRate = (float)stats.CompletedGames / stats.TotalGames;
            }

            // 聚合统计
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument("gameStatus", "Completed")),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = BsonNull.Value,
                    ["avgDuration"] = new BsonDocument("$avg", "$durationSeconds"),
                    ["avgPlayers"] = new BsonDocument("$avg", new BsonDocument("$size", "$players")),
                    ["totalPlayers"] = new BsonDocument("$sum", new BsonDocument("$size", "$players")),
                    ["totalEvents"] = new BsonDocument("$sum", new BsonDocument("$size", "$events"))
                })
            };

            var aggregateResults = await _collection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();
            if (aggregateResults != null)
            {
                stats.AverageGameDuration = aggregateResults["avgDuration"].IsBsonNull ? 0 : (float)aggregateResults["avgDuration"].AsDouble;
                stats.AveragePlayersPerGame = (float)aggregateResults["avgPlayers"].AsDouble;
                stats.TotalPlayersServed = aggregateResults["totalPlayers"].AsInt64;
                stats.TotalGameEvents = aggregateResults["totalEvents"].AsInt64;
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏统计信息失败");
            throw;
        }
    }

    /// <summary>
    /// 获取玩家游戏统计
    /// </summary>
    public async Task<PlayerGameStatistics> GetPlayerGameStatisticsAsync(string playerId)
    {
        try
        {
            var stats = new PlayerGameStatistics { PlayerId = playerId };

            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument("players.playerId", playerId)),
                new BsonDocument("$unwind", "$players"),
                new BsonDocument("$match", new BsonDocument("players.playerId", playerId)),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = BsonNull.Value,
                    ["totalGames"] = new BsonDocument("$sum", 1),
                    ["gamesWon"] = new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                    {
                        "$players.isWinner",
                        1,
                        0
                    })),
                    ["avgScore"] = new BsonDocument("$avg", "$players.finalScore"),
                    ["avgRank"] = new BsonDocument("$avg", "$players.finalRank"),
                    ["totalPlayTime"] = new BsonDocument("$sum", "$players.playTimeSeconds"),
                    ["avgGameDuration"] = new BsonDocument("$avg", "$durationSeconds"),
                    ["firstGame"] = new BsonDocument("$min", "$startTime"),
                    ["lastGame"] = new BsonDocument("$max", "$startTime")
                })
            };

            var result = await _collection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();
            if (result != null)
            {
                stats.TotalGamesPlayed = result["totalGames"].AsInt64;
                stats.GamesWon = result["gamesWon"].AsInt64;
                stats.GamesLost = stats.TotalGamesPlayed - stats.GamesWon;
                stats.WinRate = stats.TotalGamesPlayed > 0 ? (float)stats.GamesWon / stats.TotalGamesPlayed : 0;
                stats.AverageScore = (float)result["avgScore"].AsDouble;
                stats.AverageRank = (float)result["avgRank"].AsDouble;
                stats.TotalPlayTime = result["totalPlayTime"].AsInt64;
                stats.AverageGameDuration = (float)result["avgGameDuration"].AsDouble;
                stats.FirstGamePlayed = result["firstGame"].ToUniversalTime();
                stats.LastGamePlayed = result["lastGame"].ToUniversalTime();
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取玩家游戏统计失败: {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// 获取游戏模式统计
    /// </summary>
    public async Task<Dictionary<string, GameModeAnalytics>> GetGameModeAnalyticsAsync()
    {
        try
        {
            var pipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = "$gameMode",
                    ["totalGames"] = new BsonDocument("$sum", 1),
                    ["completedGames"] = new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$gameStatus", "Completed" }),
                        1,
                        0
                    })),
                    ["avgDuration"] = new BsonDocument("$avg", "$durationSeconds"),
                    ["avgPlayerCount"] = new BsonDocument("$avg", new BsonDocument("$size", "$players")),
                    ["totalPlayers"] = new BsonDocument("$sum", new BsonDocument("$size", "$players")),
                    ["totalEvents"] = new BsonDocument("$sum", new BsonDocument("$size", "$events")),
                    ["firstPlayed"] = new BsonDocument("$min", "$startTime"),
                    ["lastPlayed"] = new BsonDocument("$max", "$startTime")
                }),
                new BsonDocument("$addFields", new BsonDocument
                {
                    ["completionRate"] = new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gt", new BsonArray { "$totalGames", 0 }),
                        new BsonDocument("$divide", new BsonArray { "$completedGames", "$totalGames" }),
                        0
                    }),
                    ["avgEventsPerGame"] = new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gt", new BsonArray { "$totalGames", 0 }),
                        new BsonDocument("$divide", new BsonArray { "$totalEvents", "$totalGames" }),
                        0
                    })
                })
            };

            var results = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
            var analytics = new Dictionary<string, GameModeAnalytics>();

            foreach (var doc in results)
            {
                var gameMode = doc["_id"].AsString;
                if (!string.IsNullOrEmpty(gameMode))
                {
                    analytics[gameMode] = new GameModeAnalytics
                    {
                        GameMode = gameMode,
                        TotalGames = doc["totalGames"].AsInt64,
                        CompletedGames = doc["completedGames"].AsInt64,
                        CompletionRate = (float)doc["completionRate"].AsDouble,
                        AverageDuration = doc["avgDuration"].IsBsonNull ? 0 : (float)doc["avgDuration"].AsDouble,
                        AveragePlayerCount = (float)doc["avgPlayerCount"].AsDouble,
                        TotalPlayersServed = doc["totalPlayers"].AsInt64,
                        TotalEvents = doc["totalEvents"].AsInt64,
                        AverageEventsPerGame = (float)doc["avgEventsPerGame"].AsDouble,
                        FirstPlayed = doc["firstPlayed"].ToUniversalTime(),
                        LastPlayed = doc["lastPlayed"].ToUniversalTime()
                    };
                }
            }

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏模式分析失败");
            throw;
        }
    }

    /// <summary>
    /// 获取游戏事件统计
    /// </summary>
    public async Task<Dictionary<string, long>> GetGameEventStatisticsAsync(DateTime? since = null)
    {
        try
        {
            var matchStage = since.HasValue 
                ? new BsonDocument("$match", new BsonDocument("startTime", new BsonDocument("$gte", since.Value)))
                : new BsonDocument("$match", new BsonDocument());

            var pipeline = new[]
            {
                matchStage,
                new BsonDocument("$unwind", "$events"),
                new BsonDocument("$group", new BsonDocument
                {
                    ["_id"] = "$events.eventType",
                    ["count"] = new BsonDocument("$sum", 1)
                }),
                new BsonDocument("$sort", new BsonDocument("count", -1))
            };

            var results = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
            var statistics = new Dictionary<string, long>();

            foreach (var doc in results)
            {
                var eventType = doc["_id"].AsString;
                if (!string.IsNullOrEmpty(eventType))
                {
                    statistics[eventType] = doc["count"].AsInt64;
                }
            }

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取游戏事件统计失败");
            throw;
        }
    }

    /// <summary>
    /// 清理过期的游戏记录
    /// </summary>
    public async Task<int> CleanupExpiredGameRecordsAsync(TimeSpan expiredThreshold)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - expiredThreshold;
            var filter = Builders<GameRecordDocument>.Filter.Lt(x => x.CreatedAt, cutoffDate);

            var result = await _collection.DeleteManyAsync(filter);
            _logger.LogInformation("清理了 {Count} 个过期游戏记录", result.DeletedCount);
            
            return (int)result.DeletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期游戏记录失败");
            throw;
        }
    }

    /// <summary>
    /// 获取游戏性能报告
    /// </summary>
    public async Task<GamePerformanceReport> GetGamePerformanceReportAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var report = new GamePerformanceReport
            {
                ReportPeriodStart = startDate,
                ReportPeriodEnd = endDate
            };

            var filter = Builders<GameRecordDocument>.Filter.And(
                Builders<GameRecordDocument>.Filter.Gte(x => x.StartTime, startDate),
                Builders<GameRecordDocument>.Filter.Lte(x => x.StartTime, endDate)
            );

            // 基本统计
            var games = await _collection.Find(filter).ToListAsync();
            report.TotalGamesInPeriod = games.Count;

            if (games.Count > 0)
            {
                var completedGames = games.Where(g => g.GameStatus == GameStatus.Completed).ToList();
                report.GameCompletionRate = (float)completedGames.Count / games.Count;
                
                if (completedGames.Count > 0)
                {
                    report.AverageGameDuration = (float)completedGames.Average(g => g.DurationSeconds);
                    report.AveragePlayersPerGame = (float)completedGames.Average(g => g.Players.Count);
                }

                // 按小时分布
                var gamesByHour = games.GroupBy(g => g.StartTime.Hour)
                    .ToDictionary(g => g.Key, g => (long)g.Count());
                report.GamesByHour = gamesByHour;

                // 按天分布
                var gamesByDay = games.GroupBy(g => g.StartTime.DayOfWeek.ToString())
                    .ToDictionary(g => g.Key, g => (long)g.Count());
                report.GamesByDay = gamesByDay;

                // 热门游戏模式
                report.TopGameModes = games.GroupBy(g => g.GameMode)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList();
            }

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成游戏性能报告失败");
            throw;
        }
    }
}
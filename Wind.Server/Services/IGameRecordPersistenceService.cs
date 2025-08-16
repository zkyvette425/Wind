using Wind.Server.Models.Documents;

namespace Wind.Server.Services;

/// <summary>
/// 游戏记录持久化服务接口
/// 提供游戏记录数据的MongoDB持久化操作
/// </summary>
public interface IGameRecordPersistenceService
{
    /// <summary>
    /// 保存游戏记录到MongoDB
    /// </summary>
    Task<string> SaveGameRecordAsync(GameRecordDocument gameRecord);

    /// <summary>
    /// 批量保存游戏记录
    /// </summary>
    Task<List<string>> SaveGameRecordsAsync(IEnumerable<GameRecordDocument> gameRecords);

    /// <summary>
    /// 根据游戏ID获取游戏记录
    /// </summary>
    Task<GameRecordDocument?> GetGameRecordByIdAsync(string gameId);

    /// <summary>
    /// 根据房间ID获取游戏记录
    /// </summary>
    Task<List<GameRecordDocument>> GetGameRecordsByRoomIdAsync(string roomId);

    /// <summary>
    /// 获取指定玩家的游戏记录
    /// </summary>
    Task<List<GameRecordDocument>> GetGameRecordsByPlayerAsync(string playerId, int page = 1, int pageSize = 50);

    /// <summary>
    /// 获取指定玩家的胜利记录
    /// </summary>
    Task<List<GameRecordDocument>> GetPlayerWinRecordsAsync(string playerId, int limit = 50);

    /// <summary>
    /// 获取指定游戏模式的记录
    /// </summary>
    Task<List<GameRecordDocument>> GetGameRecordsByModeAsync(string gameMode, int page = 1, int pageSize = 50);

    /// <summary>
    /// 获取指定时间范围内的游戏记录
    /// </summary>
    Task<List<GameRecordDocument>> GetGameRecordsByTimeRangeAsync(DateTime startTime, DateTime endTime, int limit = 100);

    /// <summary>
    /// 获取最近完成的游戏记录
    /// </summary>
    Task<List<GameRecordDocument>> GetRecentCompletedGamesAsync(int limit = 100);

    /// <summary>
    /// 获取进行中的游戏记录
    /// </summary>
    Task<List<GameRecordDocument>> GetInProgressGamesAsync(int limit = 50);

    /// <summary>
    /// 获取游戏时长排行榜
    /// </summary>
    Task<List<GameRecordDocument>> GetGameDurationRankingAsync(int page = 1, int pageSize = 50);

    /// <summary>
    /// 搜索游戏记录（按游戏ID、房间ID或玩家）
    /// </summary>
    Task<List<GameRecordDocument>> SearchGameRecordsAsync(string searchText, int limit = 20);

    /// <summary>
    /// 添加游戏事件到记录
    /// </summary>
    Task<bool> AddGameEventAsync(string gameId, GameEventDocument gameEvent);

    /// <summary>
    /// 批量添加游戏事件
    /// </summary>
    Task<bool> AddGameEventsAsync(string gameId, IEnumerable<GameEventDocument> gameEvents);

    /// <summary>
    /// 更新游戏状态
    /// </summary>
    Task<bool> UpdateGameStatusAsync(string gameId, GameStatus status);

    /// <summary>
    /// 更新游戏结束时间和结果
    /// </summary>
    Task<bool> UpdateGameEndAsync(string gameId, DateTime endTime, GameResult result);

    /// <summary>
    /// 更新玩家游戏结果
    /// </summary>
    Task<bool> UpdatePlayerGameResultAsync(string gameId, string playerId, bool isWinner, int finalScore, int finalRank);

    /// <summary>
    /// 获取游戏统计信息
    /// </summary>
    Task<GameRecordStatistics> GetGameStatisticsAsync();

    /// <summary>
    /// 获取玩家游戏统计
    /// </summary>
    Task<PlayerGameStatistics> GetPlayerGameStatisticsAsync(string playerId);

    /// <summary>
    /// 获取游戏模式统计
    /// </summary>
    Task<Dictionary<string, GameModeAnalytics>> GetGameModeAnalyticsAsync();

    /// <summary>
    /// 获取游戏事件统计
    /// </summary>
    Task<Dictionary<string, long>> GetGameEventStatisticsAsync(DateTime? since = null);

    /// <summary>
    /// 清理过期的游戏记录
    /// </summary>
    Task<int> CleanupExpiredGameRecordsAsync(TimeSpan expiredThreshold);

    /// <summary>
    /// 获取游戏性能报告
    /// </summary>
    Task<GamePerformanceReport> GetGamePerformanceReportAsync(DateTime startDate, DateTime endDate);
}

/// <summary>
/// 游戏记录统计信息
/// </summary>
public class GameRecordStatistics
{
    public long TotalGames { get; set; }
    public long CompletedGames { get; set; }
    public long InProgressGames { get; set; }
    public long AbortedGames { get; set; }
    public float CompletionRate { get; set; }
    public float AverageGameDuration { get; set; }
    public float AveragePlayersPerGame { get; set; }
    public long TotalPlayersServed { get; set; }
    public long TotalGameEvents { get; set; }
    public Dictionary<GameStatus, long> GamesByStatus { get; set; } = new();
    public Dictionary<GameResult, long> GamesByResult { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 玩家游戏统计信息
/// </summary>
public class PlayerGameStatistics
{
    public string PlayerId { get; set; } = string.Empty;
    public long TotalGamesPlayed { get; set; }
    public long GamesWon { get; set; }
    public long GamesLost { get; set; }
    public float WinRate { get; set; }
    public float AverageScore { get; set; }
    public float AverageRank { get; set; }
    public long TotalPlayTime { get; set; }
    public float AverageGameDuration { get; set; }
    public Dictionary<string, long> GameModeStats { get; set; } = new();
    public List<string> Achievements { get; set; } = new();
    public DateTime FirstGamePlayed { get; set; }
    public DateTime LastGamePlayed { get; set; }
}

/// <summary>
/// 游戏模式分析数据
/// </summary>
public class GameModeAnalytics
{
    public string GameMode { get; set; } = string.Empty;
    public long TotalGames { get; set; }
    public long CompletedGames { get; set; }
    public float CompletionRate { get; set; }
    public float AverageDuration { get; set; }
    public float AveragePlayerCount { get; set; }
    public long TotalPlayersServed { get; set; }
    public long TotalEvents { get; set; }
    public float AverageEventsPerGame { get; set; }
    public Dictionary<string, long> EventTypeDistribution { get; set; } = new();
    public Dictionary<string, float> PerformanceMetrics { get; set; } = new();
    public DateTime FirstPlayed { get; set; }
    public DateTime LastPlayed { get; set; }
}

/// <summary>
/// 游戏性能报告
/// </summary>
public class GamePerformanceReport
{
    public DateTime ReportPeriodStart { get; set; }
    public DateTime ReportPeriodEnd { get; set; }
    public long TotalGamesInPeriod { get; set; }
    public float AverageGameDuration { get; set; }
    public float GameCompletionRate { get; set; }
    public float AveragePlayersPerGame { get; set; }
    public float PeakConcurrentGames { get; set; }
    public Dictionary<string, float> PerformanceMetrics { get; set; } = new();
    public Dictionary<int, long> GamesByHour { get; set; } = new(); // 24小时分布
    public Dictionary<string, long> GamesByDay { get; set; } = new(); // 一周分布
    public List<string> TopGameModes { get; set; } = new();
    public List<string> PerformanceIssues { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Wind.Server.Models.Documents;

/// <summary>
/// 游戏记录MongoDB文档模型
/// 用于存储完整的游戏会话记录，支持数据分析和回放
/// </summary>
public class GameRecordDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// 游戏记录唯一标识
    /// </summary>
    [BsonElement("gameId")]
    public string GameId { get; set; } = string.Empty;

    /// <summary>
    /// 关联的房间ID
    /// </summary>
    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// 游戏模式
    /// </summary>
    [BsonElement("gameMode")]
    public string GameMode { get; set; } = string.Empty;

    /// <summary>
    /// 地图ID
    /// </summary>
    [BsonElement("mapId")]
    public string MapId { get; set; } = string.Empty;

    /// <summary>
    /// 游戏开始时间
    /// </summary>
    [BsonElement("startTime")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 游戏结束时间
    /// </summary>
    [BsonElement("endTime")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 游戏持续时间(秒)
    /// </summary>
    [BsonElement("durationSeconds")]
    public int DurationSeconds { get; set; } = 0;

    /// <summary>
    /// 游戏状态
    /// </summary>
    [BsonElement("gameStatus")]
    [BsonRepresentation(BsonType.String)]
    public GameStatus GameStatus { get; set; } = GameStatus.InProgress;

    /// <summary>
    /// 游戏结果
    /// </summary>
    [BsonElement("gameResult")]
    [BsonRepresentation(BsonType.String)]
    public GameResult GameResult { get; set; } = GameResult.None;

    /// <summary>
    /// 参与玩家列表
    /// </summary>
    [BsonElement("players")]
    public List<GamePlayerDocument> Players { get; set; } = new();

    /// <summary>
    /// 游戏事件记录
    /// </summary>
    [BsonElement("events")]
    public List<GameEventDocument> Events { get; set; } = new();

    /// <summary>
    /// 游戏统计数据
    /// </summary>
    [BsonElement("statistics")]
    public GameStatisticsDocument Statistics { get; set; } = new();

    /// <summary>
    /// 游戏设置快照
    /// </summary>
    [BsonElement("gameSettings")]
    public Dictionary<string, object> GameSettings { get; set; } = new();

    /// <summary>
    /// 服务器版本
    /// </summary>
    [BsonElement("serverVersion")]
    public string ServerVersion { get; set; } = "1.2.0";

    /// <summary>
    /// 客户端版本统计
    /// </summary>
    [BsonElement("clientVersions")]
    public Dictionary<string, int> ClientVersions { get; set; } = new();

    /// <summary>
    /// 记录创建时间
    /// </summary>
    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 数据来源
    /// </summary>
    [BsonElement("dataSource")]
    public string DataSource { get; set; } = "GameEngine";

    /// <summary>
    /// 计算并更新游戏持续时间
    /// </summary>
    public void UpdateDuration()
    {
        if (EndTime.HasValue)
        {
            DurationSeconds = (int)(EndTime.Value - StartTime).TotalSeconds;
        }
    }

    /// <summary>
    /// 添加游戏事件
    /// </summary>
    public void AddEvent(GameEventDocument gameEvent)
    {
        Events.Add(gameEvent);
        gameEvent.EventSequence = Events.Count;
    }

    /// <summary>
    /// 获取指定玩家的游戏记录
    /// </summary>
    public GamePlayerDocument? GetPlayerRecord(string playerId)
    {
        return Players.FirstOrDefault(p => p.PlayerId == playerId);
    }

    /// <summary>
    /// 获取获胜玩家列表
    /// </summary>
    public List<GamePlayerDocument> GetWinners()
    {
        return Players.Where(p => p.IsWinner).ToList();
    }
}

/// <summary>
/// 游戏玩家记录文档
/// </summary>
public class GamePlayerDocument
{
    [BsonElement("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [BsonElement("team")]
    public int Team { get; set; } = 0;

    [BsonElement("joinTime")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime JoinTime { get; set; } = DateTime.UtcNow;

    [BsonElement("leaveTime")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LeaveTime { get; set; }

    [BsonElement("playTimeSeconds")]
    public int PlayTimeSeconds { get; set; } = 0;

    [BsonElement("isWinner")]
    public bool IsWinner { get; set; } = false;

    [BsonElement("finalScore")]
    public int FinalScore { get; set; } = 0;

    [BsonElement("finalRank")]
    public int FinalRank { get; set; } = 0;

    [BsonElement("disconnectReason")]
    public string? DisconnectReason { get; set; }

    [BsonElement("playerStats")]
    public Dictionary<string, object> PlayerStats { get; set; } = new();

    [BsonElement("achievements")]
    public List<string> Achievements { get; set; } = new();

    /// <summary>
    /// 计算玩家游戏时长
    /// </summary>
    public void UpdatePlayTime()
    {
        if (LeaveTime.HasValue)
        {
            PlayTimeSeconds = (int)(LeaveTime.Value - JoinTime).TotalSeconds;
        }
    }
}

/// <summary>
/// 游戏事件记录文档
/// </summary>
public class GameEventDocument
{
    [BsonElement("eventId")]
    public string EventId { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("eventType")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("eventSequence")]
    public int EventSequence { get; set; } = 0;

    [BsonElement("timestamp")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("playerId")]
    public string? PlayerId { get; set; }

    [BsonElement("targetPlayerId")]
    public string? TargetPlayerId { get; set; }

    [BsonElement("eventData")]
    public Dictionary<string, object> EventData { get; set; } = new();

    [BsonElement("severity")]
    [BsonRepresentation(BsonType.String)]
    public EventSeverity Severity { get; set; } = EventSeverity.Info;

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// 游戏统计数据文档
/// </summary>
public class GameStatisticsDocument
{
    [BsonElement("totalPlayers")]
    public int TotalPlayers { get; set; } = 0;

    [BsonElement("completionRate")]
    public float CompletionRate { get; set; } = 0f;

    [BsonElement("averagePlayTime")]
    public float AveragePlayTimeSeconds { get; set; } = 0f;

    [BsonElement("maxScore")]
    public int MaxScore { get; set; } = 0;

    [BsonElement("totalEvents")]
    public int TotalEvents { get; set; } = 0;

    [BsonElement("disconnectionCount")]
    public int DisconnectionCount { get; set; } = 0;

    [BsonElement("performanceMetrics")]
    public Dictionary<string, float> PerformanceMetrics { get; set; } = new();

    [BsonElement("balanceMetrics")]
    public Dictionary<string, object> BalanceMetrics { get; set; } = new();
}

/// <summary>
/// 游戏状态枚举
/// </summary>
public enum GameStatus
{
    Waiting = 0,
    InProgress = 1,
    Completed = 2,
    Aborted = 3,
    Error = 4
}

/// <summary>
/// 游戏结果枚举
/// </summary>
public enum GameResult
{
    None = 0,
    Victory = 1,
    Defeat = 2,
    Draw = 3,
    Timeout = 4,
    Disconnected = 5
}

/// <summary>
/// 事件严重性枚举
/// </summary>
public enum EventSeverity
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}
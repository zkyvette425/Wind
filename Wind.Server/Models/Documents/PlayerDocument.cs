using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Wind.Shared.Models;

namespace Wind.Server.Models.Documents;

/// <summary>
/// 玩家MongoDB文档模型
/// 映射自Orleans PlayerState，用于长期持久化存储
/// </summary>
public class PlayerDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// 玩家唯一标识
    /// </summary>
    [BsonElement("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>
    /// 文档版本，用于数据迁移和兼容性
    /// </summary>
    [BsonElement("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// 显示名称
    /// </summary>
    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 玩家等级
    /// </summary>
    [BsonElement("level")]
    public int Level { get; set; } = 1;

    /// <summary>
    /// 经验值
    /// </summary>
    [BsonElement("experience")]
    public long Experience { get; set; } = 0;

    /// <summary>
    /// 账户创建时间
    /// </summary>
    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后登录时间
    /// </summary>
    [BsonElement("lastLoginAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后活跃时间
    /// </summary>
    [BsonElement("lastActiveAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 在线状态
    /// </summary>
    [BsonElement("onlineStatus")]
    [BsonRepresentation(BsonType.String)]
    public PlayerOnlineStatus OnlineStatus { get; set; } = PlayerOnlineStatus.Offline;

    /// <summary>
    /// 当前房间ID
    /// </summary>
    [BsonElement("currentRoomId")]
    public string? CurrentRoomId { get; set; }

    /// <summary>
    /// 玩家位置信息
    /// </summary>
    [BsonElement("position")]
    public PlayerPositionDocument Position { get; set; } = new();

    /// <summary>
    /// 玩家统计信息
    /// </summary>
    [BsonElement("stats")]
    public PlayerStatsDocument Stats { get; set; } = new();

    /// <summary>
    /// 玩家设置信息
    /// </summary>
    [BsonElement("settings")]
    public PlayerSettingsDocument Settings { get; set; } = new();

    /// <summary>
    /// 最后一次数据更新时间 (MongoDB专用)
    /// </summary>
    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 数据来源标识 (Redis/MongoDB/Manual)
    /// </summary>
    [BsonElement("dataSource")]
    public string DataSource { get; set; } = "Redis";

    /// <summary>
    /// 从Orleans PlayerState映射
    /// </summary>
    public static PlayerDocument FromPlayerState(PlayerState state, string? mongoId = null)
    {
        return new PlayerDocument
        {
            Id = mongoId,
            PlayerId = state.PlayerId,
            Version = state.Version,
            DisplayName = state.DisplayName,
            Level = state.Level,
            Experience = state.Experience,
            CreatedAt = state.CreatedAt,
            LastLoginAt = state.LastLoginAt,
            LastActiveAt = state.LastActiveAt,
            OnlineStatus = state.OnlineStatus,
            CurrentRoomId = state.CurrentRoomId,
            Position = PlayerPositionDocument.FromPlayerPosition(state.Position),
            Stats = PlayerStatsDocument.FromPlayerStats(state.Stats),
            Settings = PlayerSettingsDocument.FromPlayerSettings(state.Settings),
            UpdatedAt = DateTime.UtcNow,
            DataSource = "Redis"
        };
    }

    /// <summary>
    /// 转换为Orleans PlayerState
    /// </summary>
    public PlayerState ToPlayerState()
    {
        return new PlayerState
        {
            PlayerId = PlayerId,
            Version = Version,
            DisplayName = DisplayName,
            Level = Level,
            Experience = Experience,
            CreatedAt = CreatedAt,
            LastLoginAt = LastLoginAt,
            LastActiveAt = LastActiveAt,
            OnlineStatus = OnlineStatus,
            CurrentRoomId = CurrentRoomId,
            Position = Position.ToPlayerPosition(),
            Stats = Stats.ToPlayerStats(),
            Settings = Settings.ToPlayerSettings(),
            Session = new PlayerSession() // Session数据不持久化到MongoDB
        };
    }
}

/// <summary>
/// 玩家位置MongoDB文档
/// </summary>
public class PlayerPositionDocument
{
    [BsonElement("x")]
    public float X { get; set; } = 0f;

    [BsonElement("y")]
    public float Y { get; set; } = 0f;

    [BsonElement("z")]
    public float Z { get; set; } = 0f;

    [BsonElement("rotation")]
    public float Rotation { get; set; } = 0f;

    [BsonElement("mapId")]
    public string MapId { get; set; } = string.Empty;

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static PlayerPositionDocument FromPlayerPosition(PlayerPosition position)
    {
        return new PlayerPositionDocument
        {
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            Rotation = position.Rotation,
            MapId = position.MapId,
            UpdatedAt = position.UpdatedAt
        };
    }

    public PlayerPosition ToPlayerPosition()
    {
        return new PlayerPosition
        {
            X = X,
            Y = Y,
            Z = Z,
            Rotation = Rotation,
            MapId = MapId,
            UpdatedAt = UpdatedAt
        };
    }
}

/// <summary>
/// 玩家统计MongoDB文档
/// </summary>
public class PlayerStatsDocument
{
    [BsonElement("gamesPlayed")]
    public int GamesPlayed { get; set; } = 0;

    [BsonElement("gamesWon")]
    public int GamesWon { get; set; } = 0;

    [BsonElement("gamesLost")]
    public int GamesLost { get; set; } = 0;

    [BsonElement("totalPlayTime")]
    public long TotalPlayTime { get; set; } = 0;

    [BsonElement("highestScore")]
    public int HighestScore { get; set; } = 0;

    [BsonElement("customStats")]
    public Dictionary<string, object> CustomStats { get; set; } = new();

    public static PlayerStatsDocument FromPlayerStats(PlayerStats stats)
    {
        return new PlayerStatsDocument
        {
            GamesPlayed = stats.GamesPlayed,
            GamesWon = stats.GamesWon,
            GamesLost = stats.GamesLost,
            TotalPlayTime = stats.TotalPlayTime,
            HighestScore = stats.HighestScore,
            CustomStats = new Dictionary<string, object>(stats.CustomStats)
        };
    }

    public PlayerStats ToPlayerStats()
    {
        return new PlayerStats
        {
            GamesPlayed = GamesPlayed,
            GamesWon = GamesWon,
            GamesLost = GamesLost,
            TotalPlayTime = TotalPlayTime,
            HighestScore = HighestScore,
            CustomStats = new Dictionary<string, object>(CustomStats)
        };
    }
}

/// <summary>
/// 玩家设置MongoDB文档
/// </summary>
public class PlayerSettingsDocument
{
    [BsonElement("language")]
    public string Language { get; set; } = "zh-CN";

    [BsonElement("timezone")]
    public string Timezone { get; set; } = "Asia/Shanghai";

    [BsonElement("enableNotifications")]
    public bool EnableNotifications { get; set; } = true;

    [BsonElement("enableSound")]
    public bool EnableSound { get; set; } = true;

    [BsonElement("soundVolume")]
    public float SoundVolume { get; set; } = 0.8f;

    [BsonElement("gameSettings")]
    public Dictionary<string, object> GameSettings { get; set; } = new();

    [BsonElement("uiSettings")]
    public Dictionary<string, string> UISettings { get; set; } = new();

    public static PlayerSettingsDocument FromPlayerSettings(PlayerSettings settings)
    {
        return new PlayerSettingsDocument
        {
            Language = settings.Language,
            Timezone = settings.Timezone,
            EnableNotifications = settings.EnableNotifications,
            EnableSound = settings.EnableSound,
            SoundVolume = settings.SoundVolume,
            GameSettings = new Dictionary<string, object>(settings.GameSettings),
            UISettings = new Dictionary<string, string>(settings.UISettings)
        };
    }

    public PlayerSettings ToPlayerSettings()
    {
        return new PlayerSettings
        {
            Language = Language,
            Timezone = Timezone,
            EnableNotifications = EnableNotifications,
            EnableSound = EnableSound,
            SoundVolume = SoundVolume,
            GameSettings = new Dictionary<string, object>(GameSettings),
            UISettings = new Dictionary<string, string>(UISettings)
        };
    }
}
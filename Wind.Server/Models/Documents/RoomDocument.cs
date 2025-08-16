using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Wind.Shared.Models;

namespace Wind.Server.Models.Documents;

/// <summary>
/// 房间MongoDB文档模型
/// 映射自Orleans RoomState，用于房间历史记录和分析
/// </summary>
public class RoomDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// 房间唯一标识
    /// </summary>
    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// 文档版本
    /// </summary>
    [BsonElement("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// 房间名称
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 房间描述
    /// </summary>
    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 房间类型
    /// </summary>
    [BsonElement("roomType")]
    [BsonRepresentation(BsonType.String)]
    public RoomType Type { get; set; } = RoomType.Normal;

    /// <summary>
    /// 房间状态
    /// </summary>
    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;

    /// <summary>
    /// 房主玩家ID
    /// </summary>
    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// 最大玩家数
    /// </summary>
    [BsonElement("maxPlayers")]
    public int MaxPlayers { get; set; } = 4;

    /// <summary>
    /// 当前玩家数
    /// </summary>
    [BsonElement("currentPlayerCount")]
    public int CurrentPlayerCount { get; set; } = 0;

    /// <summary>
    /// 玩家列表
    /// </summary>
    [BsonElement("players")]
    public List<RoomPlayerDocument> Players { get; set; } = new();

    /// <summary>
    /// 房间设置
    /// </summary>
    [BsonElement("settings")]
    public RoomSettingsDocument Settings { get; set; } = new();

    /// <summary>
    /// 房间创建时间
    /// </summary>
    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 房间开始时间
    /// </summary>
    [BsonElement("startedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 房间结束时间
    /// </summary>
    [BsonElement("endedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// 游戏持续时间(秒)
    /// </summary>
    [BsonElement("durationSeconds")]
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 数据来源
    /// </summary>
    [BsonElement("dataSource")]
    public string DataSource { get; set; } = "Redis";

    /// <summary>
    /// 房间统计信息
    /// </summary>
    [BsonElement("statistics")]
    public RoomStatisticsDocument Statistics { get; set; } = new();

    /// <summary>
    /// 从Orleans RoomState映射
    /// </summary>
    public static RoomDocument FromRoomState(RoomState state, string? mongoId = null)
    {
        var doc = new RoomDocument
        {
            Id = mongoId,
            RoomId = state.RoomId,
            Version = state.Version,
            Name = state.RoomName, // 正确的属性名
            Description = "", // RoomState 没有 Description 属性
            Type = state.RoomType, // 正确的属性名
            Status = state.Status,
            OwnerId = state.CreatorId, // 正确的属性名
            MaxPlayers = state.MaxPlayerCount, // 正确的属性名
            CurrentPlayerCount = state.Players.Count,
            Players = state.Players.Select(RoomPlayerDocument.FromRoomPlayer).ToList(),
            Settings = RoomSettingsDocument.FromRoomSettings(state.Settings),
            CreatedAt = state.CreatedAt,
            StartedAt = state.GameStartTime, // 正确的属性名
            EndedAt = state.GameEndTime, // 正确的属性名
            UpdatedAt = DateTime.UtcNow,
            DataSource = "Redis"
        };

        // 计算游戏时长
        if (state.GameStartTime.HasValue && state.GameEndTime.HasValue)
        {
            doc.DurationSeconds = (int)(state.GameEndTime.Value - state.GameStartTime.Value).TotalSeconds;
        }

        return doc;
    }

    /// <summary>
    /// 转换为Orleans RoomState
    /// </summary>
    public RoomState ToRoomState()
    {
        return new RoomState
        {
            RoomId = RoomId,
            Version = Version,
            RoomName = Name, // 正确的属性名
            RoomType = Type, // 正确的属性名
            Status = Status,
            CreatorId = OwnerId, // 正确的属性名
            MaxPlayerCount = MaxPlayers, // 正确的属性名
            CurrentPlayerCount = CurrentPlayerCount,
            Players = Players.Select(p => p.ToRoomPlayer()).ToList(),
            Settings = Settings.ToRoomSettings(),
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            GameStartTime = StartedAt, // 正确的属性名
            GameEndTime = EndedAt, // 正确的属性名
            Password = null, // 可以根据需要设置
            CustomData = new Dictionary<string, object>()
        };
    }
}

/// <summary>
/// 房间玩家MongoDB文档
/// </summary>
public class RoomPlayerDocument
{
    [BsonElement("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public PlayerReadyStatus Status { get; set; } = PlayerReadyStatus.NotReady;

    [BsonElement("team")]
    public int Team { get; set; } = 0;

    [BsonElement("joinedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("isHost")]
    public bool IsHost { get; set; } = false;

    public static RoomPlayerDocument FromRoomPlayer(RoomPlayer player)
    {
        return new RoomPlayerDocument
        {
            PlayerId = player.PlayerId,
            DisplayName = player.DisplayName,
            Status = player.ReadyStatus, // 正确的属性名
            Team = 0, // RoomPlayer 没有 Team 属性，设置默认值
            JoinedAt = player.JoinedAt,
            IsHost = player.Role == PlayerRole.Leader // 通过Role判断是否为主机
        };
    }

    public RoomPlayer ToRoomPlayer()
    {
        return new RoomPlayer
        {
            PlayerId = PlayerId,
            DisplayName = DisplayName,
            ReadyStatus = Status, // 正确的属性名
            JoinedAt = JoinedAt,
            Role = IsHost ? PlayerRole.Leader : PlayerRole.Member, // 通过IsHost设置Role
            Level = 1, // RoomPlayerDocument没有Level，设置默认值
            Position = new PlayerPosition(), // 设置默认位置
            Score = 0, // 设置默认分数
            PlayerData = new Dictionary<string, object>() // 设置默认数据
        };
    }
}

/// <summary>
/// 房间设置MongoDB文档
/// </summary>
public class RoomSettingsDocument
{
    [BsonElement("gameMode")]
    public string GameMode { get; set; } = string.Empty;

    [BsonElement("mapId")]
    public string MapId { get; set; } = string.Empty;

    [BsonElement("timeLimit")]
    public int TimeLimit { get; set; } = 0;

    [BsonElement("scoreLimit")]
    public int ScoreLimit { get; set; } = 0;

    [BsonElement("isPrivate")]
    public bool IsPrivate { get; set; } = false;

    [BsonElement("password")]
    public string Password { get; set; } = string.Empty;

    [BsonElement("customSettings")]
    public Dictionary<string, object> CustomSettings { get; set; } = new();

    public static RoomSettingsDocument FromRoomSettings(RoomSettings settings)
    {
        return new RoomSettingsDocument
        {
            GameMode = settings.GameMode,
            MapId = settings.MapId,
            TimeLimit = settings.GameDuration, // 正确的属性名
            ScoreLimit = settings.MaxScore, // 正确的属性名
            IsPrivate = settings.IsPrivate,
            Password = "", // RoomSettings没有Password属性，设置默认值
            CustomSettings = new Dictionary<string, object>(settings.CustomSettings)
        };
    }

    public RoomSettings ToRoomSettings()
    {
        return new RoomSettings
        {
            GameMode = GameMode,
            MapId = MapId,
            GameDuration = TimeLimit, // 正确的属性名
            MaxScore = ScoreLimit, // 正确的属性名
            IsPrivate = IsPrivate,
            EnableSpectators = true, // 设置默认值
            AutoStart = false, // 设置默认值
            MinPlayersToStart = 2, // 设置默认值
            GameRules = new Dictionary<string, object>(), // 设置默认值
            CustomSettings = new Dictionary<string, object>(CustomSettings)
        };
    }
}

/// <summary>
/// 房间统计信息MongoDB文档
/// </summary>
public class RoomStatisticsDocument
{
    [BsonElement("totalPlayersJoined")]
    public int TotalPlayersJoined { get; set; } = 0;

    [BsonElement("averagePlayerCount")]
    public float AveragePlayerCount { get; set; } = 0f;

    [BsonElement("maxPlayersReached")]
    public int MaxPlayersReached { get; set; } = 0;

    [BsonElement("playerJoinEvents")]
    public int PlayerJoinEvents { get; set; } = 0;

    [BsonElement("playerLeaveEvents")]
    public int PlayerLeaveEvents { get; set; } = 0;

    [BsonElement("gameCompletionRate")]
    public float GameCompletionRate { get; set; } = 0f;

    [BsonElement("customStatistics")]
    public Dictionary<string, object> CustomStatistics { get; set; } = new();
}
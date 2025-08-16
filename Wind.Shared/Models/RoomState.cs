using MessagePack;
using System;
using System.Collections.Generic;

namespace Wind.Shared.Models
{
    /// <summary>
    /// 房间状态数据模型
    /// 包含房间的基础信息、玩家列表、游戏设置和状态管理
    /// </summary>
    [MessagePackObject]
    public class RoomState
    {
        [Key(0)]
        public int Version { get; set; } = 1;

        [Key(1)]
        public string RoomId { get; set; } = string.Empty;

        [Key(2)]
        public string RoomName { get; set; } = string.Empty;

        [Key(3)]
        public string CreatorId { get; set; } = string.Empty;

        [Key(4)]
        public RoomType RoomType { get; set; } = RoomType.Normal;

        [Key(5)]
        public RoomStatus Status { get; set; } = RoomStatus.Waiting;

        [Key(6)]
        public int MaxPlayerCount { get; set; } = 4;

        [Key(7)]
        public int CurrentPlayerCount { get; set; } = 0;

        [Key(8)]
        public List<RoomPlayer> Players { get; set; } = new();

        [Key(9)]
        public RoomSettings Settings { get; set; } = new();

        [Key(10)]
        public RoomGameState GameState { get; set; } = new();

        [Key(11)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Key(12)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Key(13)]
        public DateTime? GameStartTime { get; set; }

        [Key(14)]
        public DateTime? GameEndTime { get; set; }

        [Key(15)]
        public string? Password { get; set; }

        [Key(16)]
        public Dictionary<string, object> CustomData { get; set; } = new();

        [Key(17)]
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 房间内玩家信息
    /// </summary>
    [MessagePackObject]
    public class RoomPlayer
    {
        [Key(0)]
        public string PlayerId { get; set; } = string.Empty;

        [Key(1)]
        public string DisplayName { get; set; } = string.Empty;

        [Key(2)]
        public int Level { get; set; } = 1;

        [Key(3)]
        public PlayerRole Role { get; set; } = PlayerRole.Member;

        [Key(4)]
        public PlayerReadyStatus ReadyStatus { get; set; } = PlayerReadyStatus.NotReady;

        [Key(5)]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        [Key(6)]
        public PlayerPosition Position { get; set; } = new();

        [Key(7)]
        public int Score { get; set; } = 0;

        [Key(8)]
        public Dictionary<string, object> PlayerData { get; set; } = new();
    }

    /// <summary>
    /// 房间设置信息
    /// </summary>
    [MessagePackObject]
    public class RoomSettings
    {
        [Key(0)]
        public string GameMode { get; set; } = "Default";

        [Key(1)]
        public string MapId { get; set; } = "DefaultMap";

        [Key(2)]
        public int GameDuration { get; set; } = 300; // 秒

        [Key(3)]
        public int MaxScore { get; set; } = 100;

        [Key(4)]
        public bool EnableSpectators { get; set; } = true;

        [Key(5)]
        public bool IsPrivate { get; set; } = false;

        [Key(6)]
        public bool AutoStart { get; set; } = false;

        [Key(7)]
        public int MinPlayersToStart { get; set; } = 2;

        [Key(8)]
        public Dictionary<string, object> GameRules { get; set; } = new();

        [Key(9)]
        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }

    /// <summary>
    /// 房间游戏状态信息
    /// </summary>
    [MessagePackObject]
    public class RoomGameState
    {
        [Key(0)]
        public int RoundNumber { get; set; } = 0;

        [Key(1)]
        public int ElapsedTime { get; set; } = 0; // 游戏已进行时间(秒)

        [Key(2)]
        public string? CurrentWinner { get; set; }

        [Key(3)]
        public List<string> Spectators { get; set; } = new();

        [Key(4)]
        public Dictionary<string, int> PlayerScores { get; set; } = new();

        [Key(5)]
        public Dictionary<string, object> GameData { get; set; } = new();

        [Key(6)]
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;

        [Key(7)]
        public List<RoomEvent> RecentEvents { get; set; } = new();
    }

    /// <summary>
    /// 房间事件记录
    /// </summary>
    [MessagePackObject]
    public class RoomEvent
    {
        [Key(0)]
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        [Key(1)]
        public RoomEventType EventType { get; set; }

        [Key(2)]
        public string? PlayerId { get; set; }

        [Key(3)]
        public string Description { get; set; } = string.Empty;

        [Key(4)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Key(5)]
        public Dictionary<string, object> EventData { get; set; } = new();
    }

    /// <summary>
    /// 房间类型枚举
    /// </summary>
    public enum RoomType
    {
        Normal = 0,     // 普通房间
        Ranked = 1,     // 排位房间  
        Private = 2,    // 私人房间
        Tournament = 3  // 比赛房间
    }

    /// <summary>
    /// 房间状态枚举
    /// </summary>
    public enum RoomStatus
    {
        Waiting = 0,    // 等待玩家
        Ready = 1,      // 准备开始
        InGame = 2,     // 游戏中
        Finished = 3,   // 游戏结束
        Closed = 4      // 房间已关闭
    }

    /// <summary>
    /// 玩家角色枚举
    /// </summary>
    public enum PlayerRole
    {
        Member = 0,     // 普通成员
        Leader = 1,     // 房主
        Admin = 2       // 管理员
    }

    /// <summary>
    /// 玩家准备状态枚举
    /// </summary>
    public enum PlayerReadyStatus
    {
        NotReady = 0,   // 未准备
        Ready = 1,      // 已准备
        Loading = 2     // 加载中
    }

    /// <summary>
    /// 房间事件类型枚举
    /// </summary>
    public enum RoomEventType
    {
        PlayerJoined = 0,       // 玩家加入
        PlayerLeft = 1,         // 玩家离开
        PlayerReady = 2,        // 玩家准备
        PlayerNotReady = 3,     // 玩家取消准备
        GameStarted = 4,        // 游戏开始
        GameEnded = 5,          // 游戏结束
        RoomSettingsChanged = 6, // 房间设置变更
        PlayerKicked = 7,       // 玩家被踢出
        RoomClosed = 8          // 房间关闭
    }
}
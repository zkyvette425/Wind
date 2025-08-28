using MessagePack;
using Orleans;
using System;
using System.Collections.Generic;

namespace Wind.Shared.Models
{
    /// <summary>
    /// 房间状态数据模型
    /// 包含房间的基础信息、玩家列表、游戏设置和状态管理
    /// </summary>
    [GenerateSerializer]
    [MessagePackObject]
    public class RoomState
    {
        [Id(0)][Key(0)]
        public int Version { get; set; } = 1;

        [Id(1)][Key(1)]
        public string RoomId { get; set; } = string.Empty;

        [Id(2)][Key(2)]
        public string RoomName { get; set; } = string.Empty;

        [Id(3)][Key(3)]
        public string CreatorId { get; set; } = string.Empty;

        [Id(4)][Key(4)]
        public RoomType RoomType { get; set; } = RoomType.Normal;

        [Id(5)][Key(5)]
        public RoomStatus Status { get; set; } = RoomStatus.Waiting;

        [Id(6)][Key(6)]
        public int MaxPlayerCount { get; set; } = 4;

        [Id(7)][Key(7)]
        public int CurrentPlayerCount { get; set; } = 0;

        [Id(8)][Key(8)]
        public List<RoomPlayer> Players { get; set; } = new();

        [Id(9)][Key(9)]
        public RoomSettings Settings { get; set; } = new();

        [Id(10)][Key(10)]
        public RoomGameState GameState { get; set; } = new();

        [Id(11)][Key(11)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Id(12)][Key(12)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Id(13)][Key(13)]
        public DateTime? GameStartTime { get; set; }

        [Id(14)][Key(14)]
        public DateTime? GameEndTime { get; set; }

        [Id(15)][Key(15)]
        public string? Password { get; set; }

        [Id(16)][Key(16)]
        public Dictionary<string, object> CustomData { get; set; } = new();

        [Id(17)][Key(17)]
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 房间内玩家信息
    /// </summary>
    [GenerateSerializer]
    [MessagePackObject]
    public class RoomPlayer
    {
        [Id(0)][Key(0)]
        public string PlayerId { get; set; } = string.Empty;

        [Id(1)][Key(1)]
        public string DisplayName { get; set; } = string.Empty;

        [Id(2)][Key(2)]
        public int Level { get; set; } = 1;

        [Id(3)][Key(3)]
        public PlayerRole Role { get; set; } = PlayerRole.Member;

        [Id(4)][Key(4)]
        public PlayerReadyStatus ReadyStatus { get; set; } = PlayerReadyStatus.NotReady;

        [Id(5)][Key(5)]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        [Id(6)][Key(6)]
        public PlayerPosition Position { get; set; } = new();

        [Id(7)][Key(7)]
        public int Score { get; set; } = 0;

        [Id(8)][Key(8)]
        public Dictionary<string, object> PlayerData { get; set; } = new();
    }

    /// <summary>
    /// 房间设置信息
    /// </summary>
    [GenerateSerializer]
    [MessagePackObject]
    public class RoomSettings
    {
        [Id(0)][Key(0)]
        public string GameMode { get; set; } = "Default";

        [Id(1)][Key(1)]
        public string MapId { get; set; } = "DefaultMap";

        [Id(2)][Key(2)]
        public int GameDuration { get; set; } = 300; // 秒

        [Id(3)][Key(3)]
        public int MaxScore { get; set; } = 100;

        [Id(4)][Key(4)]
        public bool EnableSpectators { get; set; } = true;

        [Id(5)][Key(5)]
        public bool IsPrivate { get; set; } = false;

        [Id(6)][Key(6)]
        public bool AutoStart { get; set; } = false;

        [Id(7)][Key(7)]
        public int MinPlayersToStart { get; set; } = 2;

        [Id(8)][Key(8)]
        public Dictionary<string, object> GameRules { get; set; } = new();

        [Id(9)][Key(9)]
        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }

    /// <summary>
    /// 房间游戏状态信息
    /// </summary>
    [GenerateSerializer]
    [MessagePackObject]
    public class RoomGameState
    {
        [Id(0)][Key(0)]
        public int RoundNumber { get; set; } = 0;

        [Id(1)][Key(1)]
        public int ElapsedTime { get; set; } = 0; // 游戏已进行时间(秒)

        [Id(2)][Key(2)]
        public string? CurrentWinner { get; set; }

        [Id(3)][Key(3)]
        public List<string> Spectators { get; set; } = new();

        [Id(4)][Key(4)]
        public Dictionary<string, int> PlayerScores { get; set; } = new();

        [Id(5)][Key(5)]
        public Dictionary<string, object> GameData { get; set; } = new();

        [Id(6)][Key(6)]
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;

        [Id(7)][Key(7)]
        public List<RoomEvent> RecentEvents { get; set; } = new();
    }

    /// <summary>
    /// 房间事件记录
    /// </summary>
    [GenerateSerializer]
    [MessagePackObject]
    public class RoomEvent
    {
        [Id(0)][Key(0)]
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        [Id(1)][Key(1)]
        public RoomEventType EventType { get; set; }

        [Id(2)][Key(2)]
        public string? PlayerId { get; set; }

        [Id(3)][Key(3)]
        public string Description { get; set; } = string.Empty;

        [Id(4)][Key(4)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Id(5)][Key(5)]
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
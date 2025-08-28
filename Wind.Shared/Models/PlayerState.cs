using MessagePack;
using Orleans;
using System;
using System.Collections.Generic;

namespace Wind.Shared.Models
{
    /// <summary>
    /// 玩家状态数据模型
    /// 包含玩家的基础信息、会话数据和游戏状态
    /// </summary>
    [GenerateSerializer]
    [MessagePackObject]
    public class PlayerState
    {
        [Id(0)][Key(0)]
        public int Version { get; set; } = 1;

        [Id(1)][Key(1)]
        public string PlayerId { get; set; } = string.Empty;

        [Id(2)][Key(2)]
        public string DisplayName { get; set; } = string.Empty;

        [Id(3)][Key(3)]
        public int Level { get; set; } = 1;

        [Id(4)][Key(4)]
        public long Experience { get; set; } = 0;

        [Id(5)][Key(5)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Id(6)][Key(6)]
        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

        [Id(7)][Key(7)]
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        [Id(8)][Key(8)]
        public PlayerOnlineStatus OnlineStatus { get; set; } = PlayerOnlineStatus.Offline;

        [Id(9)][Key(9)]
        public string? CurrentRoomId { get; set; }

        [Id(10)][Key(10)]
        public PlayerPosition Position { get; set; } = new();

        [Id(11)][Key(11)]
        public PlayerStats Stats { get; set; } = new();

        [Id(12)][Key(12)]
        public PlayerSettings Settings { get; set; } = new();

        [Id(13)][Key(13)]
        public PlayerSession Session { get; set; } = new();
    }

    /// <summary>
    /// 玩家位置信息
    /// </summary>
    [GenerateSerializer]
    [MessagePackObject]
    public class PlayerPosition
    {
        [Id(0)][Key(0)]
        public float X { get; set; } = 0f;

        [Id(1)][Key(1)]
        public float Y { get; set; } = 0f;

        [Id(2)][Key(2)]
        public float Z { get; set; } = 0f;

        [Id(3)][Key(3)]
        public float Rotation { get; set; } = 0f;

        [Id(4)][Key(4)]
        public string MapId { get; set; } = string.Empty;

        [Id(5)][Key(5)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 玩家统计信息
    /// </summary>
    [GenerateSerializer]
    [MessagePackObject]
    public class PlayerStats
    {
        [Id(0)][Key(0)]
        public int GamesPlayed { get; set; } = 0;

        [Id(1)][Key(1)]
        public int GamesWon { get; set; } = 0;

        [Id(2)][Key(2)]
        public int GamesLost { get; set; } = 0;

        [Id(3)][Key(3)]
        public long TotalPlayTime { get; set; } = 0;

        [Id(4)][Key(4)]
        public int HighestScore { get; set; } = 0;

        [Id(5)][Key(5)]
        public Dictionary<string, object> CustomStats { get; set; } = new();
    }

    /// <summary>
    /// 玩家设置信息
    /// </summary>
    [GenerateSerializer]
    [MessagePackObject]
    public class PlayerSettings
    {
        [Id(0)][Key(0)]
        public string Language { get; set; } = "zh-CN";

        [Id(1)][Key(1)]
        public string Timezone { get; set; } = "Asia/Shanghai";

        [Id(2)][Key(2)]
        public bool EnableNotifications { get; set; } = true;

        [Id(3)][Key(3)]
        public bool EnableSound { get; set; } = true;

        [Id(4)][Key(4)]
        public float SoundVolume { get; set; } = 0.8f;

        [Id(5)][Key(5)]
        public Dictionary<string, object> GameSettings { get; set; } = new();

        [Id(6)][Key(6)]
        public Dictionary<string, string> UISettings { get; set; } = new();
    }

    /// <summary>
    /// 玩家会话信息
    /// </summary>
    [GenerateSerializer]
    [MessagePackObject]
    public class PlayerSession
    {
        [Id(0)][Key(0)]
        public string SessionId { get; set; } = string.Empty;

        [Id(1)][Key(1)]
        public string AuthToken { get; set; } = string.Empty;

        [Id(2)][Key(2)]
        public DateTime SessionStartTime { get; set; } = DateTime.UtcNow;

        [Id(3)][Key(3)]
        public DateTime SessionExpireTime { get; set; } = DateTime.UtcNow.AddHours(24);

        [Id(4)][Key(4)]
        public string ClientVersion { get; set; } = string.Empty;

        [Id(5)][Key(5)]
        public string Platform { get; set; } = string.Empty;

        [Id(6)][Key(6)]
        public string DeviceId { get; set; } = string.Empty;

        [Id(7)][Key(7)]
        public string? ClientIP { get; set; }

        [Id(8)][Key(8)]
        public Dictionary<string, string> SessionData { get; set; } = new();
    }

    /// <summary>
    /// 玩家在线状态枚举
    /// </summary>
    public enum PlayerOnlineStatus
    {
        Offline = 0,
        Online = 1,
        Away = 2,
        Busy = 3,
        InGame = 4
    }
}
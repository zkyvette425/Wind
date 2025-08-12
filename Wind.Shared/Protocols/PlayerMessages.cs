using System;
using System.ComponentModel.DataAnnotations;
using Wind.Shared.Models;
using MessagePack;
using MPKey = MessagePack.KeyAttribute;

namespace Wind.Shared.Protocols
{
    /// <summary>
    /// 玩家登录请求
    /// </summary>
    [MessagePackObject]
    public class PlayerLoginRequest
    {
        [MPKey(0)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        public string? DisplayName { get; set; }

        [MPKey(2)]
        public string ClientVersion { get; set; } = string.Empty;

        [MPKey(3)]
        public string Platform { get; set; } = string.Empty;

        [MPKey(4)]
        public string DeviceId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 玩家登录响应
    /// </summary>
    [MessagePackObject]
    public class PlayerLoginResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public string? SessionId { get; set; }

        [MPKey(3)]
        public string? AuthToken { get; set; }

        [MPKey(4)]
        public PlayerInfo? PlayerInfo { get; set; }
    }

    /// <summary>
    /// 玩家信息DTO
    /// </summary>
    [MessagePackObject]
    public class PlayerInfo
    {
        [MPKey(0)]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        public string DisplayName { get; set; } = string.Empty;

        [MPKey(2)]
        public int Level { get; set; }

        [MPKey(3)]
        public long Experience { get; set; }

        [MPKey(4)]
        public PlayerOnlineStatus OnlineStatus { get; set; }

        [MPKey(5)]
        public DateTime LastLoginAt { get; set; }

        [MPKey(6)]
        public PlayerStats Stats { get; set; } = new();

        [MPKey(7)]
        public PlayerPosition Position { get; set; } = new();
    }

    /// <summary>
    /// 玩家状态更新请求
    /// </summary>
    [MessagePackObject]
    public class PlayerUpdateRequest
    {
        [MPKey(0)]
        public string? DisplayName { get; set; }

        [MPKey(1)]
        public PlayerPosition? Position { get; set; }

        [MPKey(2)]
        public PlayerOnlineStatus? OnlineStatus { get; set; }

        [MPKey(3)]
        public PlayerSettings? Settings { get; set; }

        [MPKey(4)]
        public int Version { get; set; } // 用于乐观锁
    }

    /// <summary>
    /// 玩家状态更新响应
    /// </summary>
    [MessagePackObject]
    public class PlayerUpdateResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public int NewVersion { get; set; }

        [MPKey(3)]
        public PlayerInfo? UpdatedPlayerInfo { get; set; }
    }

    /// <summary>
    /// 玩家登出请求
    /// </summary>
    [MessagePackObject]
    public class PlayerLogoutRequest
    {
        [MPKey(0)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// 玩家登出响应
    /// </summary>
    [MessagePackObject]
    public class PlayerLogoutResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;
    }

    // 其他类保持简单，仅为事件通知使用
    [MessagePackObject]
    public class PlayerOnlineStatusChangedEvent
    {
        [MPKey(0)]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        public string DisplayName { get; set; } = string.Empty;

        [MPKey(2)]
        public PlayerOnlineStatus OldStatus { get; set; }

        [MPKey(3)]
        public PlayerOnlineStatus NewStatus { get; set; }

        [MPKey(4)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    [MessagePackObject]
    public class PlayerPositionUpdateEvent
    {
        [MPKey(0)]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        public PlayerPosition Position { get; set; } = new();

        [MPKey(2)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    [MessagePackObject]
    public class GetPlayerInfoRequest
    {
        [MPKey(0)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        public bool IncludeStats { get; set; } = true;

        [MPKey(2)]
        public bool IncludeSettings { get; set; } = false;
    }

    [MessagePackObject]
    public class GetPlayerInfoResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public PlayerInfo? PlayerInfo { get; set; }

        [MPKey(3)]
        public PlayerSettings? Settings { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Wind.Shared.Models;
using MessagePack;
using MPKey = MessagePack.KeyAttribute;

namespace Wind.Shared.Protocols
{
    // ======== 房间创建相关消息 ========

    /// <summary>
    /// 创建房间请求
    /// </summary>
    [MessagePackObject]
    public class CreateRoomRequest
    {
        [MPKey(0)]
        [Required]
        public string CreatorId { get; set; } = string.Empty;

        [MPKey(1)]
        [Required]
        public string RoomName { get; set; } = string.Empty;

        [MPKey(2)]
        public RoomType RoomType { get; set; } = RoomType.Normal;

        [MPKey(3)]
        public int MaxPlayerCount { get; set; } = 4;

        [MPKey(4)]
        public string? Password { get; set; }

        [MPKey(5)]
        public RoomSettings Settings { get; set; } = new();

        [MPKey(6)]
        public Dictionary<string, object> CustomData { get; set; } = new();
    }

    /// <summary>
    /// 创建房间响应
    /// </summary>
    [MessagePackObject]
    public class CreateRoomResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public string? RoomId { get; set; }

        [MPKey(3)]
        public RoomState? RoomInfo { get; set; }
    }

    // ======== 房间加入相关消息 ========

    /// <summary>
    /// 加入房间请求
    /// </summary>
    [MessagePackObject]
    public class JoinRoomRequest
    {
        [MPKey(0)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        [Required]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(2)]
        public string? Password { get; set; }

        [MPKey(3)]
        public bool IsSpectator { get; set; } = false;

        [MPKey(4)]
        public Dictionary<string, object> PlayerData { get; set; } = new();
    }

    /// <summary>
    /// 加入房间响应
    /// </summary>
    [MessagePackObject]
    public class JoinRoomResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public RoomState? RoomInfo { get; set; }

        [MPKey(3)]
        public RoomPlayer? PlayerInfo { get; set; }
    }

    // ======== 房间离开相关消息 ========

    /// <summary>
    /// 离开房间请求
    /// </summary>
    [MessagePackObject]
    public class LeaveRoomRequest
    {
        [MPKey(0)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        [Required]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(2)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// 离开房间响应
    /// </summary>
    [MessagePackObject]
    public class LeaveRoomResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;
    }

    // ======== 房间信息查询消息 ========

    /// <summary>
    /// 获取房间信息请求
    /// </summary>
    [MessagePackObject]
    public class GetRoomInfoRequest
    {
        [MPKey(0)]
        [Required]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(1)]
        public bool IncludePlayerDetails { get; set; } = true;

        [MPKey(2)]
        public bool IncludeGameState { get; set; } = true;
    }

    /// <summary>
    /// 获取房间信息响应
    /// </summary>
    [MessagePackObject]
    public class GetRoomInfoResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public RoomState? RoomInfo { get; set; }
    }

    // ======== 房间列表查询消息 ========

    /// <summary>
    /// 获取房间列表请求
    /// </summary>
    [MessagePackObject]
    public class GetRoomListRequest
    {
        [MPKey(0)]
        public RoomType? RoomType { get; set; }

        [MPKey(1)]
        public RoomStatus? Status { get; set; }

        [MPKey(2)]
        public bool IncludePrivate { get; set; } = false;

        [MPKey(3)]
        public bool IncludeFull { get; set; } = false;

        [MPKey(4)]
        public int PageIndex { get; set; } = 0;

        [MPKey(5)]
        public int PageSize { get; set; } = 20;

        [MPKey(6)]
        public Dictionary<string, object> Filters { get; set; } = new();
    }

    /// <summary>
    /// 获取房间列表响应
    /// </summary>
    [MessagePackObject]
    public class GetRoomListResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public List<RoomBrief> Rooms { get; set; } = new();

        [MPKey(3)]
        public int TotalCount { get; set; }

        [MPKey(4)]
        public int PageIndex { get; set; }

        [MPKey(5)]
        public int PageSize { get; set; }
    }

    /// <summary>
    /// 房间简要信息
    /// </summary>
    [MessagePackObject]
    public class RoomBrief
    {
        [MPKey(0)]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(1)]
        public string RoomName { get; set; } = string.Empty;

        [MPKey(2)]
        public RoomType RoomType { get; set; }

        [MPKey(3)]
        public RoomStatus Status { get; set; }

        [MPKey(4)]
        public int CurrentPlayerCount { get; set; }

        [MPKey(5)]
        public int MaxPlayerCount { get; set; }

        [MPKey(6)]
        public bool HasPassword { get; set; }

        [MPKey(7)]
        public string CreatorName { get; set; } = string.Empty;

        [MPKey(8)]
        public DateTime CreatedAt { get; set; }

        [MPKey(9)]
        public string GameMode { get; set; } = string.Empty;

        [MPKey(10)]
        public string MapId { get; set; } = string.Empty;
    }

    // ======== 房间设置更新消息 ========

    /// <summary>
    /// 更新房间设置请求
    /// </summary>
    [MessagePackObject]
    public class UpdateRoomSettingsRequest
    {
        [MPKey(0)]
        [Required]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(1)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(2)]
        public RoomSettings Settings { get; set; } = new();
    }

    /// <summary>
    /// 更新房间设置响应
    /// </summary>
    [MessagePackObject]
    public class UpdateRoomSettingsResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public RoomSettings? UpdatedSettings { get; set; }
    }

    // ======== 玩家准备状态消息 ========

    /// <summary>
    /// 玩家准备状态请求
    /// </summary>
    [MessagePackObject]
    public class PlayerReadyRequest
    {
        [MPKey(0)]
        [Required]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(1)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(2)]
        public PlayerReadyStatus ReadyStatus { get; set; }
    }

    /// <summary>
    /// 玩家准备状态响应
    /// </summary>
    [MessagePackObject]
    public class PlayerReadyResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public PlayerReadyStatus ReadyStatus { get; set; }
    }

    // ======== 游戏控制消息 ========

    /// <summary>
    /// 开始游戏请求
    /// </summary>
    [MessagePackObject]
    public class StartGameRequest
    {
        [MPKey(0)]
        [Required]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(1)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(2)]
        public bool ForceStart { get; set; } = false;
    }

    /// <summary>
    /// 开始游戏响应
    /// </summary>
    [MessagePackObject]
    public class StartGameResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public DateTime? GameStartTime { get; set; }

        [MPKey(3)]
        public RoomGameState? GameState { get; set; }
    }

    /// <summary>
    /// 结束游戏请求
    /// </summary>
    [MessagePackObject]
    public class EndGameRequest
    {
        [MPKey(0)]
        [Required]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(1)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(2)]
        public string? Reason { get; set; }

        [MPKey(3)]
        public Dictionary<string, int> FinalScores { get; set; } = new();
    }

    /// <summary>
    /// 结束游戏响应
    /// </summary>
    [MessagePackObject]
    public class EndGameResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public DateTime? GameEndTime { get; set; }

        [MPKey(3)]
        public Dictionary<string, int> FinalScores { get; set; } = new();

        [MPKey(4)]
        public string? Winner { get; set; }
    }

    // ======== 房间事件通知消息 ========

    /// <summary>
    /// 房间事件通知
    /// </summary>
    [MessagePackObject]
    public class RoomEventNotification
    {
        [MPKey(0)]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(1)]
        public RoomEventType EventType { get; set; }

        [MPKey(2)]
        public string? PlayerId { get; set; }

        [MPKey(3)]
        public string Message { get; set; } = string.Empty;

        [MPKey(4)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MPKey(5)]
        public Dictionary<string, object> EventData { get; set; } = new();
    }

    /// <summary>
    /// 房间状态同步通知
    /// </summary>
    [MessagePackObject]
    public class RoomStateChangedNotification
    {
        [MPKey(0)]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(1)]
        public RoomState RoomState { get; set; } = new();

        [MPKey(2)]
        public RoomEventType ChangeType { get; set; }

        [MPKey(3)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 踢出玩家请求
    /// </summary>
    [MessagePackObject]
    public class KickPlayerRequest
    {
        [MPKey(0)]
        [Required]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(1)]
        [Required]
        public string OperatorId { get; set; } = string.Empty;

        [MPKey(2)]
        [Required]
        public string TargetPlayerId { get; set; } = string.Empty;

        [MPKey(3)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// 踢出玩家响应
    /// </summary>
    [MessagePackObject]
    public class KickPlayerResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;
    }
}
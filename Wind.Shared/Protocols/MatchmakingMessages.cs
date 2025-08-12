using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Wind.Shared.Models;
using MessagePack;
using MPKey = MessagePack.KeyAttribute;

namespace Wind.Shared.Protocols
{
    // ======== 快速匹配相关消息 ========

    /// <summary>
    /// 快速匹配请求
    /// </summary>
    [MessagePackObject]
    public class QuickMatchRequest
    {
        [MPKey(0)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        public string PlayerName { get; set; } = string.Empty;

        [MPKey(2)]
        public int PlayerLevel { get; set; } = 1;

        [MPKey(3)]
        public MatchmakingCriteria Criteria { get; set; } = new();

        [MPKey(4)]
        public Dictionary<string, object> PlayerData { get; set; } = new();
    }

    /// <summary>
    /// 快速匹配响应
    /// </summary>
    [MessagePackObject]
    public class QuickMatchResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public string? RequestId { get; set; }

        [MPKey(3)]
        public MatchmakingResult? Result { get; set; }

        [MPKey(4)]
        public int EstimatedWaitTime { get; set; } // 秒
    }

    // ======== 自定义匹配相关消息 ========

    /// <summary>
    /// 加入匹配队列请求
    /// </summary>
    [MessagePackObject]
    public class JoinMatchmakingQueueRequest
    {
        [MPKey(0)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        [Required]
        public string QueueId { get; set; } = string.Empty;

        [MPKey(2)]
        public MatchmakingCriteria Criteria { get; set; } = new();

        [MPKey(3)]
        public Dictionary<string, object> PlayerData { get; set; } = new();
    }

    /// <summary>
    /// 加入匹配队列响应
    /// </summary>
    [MessagePackObject]
    public class JoinMatchmakingQueueResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public string? RequestId { get; set; }

        [MPKey(3)]
        public int QueuePosition { get; set; }

        [MPKey(4)]
        public int EstimatedWaitTime { get; set; } // 秒
    }

    // ======== 取消匹配相关消息 ========

    /// <summary>
    /// 取消匹配请求
    /// </summary>
    [MessagePackObject]
    public class CancelMatchmakingRequest
    {
        [MPKey(0)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        public string? RequestId { get; set; }
    }

    /// <summary>
    /// 取消匹配响应
    /// </summary>
    [MessagePackObject]
    public class CancelMatchmakingResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;
    }

    // ======== 匹配状态查询消息 ========

    /// <summary>
    /// 获取匹配状态请求
    /// </summary>
    [MessagePackObject]
    public class GetMatchmakingStatusRequest
    {
        [MPKey(0)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        public string? RequestId { get; set; }
    }

    /// <summary>
    /// 获取匹配状态响应
    /// </summary>
    [MessagePackObject]
    public class GetMatchmakingStatusResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public MatchmakingRequest? Request { get; set; }

        [MPKey(3)]
        public int QueuePosition { get; set; }

        [MPKey(4)]
        public TimeSpan CurrentWaitTime { get; set; }

        [MPKey(5)]
        public int EstimatedRemainingTime { get; set; } // 秒
    }

    // ======== 队列管理消息 ========

    /// <summary>
    /// 获取匹配队列列表请求
    /// </summary>
    [MessagePackObject]
    public class GetMatchmakingQueuesRequest
    {
        [MPKey(0)]
        public RoomType? FilterRoomType { get; set; }

        [MPKey(1)]
        public string? FilterGameMode { get; set; }

        [MPKey(2)]
        public bool IncludeInactive { get; set; } = false;
    }

    /// <summary>
    /// 获取匹配队列列表响应
    /// </summary>
    [MessagePackObject]
    public class GetMatchmakingQueuesResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public List<MatchmakingQueueInfo> Queues { get; set; } = new();
    }

    /// <summary>
    /// 匹配队列简要信息
    /// </summary>
    [MessagePackObject]
    public class MatchmakingQueueInfo
    {
        [MPKey(0)]
        public string QueueId { get; set; } = string.Empty;

        [MPKey(1)]
        public string QueueName { get; set; } = string.Empty;

        [MPKey(2)]
        public RoomType RoomType { get; set; }

        [MPKey(3)]
        public string GameMode { get; set; } = string.Empty;

        [MPKey(4)]
        public int PlayersInQueue { get; set; }

        [MPKey(5)]
        public TimeSpan AverageWaitTime { get; set; }

        [MPKey(6)]
        public bool IsActive { get; set; }

        [MPKey(7)]
        public MatchmakingQueueSettings Settings { get; set; } = new();
    }

    // ======== 匹配统计信息消息 ========

    /// <summary>
    /// 获取匹配统计信息请求
    /// </summary>
    [MessagePackObject]
    public class GetMatchmakingStatisticsRequest
    {
        [MPKey(0)]
        public bool IncludeQueueDetails { get; set; } = true;

        [MPKey(1)]
        public bool IncludeHistoricalData { get; set; } = false;
    }

    /// <summary>
    /// 获取匹配统计信息响应
    /// </summary>
    [MessagePackObject]
    public class GetMatchmakingStatisticsResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public MatchmakingStatistics? Statistics { get; set; }

        [MPKey(3)]
        public Dictionary<string, MatchmakingQueueInfo> QueueDetails { get; set; } = new();
    }

    // ======== 匹配事件通知消息 ========

    /// <summary>
    /// 匹配成功通知
    /// </summary>
    [MessagePackObject]
    public class MatchFoundNotification
    {
        [MPKey(0)]
        public string RequestId { get; set; } = string.Empty;

        [MPKey(1)]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(2)]
        public string RoomId { get; set; } = string.Empty;

        [MPKey(3)]
        public List<string> MatchedPlayerIds { get; set; } = new();

        [MPKey(4)]
        public TimeSpan WaitTime { get; set; }

        [MPKey(5)]
        public DateTime MatchedAt { get; set; } = DateTime.UtcNow;

        [MPKey(6)]
        public RoomState? RoomInfo { get; set; }
    }

    /// <summary>
    /// 匹配状态更新通知
    /// </summary>
    [MessagePackObject]
    public class MatchmakingStatusUpdateNotification
    {
        [MPKey(0)]
        public string RequestId { get; set; } = string.Empty;

        [MPKey(1)]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(2)]
        public MatchmakingRequestStatus Status { get; set; }

        [MPKey(3)]
        public string Message { get; set; } = string.Empty;

        [MPKey(4)]
        public int QueuePosition { get; set; }

        [MPKey(5)]
        public TimeSpan CurrentWaitTime { get; set; }

        [MPKey(6)]
        public int EstimatedRemainingTime { get; set; }

        [MPKey(7)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 匹配失败通知
    /// </summary>
    [MessagePackObject]
    public class MatchFailedNotification
    {
        [MPKey(0)]
        public string RequestId { get; set; } = string.Empty;

        [MPKey(1)]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(2)]
        public string Reason { get; set; } = string.Empty;

        [MPKey(3)]
        public MatchmakingRequestStatus FinalStatus { get; set; }

        [MPKey(4)]
        public TimeSpan TotalWaitTime { get; set; }

        [MPKey(5)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MPKey(6)]
        public bool CanRetry { get; set; } = true;
    }
}
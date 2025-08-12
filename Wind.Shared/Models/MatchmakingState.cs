using MessagePack;
using System;
using System.Collections.Generic;

namespace Wind.Shared.Models
{
    /// <summary>
    /// 匹配系统状态数据模型
    /// 管理玩家匹配队列和匹配条件
    /// </summary>
    [MessagePackObject]
    public class MatchmakingState
    {
        [Key(0)]
        public int Version { get; set; } = 1;

        [Key(1)]
        public string MatchmakingId { get; set; } = string.Empty;

        [Key(2)]
        public Dictionary<string, MatchmakingQueue> Queues { get; set; } = new();

        [Key(3)]
        public Dictionary<string, MatchmakingRequest> ActiveRequests { get; set; } = new();

        [Key(4)]
        public MatchmakingSettings Settings { get; set; } = new();

        [Key(5)]
        public MatchmakingStatistics Statistics { get; set; } = new();

        [Key(6)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Key(7)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 匹配队列信息
    /// </summary>
    [MessagePackObject]
    public class MatchmakingQueue
    {
        [Key(0)]
        public string QueueId { get; set; } = string.Empty;

        [Key(1)]
        public string QueueName { get; set; } = string.Empty;

        [Key(2)]
        public RoomType RoomType { get; set; } = RoomType.Normal;

        [Key(3)]
        public string GameMode { get; set; } = "Default";

        [Key(4)]
        public List<MatchmakingRequest> WaitingPlayers { get; set; } = new();

        [Key(5)]
        public MatchmakingQueueSettings QueueSettings { get; set; } = new();

        [Key(6)]
        public int TotalPlayersInQueue { get; set; } = 0;

        [Key(7)]
        public DateTime LastMatchTime { get; set; } = DateTime.UtcNow;

        [Key(8)]
        public TimeSpan AverageWaitTime { get; set; } = TimeSpan.Zero;

        [Key(9)]
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// 匹配请求信息
    /// </summary>
    [MessagePackObject]
    public class MatchmakingRequest
    {
        [Key(0)]
        public string RequestId { get; set; } = Guid.NewGuid().ToString();

        [Key(1)]
        public string PlayerId { get; set; } = string.Empty;

        [Key(2)]
        public string PlayerName { get; set; } = string.Empty;

        [Key(3)]
        public int PlayerLevel { get; set; } = 1;

        [Key(4)]
        public string QueueId { get; set; } = string.Empty;

        [Key(5)]
        public MatchmakingCriteria Criteria { get; set; } = new();

        [Key(6)]
        public MatchmakingRequestStatus Status { get; set; } = MatchmakingRequestStatus.Queued;

        [Key(7)]
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        [Key(8)]
        public DateTime? MatchedAt { get; set; }

        [Key(9)]
        public string? MatchedRoomId { get; set; }

        [Key(10)]
        public int RetryCount { get; set; } = 0;

        [Key(11)]
        public TimeSpan CurrentWaitTime => DateTime.UtcNow - RequestedAt;

        [Key(12)]
        public Dictionary<string, object> PlayerData { get; set; } = new();
    }

    /// <summary>
    /// 匹配条件
    /// </summary>
    [MessagePackObject]
    public class MatchmakingCriteria
    {
        [Key(0)]
        public RoomType PreferredRoomType { get; set; } = RoomType.Normal;

        [Key(1)]
        public string PreferredGameMode { get; set; } = "Default";

        [Key(2)]
        public string? PreferredMapId { get; set; }

        [Key(3)]
        public int MinPlayerLevel { get; set; } = 1;

        [Key(4)]
        public int MaxPlayerLevel { get; set; } = 999;

        [Key(5)]
        public int PreferredPlayerCount { get; set; } = 4;

        [Key(6)]
        public int MinPlayerCount { get; set; } = 2;

        [Key(7)]
        public int MaxPlayerCount { get; set; } = 8;

        [Key(8)]
        public string? PreferredRegion { get; set; }

        [Key(9)]
        public int MaxPing { get; set; } = 200;

        [Key(10)]
        public bool AllowSpectating { get; set; } = false;

        [Key(11)]
        public bool CreateNewRoomIfNeeded { get; set; } = true;

        [Key(12)]
        public Dictionary<string, object> CustomCriteria { get; set; } = new();
    }

    /// <summary>
    /// 匹配队列设置
    /// </summary>
    [MessagePackObject]
    public class MatchmakingQueueSettings
    {
        [Key(0)]
        public int MaxPlayersPerMatch { get; set; } = 4;

        [Key(1)]
        public int MinPlayersPerMatch { get; set; } = 2;

        [Key(2)]
        public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromMinutes(5);

        [Key(3)]
        public TimeSpan MatchCheckInterval { get; set; } = TimeSpan.FromSeconds(5);

        [Key(4)]
        public int LevelDifferenceThreshold { get; set; } = 10;

        [Key(5)]
        public int ExpandLevelDifferenceAfter { get; set; } = 30; // 秒

        [Key(6)]
        public bool EnableSkillBasedMatching { get; set; } = false;

        [Key(7)]
        public bool EnableRegionPriority { get; set; } = true;

        [Key(8)]
        public bool AllowBackfill { get; set; } = true; // 允许加入进行中的游戏

        [Key(9)]
        public Dictionary<string, object> AdvancedSettings { get; set; } = new();
    }

    /// <summary>
    /// 匹配系统设置
    /// </summary>
    [MessagePackObject]
    public class MatchmakingSettings
    {
        [Key(0)]
        public bool EnableMatchmaking { get; set; } = true;

        [Key(1)]
        public int MaxConcurrentMatches { get; set; } = 1000;

        [Key(2)]
        public int MaxQueueSize { get; set; } = 10000;

        [Key(3)]
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

        [Key(4)]
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(10);

        [Key(5)]
        public int MaxRetryCount { get; set; } = 3;

        [Key(6)]
        public bool EnableLogging { get; set; } = true;

        [Key(7)]
        public bool EnableStatistics { get; set; } = true;

        [Key(8)]
        public Dictionary<string, object> GlobalSettings { get; set; } = new();
    }

    /// <summary>
    /// 匹配统计信息
    /// </summary>
    [MessagePackObject]
    public class MatchmakingStatistics
    {
        [Key(0)]
        public int TotalMatchesMade { get; set; } = 0;

        [Key(1)]
        public int TotalPlayersMatched { get; set; } = 0;

        [Key(2)]
        public TimeSpan AverageMatchTime { get; set; } = TimeSpan.Zero;

        [Key(3)]
        public int CurrentPlayersInQueue { get; set; } = 0;

        [Key(4)]
        public int FailedMatches { get; set; } = 0;

        [Key(5)]
        public int CancelledRequests { get; set; } = 0;

        [Key(6)]
        public int TimeoutRequests { get; set; } = 0;

        [Key(7)]
        public DateTime LastResetTime { get; set; } = DateTime.UtcNow;

        [Key(8)]
        public Dictionary<string, int> QueueStatistics { get; set; } = new();

        [Key(9)]
        public Dictionary<string, TimeSpan> QueueWaitTimes { get; set; } = new();
    }

    /// <summary>
    /// 匹配结果
    /// </summary>
    [MessagePackObject]
    public class MatchmakingResult
    {
        [Key(0)]
        public bool Success { get; set; }

        [Key(1)]
        public string Message { get; set; } = string.Empty;

        [Key(2)]
        public MatchmakingResultType ResultType { get; set; }

        [Key(3)]
        public string? RoomId { get; set; }

        [Key(4)]
        public List<string> MatchedPlayerIds { get; set; } = new();

        [Key(5)]
        public TimeSpan WaitTime { get; set; }

        [Key(6)]
        public DateTime MatchedAt { get; set; } = DateTime.UtcNow;

        [Key(7)]
        public Dictionary<string, object> ResultData { get; set; } = new();
    }

    /// <summary>
    /// 匹配请求状态枚举
    /// </summary>
    public enum MatchmakingRequestStatus
    {
        Queued = 0,         // 排队中
        Matching = 1,       // 匹配中
        Matched = 2,        // 已匹配
        Cancelled = 3,      // 已取消
        Timeout = 4,        // 超时
        Failed = 5          // 失败
    }

    /// <summary>
    /// 匹配结果类型枚举
    /// </summary>
    public enum MatchmakingResultType
    {
        JoinedExistingRoom = 0,     // 加入现有房间
        CreatedNewRoom = 1,         // 创建新房间
        AddedToQueue = 2,           // 添加到队列
        MatchTimeout = 3,           // 匹配超时
        MatchCancelled = 4,         // 匹配取消
        MatchFailed = 5             // 匹配失败
    }
}
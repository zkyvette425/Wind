using Orleans;
using Wind.Shared.Models;
using Wind.Shared.Protocols;

namespace Wind.GrainInterfaces
{
    /// <summary>
    /// 匹配系统Grain接口
    /// 提供玩家匹配、队列管理、统计信息等功能
    /// </summary>
    public interface IMatchmakingGrain : IGrainWithStringKey
    {
        /// <summary>
        /// 快速匹配
        /// </summary>
        /// <param name="request">快速匹配请求</param>
        /// <returns>匹配结果</returns>
        Task<QuickMatchResponse> QuickMatchAsync(QuickMatchRequest request);

        /// <summary>
        /// 加入匹配队列
        /// </summary>
        /// <param name="request">加入队列请求</param>
        /// <returns>加入结果</returns>
        Task<JoinMatchmakingQueueResponse> JoinQueueAsync(JoinMatchmakingQueueRequest request);

        /// <summary>
        /// 取消匹配
        /// </summary>
        /// <param name="request">取消匹配请求</param>
        /// <returns>取消结果</returns>
        Task<CancelMatchmakingResponse> CancelMatchmakingAsync(CancelMatchmakingRequest request);

        /// <summary>
        /// 获取匹配状态
        /// </summary>
        /// <param name="request">状态查询请求</param>
        /// <returns>匹配状态</returns>
        Task<GetMatchmakingStatusResponse> GetMatchmakingStatusAsync(GetMatchmakingStatusRequest request);

        /// <summary>
        /// 获取匹配队列列表
        /// </summary>
        /// <param name="request">队列查询请求</param>
        /// <returns>队列列表</returns>
        Task<GetMatchmakingQueuesResponse> GetQueuesAsync(GetMatchmakingQueuesRequest request);

        /// <summary>
        /// 获取匹配统计信息
        /// </summary>
        /// <param name="request">统计信息请求</param>
        /// <returns>统计信息</returns>
        Task<GetMatchmakingStatisticsResponse> GetStatisticsAsync(GetMatchmakingStatisticsRequest request);

        /// <summary>
        /// 初始化匹配系统
        /// </summary>
        /// <param name="settings">系统设置</param>
        /// <returns>是否成功</returns>
        Task<bool> InitializeAsync(MatchmakingSettings settings);

        /// <summary>
        /// 创建匹配队列
        /// </summary>
        /// <param name="queueId">队列ID</param>
        /// <param name="queueName">队列名称</param>
        /// <param name="roomType">房间类型</param>
        /// <param name="gameMode">游戏模式</param>
        /// <param name="settings">队列设置</param>
        /// <returns>是否成功</returns>
        Task<bool> CreateQueueAsync(string queueId, string queueName, RoomType roomType, string gameMode, MatchmakingQueueSettings? settings = null);

        /// <summary>
        /// 删除匹配队列
        /// </summary>
        /// <param name="queueId">队列ID</param>
        /// <returns>是否成功</returns>
        Task<bool> RemoveQueueAsync(string queueId);

        /// <summary>
        /// 更新队列设置
        /// </summary>
        /// <param name="queueId">队列ID</param>
        /// <param name="settings">新设置</param>
        /// <returns>是否成功</returns>
        Task<bool> UpdateQueueSettingsAsync(string queueId, MatchmakingQueueSettings settings);

        /// <summary>
        /// 启用或禁用队列
        /// </summary>
        /// <param name="queueId">队列ID</param>
        /// <param name="isActive">是否启用</param>
        /// <returns>是否成功</returns>
        Task<bool> SetQueueActiveAsync(string queueId, bool isActive);

        /// <summary>
        /// 手动触发匹配检查
        /// </summary>
        /// <param name="queueId">队列ID，null表示检查所有队列</param>
        /// <returns>匹配成功的数量</returns>
        Task<int> TriggerMatchCheckAsync(string? queueId = null);

        /// <summary>
        /// 清理过期请求
        /// </summary>
        /// <returns>清理的请求数量</returns>
        Task<int> CleanupExpiredRequestsAsync();

        /// <summary>
        /// 获取玩家当前匹配请求
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>匹配请求，如果没有则返回null</returns>
        Task<MatchmakingRequest?> GetPlayerRequestAsync(string playerId);

        /// <summary>
        /// 获取队列中的玩家数量
        /// </summary>
        /// <param name="queueId">队列ID</param>
        /// <returns>玩家数量</returns>
        Task<int> GetQueuePlayerCountAsync(string queueId);

        /// <summary>
        /// 获取队列平均等待时间
        /// </summary>
        /// <param name="queueId">队列ID</param>
        /// <returns>平均等待时间</returns>
        Task<TimeSpan> GetQueueAverageWaitTimeAsync(string queueId);

        /// <summary>
        /// 重置匹配统计信息
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> ResetStatisticsAsync();

        /// <summary>
        /// 获取匹配系统健康状态
        /// </summary>
        /// <returns>健康状态信息</returns>
        Task<MatchmakingHealthStatus> GetHealthStatusAsync();

        /// <summary>
        /// 设置匹配系统设置
        /// </summary>
        /// <param name="settings">新设置</param>
        /// <returns>是否成功</returns>
        Task<bool> UpdateSettingsAsync(MatchmakingSettings settings);

        /// <summary>
        /// 强制移除玩家的匹配请求
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="reason">移除原因</param>
        /// <returns>是否成功</returns>
        Task<bool> ForceRemovePlayerRequestAsync(string playerId, string reason);
    }

    /// <summary>
    /// 匹配系统健康状态
    /// </summary>
    [MessagePack.MessagePackObject]
    public class MatchmakingHealthStatus
    {
        [MessagePack.Key(0)]
        public bool IsHealthy { get; set; } = true;

        [MessagePack.Key(1)]
        public string SystemStatus { get; set; } = "Healthy";

        [MessagePack.Key(2)]
        public int TotalActiveQueues { get; set; }

        [MessagePack.Key(3)]
        public int TotalPlayersInQueues { get; set; }

        [MessagePack.Key(4)]
        public int TotalActiveRequests { get; set; }

        [MessagePack.Key(5)]
        public TimeSpan Uptime { get; set; }

        [MessagePack.Key(6)]
        public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;

        [MessagePack.Key(7)]
        public List<string> Issues { get; set; } = new();

        [MessagePack.Key(8)]
        public Dictionary<string, object> PerformanceMetrics { get; set; } = new();
    }
}
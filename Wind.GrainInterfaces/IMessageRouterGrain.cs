using Orleans;
using Wind.Shared.Protocols;

namespace Wind.GrainInterfaces
{
    /// <summary>
    /// 消息路由Grain接口
    /// 负责消息的分发、队列管理和可靠投递
    /// </summary>
    public interface IMessageRouterGrain : IGrainWithStringKey
    {
        // ======== 核心消息路由功能 ========

        /// <summary>
        /// 发送单个消息
        /// </summary>
        /// <param name="request">发送消息请求</param>
        /// <returns>发送结果</returns>
        Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request);

        /// <summary>
        /// 批量发送消息
        /// </summary>
        /// <param name="request">批量发送请求</param>
        /// <returns>批量发送结果</returns>
        Task<BatchSendMessageResponse> SendBatchMessagesAsync(BatchSendMessageRequest request);

        /// <summary>
        /// 订阅消息
        /// </summary>
        /// <param name="request">订阅请求</param>
        /// <returns>订阅结果</returns>
        Task<SubscribeMessageResponse> SubscribeAsync(SubscribeMessageRequest request);

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <param name="request">取消订阅请求</param>
        /// <returns>取消订阅结果</returns>
        Task<UnsubscribeMessageResponse> UnsubscribeAsync(UnsubscribeMessageRequest request);

        /// <summary>
        /// 确认消息接收
        /// </summary>
        /// <param name="request">确认请求</param>
        /// <returns>确认结果</returns>
        Task<MessageAcknowledgmentResponse> AcknowledgeMessageAsync(MessageAcknowledgmentRequest request);

        // ======== 消息查询和历史记录 ========

        /// <summary>
        /// 获取消息历史
        /// </summary>
        /// <param name="request">历史查询请求</param>
        /// <returns>历史消息列表</returns>
        Task<GetMessageHistoryResponse> GetMessageHistoryAsync(GetMessageHistoryRequest request);

        /// <summary>
        /// 获取待处理的消息数量
        /// </summary>
        /// <param name="subscriberId">订阅者ID</param>
        /// <returns>待处理消息数量</returns>
        Task<int> GetPendingMessageCountAsync(string subscriberId);

        /// <summary>
        /// 获取失败的消息列表
        /// </summary>
        /// <param name="subscriberId">订阅者ID</param>
        /// <param name="limit">限制数量</param>
        /// <returns>失败消息列表</returns>
        Task<List<BaseMessage>> GetFailedMessagesAsync(string subscriberId, int limit = 50);

        // ======== 队列管理功能 ========

        /// <summary>
        /// 清空指定订阅者的消息队列
        /// </summary>
        /// <param name="subscriberId">订阅者ID</param>
        /// <returns>清空的消息数量</returns>
        Task<int> ClearQueueAsync(string subscriberId);

        /// <summary>
        /// 暂停指定订阅者的消息投递
        /// </summary>
        /// <param name="subscriberId">订阅者ID</param>
        /// <returns>操作是否成功</returns>
        Task<bool> PauseDeliveryAsync(string subscriberId);

        /// <summary>
        /// 恢复指定订阅者的消息投递
        /// </summary>
        /// <param name="subscriberId">订阅者ID</param>
        /// <returns>操作是否成功</returns>
        Task<bool> ResumeDeliveryAsync(string subscriberId);

        /// <summary>
        /// 重试失败的消息
        /// </summary>
        /// <param name="messageId">消息ID</param>
        /// <returns>重试结果</returns>
        Task<SendMessageResponse> RetryFailedMessageAsync(string messageId);

        // ======== 监控和统计功能 ========

        /// <summary>
        /// 获取路由器统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        Task<MessageRouterStats> GetStatsAsync();

        /// <summary>
        /// 获取订阅者列表
        /// </summary>
        /// <returns>活跃订阅者列表</returns>
        Task<List<string>> GetActiveSubscribersAsync();

        /// <summary>
        /// 获取指定订阅者的详细信息
        /// </summary>
        /// <param name="subscriberId">订阅者ID</param>
        /// <returns>订阅者信息</returns>
        Task<SubscriberInfo?> GetSubscriberInfoAsync(string subscriberId);

        // ======== 系统管理功能 ========

        /// <summary>
        /// 清理过期消息
        /// </summary>
        /// <returns>清理的消息数量</returns>
        Task<int> CleanupExpiredMessagesAsync();

        /// <summary>
        /// 设置路由器配置
        /// </summary>
        /// <param name="config">配置信息</param>
        /// <returns>操作是否成功</returns>
        Task<bool> SetConfigurationAsync(MessageRouterConfig config);

        /// <summary>
        /// 获取路由器配置
        /// </summary>
        /// <returns>当前配置</returns>
        Task<MessageRouterConfig> GetConfigurationAsync();

        /// <summary>
        /// 健康检查
        /// </summary>
        /// <returns>健康状态</returns>
        Task<MessageRouterHealthStatus> GetHealthStatusAsync();
    }

    // ======== 辅助数据类型 ========

    /// <summary>
    /// 消息路由器统计信息
    /// </summary>
    [MessagePack.MessagePackObject]
    public class MessageRouterStats
    {
        [MessagePack.Key(0)]
        public long TotalMessagesSent { get; set; }

        [MessagePack.Key(1)]
        public long TotalMessagesDelivered { get; set; }

        [MessagePack.Key(2)]
        public long TotalMessagesFailed { get; set; }

        [MessagePack.Key(3)]
        public long TotalMessagesRetried { get; set; }

        [MessagePack.Key(4)]
        public int ActiveSubscribers { get; set; }

        [MessagePack.Key(5)]
        public int PendingMessages { get; set; }

        [MessagePack.Key(6)]
        public double AverageDeliveryTime { get; set; }

        [MessagePack.Key(7)]
        public DateTime LastResetTime { get; set; }

        [MessagePack.Key(8)]
        public Dictionary<MessageType, long> MessageCountByType { get; set; } = new();

        [MessagePack.Key(9)]
        public Dictionary<MessagePriority, long> MessageCountByPriority { get; set; } = new();

        [MessagePack.Key(10)]
        public double MessageProcessingRate { get; set; }
    }

    /// <summary>
    /// 订阅者信息
    /// </summary>
    [MessagePack.MessagePackObject]
    public class SubscriberInfo
    {
        [MessagePack.Key(0)]
        public string SubscriberId { get; set; } = string.Empty;

        [MessagePack.Key(1)]
        public string SubscriptionId { get; set; } = string.Empty;

        [MessagePack.Key(2)]
        public MessageFilter Filter { get; set; } = new();

        [MessagePack.Key(3)]
        public DateTime SubscribedAt { get; set; }

        [MessagePack.Key(4)]
        public DateTime LastActivityAt { get; set; }

        [MessagePack.Key(5)]
        public bool IsActive { get; set; }

        [MessagePack.Key(6)]
        public bool DeliveryPaused { get; set; }

        [MessagePack.Key(7)]
        public int PendingMessageCount { get; set; }

        [MessagePack.Key(8)]
        public int FailedMessageCount { get; set; }

        [MessagePack.Key(9)]
        public long TotalMessagesReceived { get; set; }

        [MessagePack.Key(10)]
        public double AverageProcessingTime { get; set; }
    }

    /// <summary>
    /// 消息路由器配置
    /// </summary>
    [MessagePack.MessagePackObject]
    public class MessageRouterConfig
    {
        [MessagePack.Key(0)]
        public int MaxQueueSize { get; set; } = 10000;

        [MessagePack.Key(1)]
        public int MaxRetryAttempts { get; set; } = 3;

        [MessagePack.Key(2)]
        public int RetryDelayMs { get; set; } = 1000;

        [MessagePack.Key(3)]
        public int MessageTimeoutMs { get; set; } = 30000;

        [MessagePack.Key(4)]
        public int CleanupIntervalMs { get; set; } = 300000; // 5 minutes

        [MessagePack.Key(5)]
        public int MaxHistorySize { get; set; } = 1000;

        [MessagePack.Key(6)]
        public bool EnableMetrics { get; set; } = true;

        [MessagePack.Key(7)]
        public bool EnableMessagePersistence { get; set; } = false;

        [MessagePack.Key(8)]
        public Dictionary<MessagePriority, int> PriorityQueueSizes { get; set; } = new()
        {
            { MessagePriority.Critical, 1000 },
            { MessagePriority.High, 2000 },
            { MessagePriority.Normal, 5000 },
            { MessagePriority.Low, 2000 }
        };

        [MessagePack.Key(9)]
        public Dictionary<MessageType, int> TypeSpecificTimeouts { get; set; } = new();
    }

    /// <summary>
    /// 消息路由器健康状态
    /// </summary>
    [MessagePack.MessagePackObject]
    public class MessageRouterHealthStatus
    {
        [MessagePack.Key(0)]
        public bool IsHealthy { get; set; }

        [MessagePack.Key(1)]
        public string Status { get; set; } = string.Empty;

        [MessagePack.Key(2)]
        public DateTime CheckTime { get; set; }

        [MessagePack.Key(3)]
        public List<string> Issues { get; set; } = new();

        [MessagePack.Key(4)]
        public Dictionary<string, object> Metrics { get; set; } = new();

        [MessagePack.Key(5)]
        public double CpuUsage { get; set; }

        [MessagePack.Key(6)]
        public long MemoryUsage { get; set; }

        [MessagePack.Key(7)]
        public int ActiveConnections { get; set; }

        [MessagePack.Key(8)]
        public double MessageProcessingRate { get; set; }
    }
}
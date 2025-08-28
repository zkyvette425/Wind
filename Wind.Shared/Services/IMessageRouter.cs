using Wind.Shared.Protocols;

namespace Wind.Shared.Services;

/// <summary>
/// 消息路由服务接口 - v1.3网络通信层
/// 提供智能消息路由和广播功能
/// </summary>
public interface IMessageRouter
{
    /// <summary>
    /// 路由消息到指定目标
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="message">路由消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>路由结果统计</returns>
    Task<RouteResult> RouteMessageAsync<T>(RoutedMessage<T> message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量路由消息
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="messages">消息列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>批量路由结果</returns>
    Task<BatchRouteResult> RouteBatchMessagesAsync<T>(IEnumerable<RoutedMessage<T>> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 注册消息接收器
    /// </summary>
    /// <param name="receiverId">接收器ID</param>
    /// <param name="receiver">接收器实例</param>
    /// <param name="metadata">接收器元数据 (房间ID、角色类型等)</param>
    Task RegisterReceiverAsync(string receiverId, IMessageReceiver receiver, Dictionary<string, string>? metadata = null);

    /// <summary>
    /// 注销消息接收器
    /// </summary>
    /// <param name="receiverId">接收器ID</param>
    Task UnregisterReceiverAsync(string receiverId);

    /// <summary>
    /// 获取活跃接收器数量
    /// </summary>
    /// <param name="filterMetadata">过滤条件</param>
    /// <returns>符合条件的接收器数量</returns>
    Task<int> GetActiveReceiversCountAsync(Dictionary<string, string>? filterMetadata = null);

    /// <summary>
    /// 获取路由统计信息
    /// </summary>
    /// <returns>路由统计</returns>
    Task<RouterStatistics> GetStatisticsAsync();

    /// <summary>
    /// 清理过期消息和统计信息
    /// </summary>
    /// <param name="maxAge">最大保留时间</param>
    Task CleanupExpiredDataAsync(TimeSpan maxAge);
}

/// <summary>
/// 消息接收器接口
/// </summary>
public interface IMessageReceiver
{
    /// <summary>
    /// 接收消息
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="message">消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果</returns>
    Task<MessageProcessResult> ReceiveMessageAsync<T>(RoutedMessage<T> message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 接收器是否在线
    /// </summary>
    bool IsOnline { get; }

    /// <summary>
    /// 接收器元数据
    /// </summary>
    Dictionary<string, string> Metadata { get; }
}

/// <summary>
/// 路由结果
/// </summary>
public class RouteResult
{
    /// <summary>消息ID</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>路由成功</summary>
    public bool Success { get; set; }

    /// <summary>成功投递数量</summary>
    public int DeliveredCount { get; set; }

    /// <summary>失败投递数量</summary>
    public int FailedCount { get; set; }

    /// <summary>总耗时</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>错误信息</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>确认回执 (如果需要)</summary>
    public List<MessageAckResponse> Acknowledgments { get; set; } = new();
}

/// <summary>
/// 批量路由结果
/// </summary>
public class BatchRouteResult
{
    /// <summary>总消息数</summary>
    public int TotalMessages { get; set; }

    /// <summary>成功路由数</summary>
    public int SuccessfulRoutes { get; set; }

    /// <summary>失败路由数</summary>
    public int FailedRoutes { get; set; }

    /// <summary>总耗时</summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>平均每条消息耗时</summary>
    public TimeSpan AverageMessageDuration { get; set; }

    /// <summary>各路由结果详情</summary>
    public List<RouteResult> Results { get; set; } = new();

    /// <summary>路由类型分组统计</summary>
    public Dictionary<RouteTargetType, RouteTypeStats> TypeStats { get; set; } = new();
}

/// <summary>
/// 路由类型统计
/// </summary>
public class RouteTypeStats
{
    /// <summary>该类型消息数量</summary>
    public int Count { get; set; }

    /// <summary>成功数量</summary>
    public int SuccessCount { get; set; }

    /// <summary>失败数量</summary>
    public int FailureCount { get; set; }

    /// <summary>平均耗时</summary>
    public TimeSpan AverageDuration { get; set; }

    /// <summary>成功率</summary>
    public double SuccessRate => Count > 0 ? (double)SuccessCount / Count : 0;
}

/// <summary>
/// 消息处理结果
/// </summary>
public class MessageProcessResult
{
    /// <summary>处理成功</summary>
    public bool Success { get; set; }

    /// <summary>处理耗时</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>需要确认回执</summary>
    public bool RequiresAck { get; set; }

    /// <summary>确认状态</summary>
    public AckStatus AckStatus { get; set; } = AckStatus.Processed;
}

/// <summary>
/// 路由器统计信息
/// </summary>
public class RouterStatistics
{
    /// <summary>活跃接收器数量</summary>
    public int ActiveReceivers { get; set; }

    /// <summary>总处理消息数</summary>
    public long TotalMessagesProcessed { get; set; }

    /// <summary>成功路由数</summary>
    public long SuccessfulRoutes { get; set; }

    /// <summary>失败路由数</summary>
    public long FailedRoutes { get; set; }

    /// <summary>平均路由延迟</summary>
    public TimeSpan AverageRouteLatency { get; set; }

    /// <summary>当前队列积压</summary>
    public int QueueBacklog { get; set; }

    /// <summary>路由类型分布</summary>
    public Dictionary<RouteTargetType, long> RouteTypeDistribution { get; set; } = new();

    /// <summary>统计更新时间</summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>成功率</summary>
    public double SuccessRate => TotalMessagesProcessed > 0 ? (double)SuccessfulRoutes / TotalMessagesProcessed : 0;
}
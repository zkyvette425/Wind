using MessagePack;
using System.ComponentModel.DataAnnotations;

namespace Wind.Shared.Protocols
{
    /// <summary>
    /// 消息类型枚举
    /// 定义系统中所有支持的消息类型
    /// </summary>
    public enum MessageType
    {
        // 系统消息 (0-99)
        System = 0,                 // 系统通知
        Heartbeat = 1,              // 心跳消息
        Error = 2,                  // 错误消息
        Status = 3,                 // 状态更新

        // 玩家消息 (100-199)
        PlayerChat = 100,           // 玩家聊天
        PlayerEmote = 101,          // 玩家表情
        PlayerPosition = 102,       // 玩家位置
        PlayerAction = 103,         // 玩家动作
        PlayerState = 104,          // 玩家状态变更

        // 房间消息 (200-299)
        RoomAnnouncement = 200,     // 房间公告
        RoomEvent = 201,            // 房间事件
        GameStart = 202,            // 游戏开始
        GameEnd = 203,              // 游戏结束
        RoomStateChange = 204,      // 房间状态变更

        // 游戏消息 (300-399)
        GameAction = 300,           // 游戏动作
        GameUpdate = 301,           // 游戏更新
        ScoreUpdate = 302,          // 分数更新
        GameEvent = 303,            // 游戏事件

        // 自定义消息 (400-999)
        Custom = 400                // 自定义消息类型起始
    }

    /// <summary>
    /// 消息优先级
    /// 用于消息队列排序和处理优先级控制
    /// </summary>
    public enum MessagePriority
    {
        Low = 0,        // 低优先级 - 聊天消息等
        Normal = 1,     // 普通优先级 - 一般游戏消息
        High = 2,       // 高优先级 - 重要状态更新
        Critical = 3    // 关键优先级 - 系统消息、错误消息
    }

    /// <summary>
    /// 消息传递模式
    /// 定义消息的传递方式和目标范围
    /// </summary>
    public enum MessageDeliveryMode
    {
        Unicast = 0,     // 单播 - 发送给特定玩家
        Multicast = 1,   // 组播 - 发送给特定玩家组
        Broadcast = 2,   // 广播 - 发送给房间内所有玩家
        GlobalBroadcast = 3  // 全局广播 - 发送给所有在线玩家
    }

    /// <summary>
    /// 消息投递保证级别
    /// 控制消息的可靠性和重试机制
    /// </summary>
    public enum MessageDeliveryGuarantee
    {
        AtMostOnce = 0,  // 最多一次 - 可能丢失，不重复
        AtLeastOnce = 1, // 至少一次 - 不丢失，可能重复
        ExactlyOnce = 2  // 恰好一次 - 不丢失，不重复
    }

    /// <summary>
    /// 统一消息协议基类
    /// 所有消息类型的基础结构
    /// </summary>
    [MessagePackObject]
    [MessagePack.Union(0, typeof(TextMessage))]
    [MessagePack.Union(1, typeof(PositionMessage))]
    [MessagePack.Union(2, typeof(ActionMessage))]
    [MessagePack.Union(3, typeof(EventMessage))]
    [MessagePack.Union(4, typeof(BinaryMessage))]
    [MessagePack.Union(5, typeof(StateMessage))]
    public abstract class BaseMessage
    {
        /// <summary>
        /// 消息唯一标识符
        /// </summary>
        [MessagePack.Key(0)]
        [Required]
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 消息类型
        /// </summary>
        [MessagePack.Key(1)]
        public MessageType Type { get; set; }

        /// <summary>
        /// 消息优先级
        /// </summary>
        [MessagePack.Key(2)]
        public MessagePriority Priority { get; set; } = MessagePriority.Normal;

        /// <summary>
        /// 传递模式
        /// </summary>
        [MessagePack.Key(3)]
        public MessageDeliveryMode DeliveryMode { get; set; } = MessageDeliveryMode.Unicast;

        /// <summary>
        /// 投递保证级别
        /// </summary>
        [MessagePack.Key(4)]
        public MessageDeliveryGuarantee DeliveryGuarantee { get; set; } = MessageDeliveryGuarantee.AtMostOnce;

        /// <summary>
        /// 发送者ID
        /// </summary>
        [MessagePack.Key(5)]
        [Required]
        public string SenderId { get; set; } = string.Empty;

        /// <summary>
        /// 目标接收者ID列表
        /// 为空表示根据DeliveryMode确定接收者
        /// </summary>
        [MessagePack.Key(6)]
        public List<string> TargetIds { get; set; } = new();

        /// <summary>
        /// 房间ID（如果是房间相关消息）
        /// </summary>
        [MessagePack.Key(7)]
        public string? RoomId { get; set; }

        /// <summary>
        /// 消息创建时间戳（UTC）
        /// </summary>
        [MessagePack.Key(8)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 消息过期时间（UTC）
        /// 超过此时间的消息将被丢弃
        /// </summary>
        [MessagePack.Key(9)]
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// 消息重试次数
        /// 用于可靠投递时的重试控制
        /// </summary>
        [MessagePack.Key(10)]
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        [MessagePack.Key(11)]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 消息元数据
        /// 存储额外的键值对信息
        /// </summary>
        [MessagePack.Key(12)]
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// 检查消息是否已过期
        /// </summary>
        [MessagePack.IgnoreMember]
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

        /// <summary>
        /// 检查是否可以重试
        /// </summary>
        [MessagePack.IgnoreMember]
        public bool CanRetry => RetryCount < MaxRetries;

        /// <summary>
        /// 增加重试次数
        /// </summary>
        public void IncrementRetry() => RetryCount++;
    }

    /// <summary>
    /// 文本消息协议
    /// 用于聊天、公告等文本内容
    /// </summary>
    [MessagePackObject]
    public class TextMessage : BaseMessage
    {
        /// <summary>
        /// 消息内容
        /// </summary>
        [MessagePack.Key(20)]
        [Required]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 消息格式类型（纯文本、Markdown、HTML等）
        /// </summary>
        [MessagePack.Key(21)]
        public string Format { get; set; } = "text/plain";

        /// <summary>
        /// 语言代码
        /// </summary>
        [MessagePack.Key(22)]
        public string? Language { get; set; }

        /// <summary>
        /// 是否为系统消息
        /// </summary>
        [MessagePack.Key(23)]
        public bool IsSystemMessage { get; set; } = false;
    }

    /// <summary>
    /// 位置消息协议
    /// 用于玩家位置同步
    /// </summary>
    [MessagePackObject]
    public class PositionMessage : BaseMessage
    {
        /// <summary>
        /// X坐标
        /// </summary>
        [MessagePack.Key(20)]
        public float X { get; set; }

        /// <summary>
        /// Y坐标
        /// </summary>
        [MessagePack.Key(21)]
        public float Y { get; set; }

        /// <summary>
        /// Z坐标
        /// </summary>
        [MessagePack.Key(22)]
        public float Z { get; set; }

        /// <summary>
        /// 旋转角度
        /// </summary>
        [MessagePack.Key(23)]
        public float Rotation { get; set; }

        /// <summary>
        /// 地图ID
        /// </summary>
        [MessagePack.Key(24)]
        public string? MapId { get; set; }

        /// <summary>
        /// 移动速度
        /// </summary>
        [MessagePack.Key(25)]
        public float Speed { get; set; }

        /// <summary>
        /// 移动方向（向量）
        /// </summary>
        [MessagePack.Key(26)]
        public Dictionary<string, float> Direction { get; set; } = new();
    }

    /// <summary>
    /// 动作消息协议
    /// 用于玩家动作、游戏行为等
    /// </summary>
    [MessagePackObject]
    public class ActionMessage : BaseMessage
    {
        /// <summary>
        /// 动作类型
        /// </summary>
        [MessagePack.Key(20)]
        [Required]
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// 动作参数
        /// </summary>
        [MessagePack.Key(21)]
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// 动作目标ID
        /// </summary>
        [MessagePack.Key(22)]
        public string? TargetId { get; set; }

        /// <summary>
        /// 动作持续时间（毫秒）
        /// </summary>
        [MessagePack.Key(23)]
        public int Duration { get; set; }

        /// <summary>
        /// 动作结果
        /// </summary>
        [MessagePack.Key(24)]
        public Dictionary<string, object> Result { get; set; } = new();
    }

    /// <summary>
    /// 事件消息协议
    /// 用于系统事件、游戏事件等
    /// </summary>
    [MessagePackObject]
    public class EventMessage : BaseMessage
    {
        /// <summary>
        /// 事件类型
        /// </summary>
        [MessagePack.Key(20)]
        [Required]
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// 事件数据
        /// </summary>
        [MessagePack.Key(21)]
        public Dictionary<string, object> EventData { get; set; } = new();

        /// <summary>
        /// 事件严重程度
        /// </summary>
        [MessagePack.Key(22)]
        public string Severity { get; set; } = "Info"; // Info, Warning, Error, Critical

        /// <summary>
        /// 事件源
        /// </summary>
        [MessagePack.Key(23)]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 是否需要确认
        /// </summary>
        [MessagePack.Key(24)]
        public bool RequiresAcknowledgment { get; set; } = false;
    }

    /// <summary>
    /// 二进制数据消息协议
    /// 用于文件传输、自定义数据等
    /// </summary>
    [MessagePackObject]
    public class BinaryMessage : BaseMessage
    {
        /// <summary>
        /// 数据内容
        /// </summary>
        [MessagePack.Key(20)]
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// 数据类型/MIME类型
        /// </summary>
        [MessagePack.Key(21)]
        public string ContentType { get; set; } = "application/octet-stream";

        /// <summary>
        /// 数据大小（字节）
        /// </summary>
        [MessagePack.Key(22)]
        public long Size { get; set; }

        /// <summary>
        /// 文件名（如果是文件）
        /// </summary>
        [MessagePack.Key(23)]
        public string? FileName { get; set; }

        /// <summary>
        /// 校验和（用于数据完整性验证）
        /// </summary>
        [MessagePack.Key(24)]
        public string? Checksum { get; set; }
    }

    /// <summary>
    /// 状态同步消息协议
    /// 用于玩家状态、游戏状态等同步
    /// </summary>
    [MessagePackObject]
    public class StateMessage : BaseMessage
    {
        /// <summary>
        /// 状态类型
        /// </summary>
        [MessagePack.Key(20)]
        [Required]
        public string StateType { get; set; } = string.Empty;

        /// <summary>
        /// 状态数据
        /// </summary>
        [MessagePack.Key(21)]
        public Dictionary<string, object> StateData { get; set; } = new();

        /// <summary>
        /// 状态版本号
        /// </summary>
        [MessagePack.Key(22)]
        public long Version { get; set; }

        /// <summary>
        /// 是否为增量更新
        /// </summary>
        [MessagePack.Key(23)]
        public bool IsIncremental { get; set; } = false;

        /// <summary>
        /// 上一个版本号（增量更新时使用）
        /// </summary>
        [MessagePack.Key(24)]
        public long? PreviousVersion { get; set; }
    }

    // ======== 消息请求和响应协议 ========

    /// <summary>
    /// 发送消息请求
    /// </summary>
    [MessagePackObject]
    public class SendMessageRequest
    {
        [MessagePack.Key(0)]
        [Required]
        public BaseMessage Message { get; set; } = null!;

        [MessagePack.Key(1)]
        public bool WaitForDelivery { get; set; } = false;

        [MessagePack.Key(2)]
        public int TimeoutMs { get; set; } = 5000;
    }

    /// <summary>
    /// 发送消息响应
    /// </summary>
    [MessagePackObject]
    public class SendMessageResponse
    {
        [MessagePack.Key(0)]
        public bool Success { get; set; }

        [MessagePack.Key(1)]
        public string Message { get; set; } = string.Empty;

        [MessagePack.Key(2)]
        public string MessageId { get; set; } = string.Empty;

        [MessagePack.Key(3)]
        public DateTime SentAt { get; set; }

        [MessagePack.Key(4)]
        public List<string> DeliveredTo { get; set; } = new();

        [MessagePack.Key(5)]
        public List<string> FailedDeliveries { get; set; } = new();
    }

    /// <summary>
    /// 批量发送消息请求
    /// </summary>
    [MessagePackObject]
    public class BatchSendMessageRequest
    {
        [MessagePack.Key(0)]
        [Required]
        public List<BaseMessage> Messages { get; set; } = new();

        [MessagePack.Key(1)]
        public bool WaitForAll { get; set; } = false;

        [MessagePack.Key(2)]
        public int TimeoutMs { get; set; } = 10000;

        [MessagePack.Key(3)]
        public bool FailFast { get; set; } = true;
    }

    /// <summary>
    /// 批量发送消息响应
    /// </summary>
    [MessagePackObject]
    public class BatchSendMessageResponse
    {
        [MessagePack.Key(0)]
        public bool Success { get; set; }

        [MessagePack.Key(1)]
        public string Message { get; set; } = string.Empty;

        [MessagePack.Key(2)]
        public int TotalMessages { get; set; }

        [MessagePack.Key(3)]
        public int SuccessCount { get; set; }

        [MessagePack.Key(4)]
        public int FailureCount { get; set; }

        [MessagePack.Key(5)]
        public List<SendMessageResponse> Results { get; set; } = new();
    }

    /// <summary>
    /// 消息确认请求
    /// </summary>
    [MessagePackObject]
    public class MessageAcknowledgmentRequest
    {
        [MessagePack.Key(0)]
        [Required]
        public string MessageId { get; set; } = string.Empty;

        [MessagePack.Key(1)]
        [Required]
        public string ReceiverId { get; set; } = string.Empty;

        [MessagePack.Key(2)]
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        [MessagePack.Key(3)]
        public bool Processed { get; set; } = true;

        [MessagePack.Key(4)]
        public string? ProcessingResult { get; set; }
    }

    /// <summary>
    /// 消息确认响应
    /// </summary>
    [MessagePackObject]
    public class MessageAcknowledgmentResponse
    {
        [MessagePack.Key(0)]
        public bool Success { get; set; }

        [MessagePack.Key(1)]
        public string Message { get; set; } = string.Empty;

        [MessagePack.Key(2)]
        public DateTime AcknowledgedAt { get; set; }
    }

    /// <summary>
    /// 获取消息历史请求
    /// </summary>
    [MessagePackObject]
    public class GetMessageHistoryRequest
    {
        [MessagePack.Key(0)]
        public string? RoomId { get; set; }

        [MessagePack.Key(1)]
        public string? SenderId { get; set; }

        [MessagePack.Key(2)]
        public MessageType? MessageType { get; set; }

        [MessagePack.Key(3)]
        public DateTime? FromTime { get; set; }

        [MessagePack.Key(4)]
        public DateTime? ToTime { get; set; }

        [MessagePack.Key(5)]
        public int Limit { get; set; } = 50;

        [MessagePack.Key(6)]
        public int Offset { get; set; } = 0;

        [MessagePack.Key(7)]
        public bool IncludeMetadata { get; set; } = false;
    }

    /// <summary>
    /// 获取消息历史响应
    /// </summary>
    [MessagePackObject]
    public class GetMessageHistoryResponse
    {
        [MessagePack.Key(0)]
        public bool Success { get; set; }

        [MessagePack.Key(1)]
        public string Message { get; set; } = string.Empty;

        [MessagePack.Key(2)]
        public List<BaseMessage> Messages { get; set; } = new();

        [MessagePack.Key(3)]
        public int TotalCount { get; set; }

        [MessagePack.Key(4)]
        public bool HasMore { get; set; }
    }

    // ======== 消息过滤和订阅协议 ========

    /// <summary>
    /// 消息过滤器
    /// 定义消息接收的过滤条件
    /// </summary>
    [MessagePackObject]
    public class MessageFilter
    {
        [MessagePack.Key(0)]
        public List<MessageType> MessageTypes { get; set; } = new();

        [MessagePack.Key(1)]
        public List<string> SenderIds { get; set; } = new();

        [MessagePack.Key(2)]
        public List<string> RoomIds { get; set; } = new();

        [MessagePack.Key(3)]
        public MessagePriority? MinPriority { get; set; }

        [MessagePack.Key(4)]
        public Dictionary<string, string> MetadataFilters { get; set; } = new();

        [MessagePack.Key(5)]
        public bool IncludeSystemMessages { get; set; } = true;
    }

    /// <summary>
    /// 订阅消息请求
    /// </summary>
    [MessagePackObject]
    public class SubscribeMessageRequest
    {
        [MessagePack.Key(0)]
        [Required]
        public string SubscriberId { get; set; } = string.Empty;

        [MessagePack.Key(1)]
        public MessageFilter Filter { get; set; } = new();

        [MessagePack.Key(2)]
        public bool ReceiveHistoricalMessages { get; set; } = false;

        [MessagePack.Key(3)]
        public int HistoricalMessageLimit { get; set; } = 100;
    }

    /// <summary>
    /// 订阅消息响应
    /// </summary>
    [MessagePackObject]
    public class SubscribeMessageResponse
    {
        [MessagePack.Key(0)]
        public bool Success { get; set; }

        [MessagePack.Key(1)]
        public string Message { get; set; } = string.Empty;

        [MessagePack.Key(2)]
        public string SubscriptionId { get; set; } = string.Empty;

        [MessagePack.Key(3)]
        public DateTime SubscribedAt { get; set; }
    }

    /// <summary>
    /// 取消订阅请求
    /// </summary>
    [MessagePackObject]
    public class UnsubscribeMessageRequest
    {
        [MessagePack.Key(0)]
        [Required]
        public string SubscriptionId { get; set; } = string.Empty;

        [MessagePack.Key(1)]
        [Required]
        public string SubscriberId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 取消订阅响应
    /// </summary>
    [MessagePackObject]
    public class UnsubscribeMessageResponse
    {
        [MessagePack.Key(0)]
        public bool Success { get; set; }

        [MessagePack.Key(1)]
        public string Message { get; set; } = string.Empty;

        [MessagePack.Key(2)]
        public DateTime UnsubscribedAt { get; set; }
    }
}
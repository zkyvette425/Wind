using MessagePack;

namespace Wind.Shared.Protocols;

/// <summary>
/// 压缩类型
/// </summary>
public enum CompressionType : byte
{
    None = 0,
    Gzip = 1,
    Lz4 = 2,
    Brotli = 3
}

/// <summary>
/// 消息标志位
/// </summary>
[Flags]
public enum MessageFlags : byte
{
    None = 0,
    Request = 1,
    Response = 2,
    RequiresAck = 4,
    Encrypted = 8,
    Compressed = 16,
    Broadcast = 32,
    System = 64,
    Reserved = 128
}

/// <summary>
/// 通知类型
/// </summary>
public enum NotificationType : byte
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Success = 3,
    System = 4
}

/// <summary>
/// 错误响应消息
/// </summary>
[MessagePackObject]
public class ErrorMessage
{
    [Key(0)]
    public string Code { get; set; } = string.Empty;

    [Key(1)]
    public string Message { get; set; } = string.Empty;

    [Key(2)]
    public string? Details { get; set; }

    [Key(3)]
    public Dictionary<string, object>? Context { get; set; }

    [Key(4)]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// 系统通知消息
/// </summary>
[MessagePackObject]
public class SystemNotificationMessage
{
    [Key(0)]
    public string Title { get; set; } = string.Empty;

    [Key(1)]
    public string Content { get; set; } = string.Empty;

    [Key(2)]
    public NotificationType Type { get; set; } = NotificationType.Info;

    [Key(3)]
    public long ExpireTime { get; set; }

    [Key(4)]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 通用响应消息
/// </summary>
[MessagePackObject]
public class ResponseMessage<T>
{
    [Key(0)]
    public bool Success { get; set; }

    [Key(1)]
    public string Message { get; set; } = string.Empty;

    [Key(2)]
    public T? Data { get; set; }

    [Key(3)]
    public string? ErrorCode { get; set; }

    [Key(4)]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// 分页响应消息
/// </summary>
[MessagePackObject]
public class PagedResponseMessage<T>
{
    [Key(0)]
    public List<T> Items { get; set; } = new();

    [Key(1)]
    public int TotalCount { get; set; }

    [Key(2)]
    public int PageIndex { get; set; }

    [Key(3)]
    public int PageSize { get; set; }

    [Key(4)]
    public bool HasNext { get; set; }

    [Key(5)]
    public bool HasPrevious { get; set; }
}

/// <summary>
/// 路由目标类型
/// </summary>
public enum RouteTargetType : byte
{
    /// <summary>单播 - 发送给特定用户</summary>
    Unicast = 0,
    /// <summary>多播 - 发送给特定用户组</summary>
    Multicast = 1,
    /// <summary>广播 - 发送给所有连接的用户</summary>
    Broadcast = 2,
    /// <summary>房间广播 - 发送给房间内所有用户</summary>
    RoomBroadcast = 3,
    /// <summary>区域广播 - 发送给特定区域内用户</summary>
    AreaBroadcast = 4,
    /// <summary>角色类型广播 - 发送给特定角色类型用户</summary>
    RoleTypeBroadcast = 5
}

/// <summary>
/// 消息路由信息
/// </summary>
[MessagePackObject]
public class MessageRoute
{
    /// <summary>路由目标类型</summary>
    [Key(0)]
    public RouteTargetType TargetType { get; set; }

    /// <summary>目标标识符列表 (用户ID、房间ID、区域ID等)</summary>
    [Key(1)]
    public List<string> TargetIds { get; set; } = new();

    /// <summary>排除的目标列表</summary>
    [Key(2)]
    public List<string> ExcludeIds { get; set; } = new();

    /// <summary>路由条件 (JSON格式的附加筛选条件)</summary>
    [Key(3)]
    public string? Conditions { get; set; }

    /// <summary>消息优先级 (0-255, 数值越大优先级越高)</summary>
    [Key(4)]
    public byte Priority { get; set; } = 128;

    /// <summary>消息过期时间 (UTC时间戳)</summary>
    [Key(5)]
    public long? ExpireTime { get; set; }

    /// <summary>是否要求确认回执</summary>
    [Key(6)]
    public bool RequireAck { get; set; }

    /// <summary>路由跳数限制 (防止循环路由)</summary>
    [Key(7)]
    public byte MaxHops { get; set; } = 10;

    /// <summary>当前跳数</summary>
    [Key(8)]
    public byte CurrentHops { get; set; }
}

/// <summary>
/// 路由消息包装器
/// </summary>
[MessagePackObject]
public class RoutedMessage<T>
{
    /// <summary>消息唯一标识</summary>
    [Key(0)]
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>消息类型名称</summary>
    [Key(1)]
    public string MessageType { get; set; } = typeof(T).Name;

    /// <summary>路由信息</summary>
    [Key(2)]
    public MessageRoute Route { get; set; } = new();

    /// <summary>实际消息内容</summary>
    [Key(3)]
    public T? Payload { get; set; }

    /// <summary>发送时间</summary>
    [Key(4)]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>发送者标识</summary>
    [Key(5)]
    public string? SenderId { get; set; }

    /// <summary>消息标签 (用于分类和过滤)</summary>
    [Key(6)]
    public Dictionary<string, string>? Tags { get; set; }

    /// <summary>消息是否已过期</summary>
    [IgnoreMember]
    public bool IsExpired => Route.ExpireTime.HasValue && 
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > Route.ExpireTime.Value;
}

/// <summary>
/// 消息确认回执
/// </summary>
[MessagePackObject]
public class MessageAckResponse
{
    /// <summary>原消息ID</summary>
    [Key(0)]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>接收者ID</summary>
    [Key(1)]
    public string ReceiverId { get; set; } = string.Empty;

    /// <summary>确认状态</summary>
    [Key(2)]
    public AckStatus Status { get; set; }

    /// <summary>处理时间</summary>
    [Key(3)]
    public long ProcessedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>错误信息 (如果处理失败)</summary>
    [Key(4)]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 确认状态枚举
/// </summary>
public enum AckStatus : byte
{
    /// <summary>已接收</summary>
    Received = 0,
    /// <summary>处理中</summary>
    Processing = 1,
    /// <summary>已处理</summary>
    Processed = 2,
    /// <summary>处理失败</summary>
    Failed = 3,
    /// <summary>已拒绝</summary>
    Rejected = 4
}
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
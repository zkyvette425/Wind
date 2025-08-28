using System.IO.Compression;
using MessagePack;

namespace Wind.Shared.Protocols;

/// <summary>
/// 消息扩展方法 - v1.3网络通信层
/// 提供消息压缩、序列化等工具方法
/// </summary>
public static class MessageExtensions
{
    /// <summary>
    /// 压缩阈值 - 小于此大小的消息不进行压缩
    /// </summary>
    public const int CompressionThreshold = 1024; // 1KB

    /// <summary>
    /// 最小压缩比阈值 - 低于此比例才使用压缩结果
    /// </summary>
    public const double MinCompressionRatio = 0.8; // 压缩后需小于原始大小的80%

    /// <summary>
    /// 序列化消息
    /// </summary>
    public static byte[] SerializeMessage<T>(this T message) where T : class
    {
        return MessagePackSerializer.Serialize(message, MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData)
            .WithCompression(MessagePackCompression.Lz4BlockArray));
    }

    /// <summary>
    /// 反序列化消息
    /// </summary>
    public static T? DeserializeMessage<T>(byte[] data) where T : class
    {
        try
        {
            return MessagePackSerializer.Deserialize<T>(data, MessagePackSerializerOptions.Standard
                .WithSecurity(MessagePackSecurity.UntrustedData)
                .WithCompression(MessagePackCompression.Lz4BlockArray));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 压缩数据 (使用Gzip)
    /// </summary>
    public static byte[] CompressData(byte[] data)
    {
        if (data.Length < CompressionThreshold)
        {
            return data; // 小数据不压缩
        }

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(data, 0, data.Length);
        }
        
        var compressed = output.ToArray();
        
        // 检查压缩效果
        if ((double)compressed.Length / data.Length > MinCompressionRatio)
        {
            return data; // 压缩效果不好，使用原始数据
        }
        
        return compressed;
    }

    /// <summary>
    /// 解压缩数据 (使用Gzip)
    /// </summary>
    public static byte[] DecompressData(byte[] compressedData)
    {
        try
        {
            using var input = new MemoryStream(compressedData);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            // 解压失败，返回原始数据（可能未压缩）
            return compressedData;
        }
    }

    /// <summary>
    /// 为BaseMessage添加压缩标志
    /// </summary>
    public static void SetCompressed(this BaseMessage message)
    {
        if (message.Metadata.ContainsKey("Compressed"))
        {
            message.Metadata["Compressed"] = "true";
        }
        else
        {
            message.Metadata.Add("Compressed", "true");
        }
    }

    /// <summary>
    /// 检查BaseMessage是否已压缩
    /// </summary>
    public static bool IsCompressed(this BaseMessage message)
    {
        return message.Metadata.TryGetValue("Compressed", out var value) && value == "true";
    }

    /// <summary>
    /// 创建响应消息
    /// </summary>
    public static ResponseMessage<T> CreateResponse<T>(bool success, string message, T? data = default)
    {
        return new ResponseMessage<T>
        {
            Success = success,
            Message = message,
            Data = data,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static ResponseMessage<T> CreateSuccess<T>(T data, string message = "Success")
    {
        return CreateResponse(true, message, data);
    }

    /// <summary>
    /// 创建错误响应
    /// </summary>
    public static ResponseMessage<T> CreateError<T>(string message, string? errorCode = null)
    {
        return new ResponseMessage<T>
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>
    /// 创建分页响应
    /// </summary>
    public static PagedResponseMessage<T> CreatePagedResponse<T>(
        List<T> items, 
        int totalCount, 
        int pageIndex, 
        int pageSize)
    {
        return new PagedResponseMessage<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
            HasNext = (pageIndex + 1) * pageSize < totalCount,
            HasPrevious = pageIndex > 0
        };
    }
}

/// <summary>
/// 消息统计信息
/// </summary>
public class MessageStats
{
    public long TotalMessages { get; set; }
    public long CompressedMessages { get; set; }
    public long TotalOriginalBytes { get; set; }
    public long TotalCompressedBytes { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }

    public double CompressionRatio => TotalOriginalBytes > 0 
        ? (double)TotalCompressedBytes / TotalOriginalBytes 
        : 1.0;

    public double CompressionPercentage => TotalMessages > 0 
        ? (double)CompressedMessages / TotalMessages * 100 
        : 0.0;

    public double AverageProcessingTime => TotalMessages > 0 
        ? TotalProcessingTime.TotalMilliseconds / TotalMessages 
        : 0.0;
}
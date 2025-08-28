using System.IO.Compression;
using System.Diagnostics;
using System.Collections.Concurrent;
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
    /// 中等消息阈值 - 超过此大小考虑使用更强压缩算法
    /// </summary>
    public const int MediumMessageThreshold = 8 * 1024; // 8KB

    /// <summary>
    /// 大消息阈值 - 超过此大小使用最强压缩算法
    /// </summary>
    public const int LargeMessageThreshold = 64 * 1024; // 64KB

    /// <summary>
    /// CPU开销阈值 - 压缩时间不超过此比例
    /// </summary>
    public const double MaxCpuOverheadRatio = 0.05; // 5%

    /// <summary>
    /// 压缩性能缓存 - 存储不同算法在不同数据类型上的性能表现
    /// </summary>
    private static readonly ConcurrentDictionary<string, CompressionPerformanceProfile> _performanceCache = new();

    #region 消息路由机制

    /// <summary>
    /// 路由统计信息
    /// </summary>
    public class RouteStatistics
    {
        public int TotalMessages { get; set; }
        public int UnicastMessages { get; set; }
        public int MulticastMessages { get; set; }
        public int BroadcastMessages { get; set; }
        public int ExpiredMessages { get; set; }
        public int FailedDeliveries { get; set; }
        public double AverageDeliveryTime { get; set; }
        public long LastUpdated { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// 路由性能缓存
    /// </summary>
    private static readonly ConcurrentDictionary<string, RouteStatistics> _routeStats = new();

    /// <summary>
    /// 消息路由队列 - 按优先级排序
    /// </summary>
    private static readonly ConcurrentDictionary<byte, ConcurrentQueue<object>> _priorityQueues = new();

    /// <summary>
    /// 创建路由消息
    /// </summary>
    public static RoutedMessage<T> CreateRoutedMessage<T>(
        this T payload,
        RouteTargetType targetType,
        IEnumerable<string>? targetIds = null,
        string? senderId = null,
        byte priority = 128,
        bool requireAck = false,
        long? expireTimeMs = null)
    {
        var route = new MessageRoute
        {
            TargetType = targetType,
            TargetIds = targetIds?.ToList() ?? new List<string>(),
            Priority = priority,
            RequireAck = requireAck,
            ExpireTime = expireTimeMs
        };

        return new RoutedMessage<T>
        {
            Payload = payload,
            Route = route,
            SenderId = senderId
        };
    }

    /// <summary>
    /// 创建单播消息
    /// </summary>
    public static RoutedMessage<T> CreateUnicastMessage<T>(
        this T payload,
        string targetUserId,
        string? senderId = null,
        byte priority = 128,
        bool requireAck = false)
    {
        return payload.CreateRoutedMessage(
            RouteTargetType.Unicast,
            new[] { targetUserId },
            senderId,
            priority,
            requireAck);
    }

    /// <summary>
    /// 创建房间广播消息
    /// </summary>
    public static RoutedMessage<T> CreateRoomBroadcastMessage<T>(
        this T payload,
        string roomId,
        string? senderId = null,
        IEnumerable<string>? excludeUsers = null,
        byte priority = 128)
    {
        var message = payload.CreateRoutedMessage(
            RouteTargetType.RoomBroadcast,
            new[] { roomId },
            senderId,
            priority);

        if (excludeUsers != null)
        {
            message.Route.ExcludeIds = excludeUsers.ToList();
        }

        return message;
    }

    /// <summary>
    /// 创建全局广播消息
    /// </summary>
    public static RoutedMessage<T> CreateGlobalBroadcastMessage<T>(
        this T payload,
        string? senderId = null,
        IEnumerable<string>? excludeUsers = null,
        byte priority = 64) // 广播消息默认较低优先级
    {
        var message = payload.CreateRoutedMessage(
            RouteTargetType.Broadcast,
            null,
            senderId,
            priority);

        if (excludeUsers != null)
        {
            message.Route.ExcludeIds = excludeUsers.ToList();
        }

        return message;
    }

    /// <summary>
    /// 验证路由消息有效性
    /// </summary>
    public static bool IsValidRouteMessage<T>(this RoutedMessage<T> message)
    {
        if (message == null || message.Payload == null)
            return false;

        // 检查消息是否过期
        if (message.IsExpired)
            return false;

        // 检查跳数限制
        if (message.Route.CurrentHops >= message.Route.MaxHops)
            return false;

        // 根据路由类型验证必要参数
        switch (message.Route.TargetType)
        {
            case RouteTargetType.Unicast:
            case RouteTargetType.RoomBroadcast:
            case RouteTargetType.AreaBroadcast:
            case RouteTargetType.RoleTypeBroadcast:
                return message.Route.TargetIds.Count > 0;

            case RouteTargetType.Multicast:
                return message.Route.TargetIds.Count > 1;

            case RouteTargetType.Broadcast:
                return true; // 广播不需要特定目标

            default:
                return false;
        }
    }

    /// <summary>
    /// 智能路由选择 - 基于目标数量和消息特征自动选择最优路由策略
    /// </summary>
    public static RouteTargetType SelectOptimalRouteType(
        int targetCount,
        int totalConnections,
        bool isUrgent = false,
        bool requiresReliability = false)
    {
        // 单个目标，使用单播
        if (targetCount == 1)
            return RouteTargetType.Unicast;

        // 计算广播阈值 - 目标数量超过连接数的一定比例时使用广播
        double broadcastThreshold = requiresReliability ? 0.8 : 0.6; // 可靠性要求高时提高广播阈值
        
        if (isUrgent)
            broadcastThreshold *= 0.7; // 紧急消息降低广播阈值

        if (targetCount >= totalConnections * broadcastThreshold)
            return RouteTargetType.Broadcast;

        // 中等规模目标，使用多播
        if (targetCount > 10)
            return RouteTargetType.Multicast;

        // 默认使用多播
        return RouteTargetType.Multicast;
    }

    /// <summary>
    /// 消息路由排序 - 按优先级和时间戳排序
    /// </summary>
    public static IEnumerable<RoutedMessage<T>> SortMessagesByPriority<T>(
        this IEnumerable<RoutedMessage<T>> messages)
    {
        return messages
            .Where(m => m.IsValidRouteMessage())
            .OrderByDescending(m => m.Route.Priority) // 优先级降序
            .ThenBy(m => m.Timestamp); // 时间戳升序 (先来先服务)
    }

    /// <summary>
    /// 批量路由消息 - 将多个消息按路由类型分组优化传输
    /// </summary>
    public static Dictionary<RouteTargetType, List<RoutedMessage<T>>> GroupMessagesByRoute<T>(
        this IEnumerable<RoutedMessage<T>> messages)
    {
        return messages
            .Where(m => m.IsValidRouteMessage())
            .GroupBy(m => m.Route.TargetType)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// 更新路由统计
    /// </summary>
    public static void UpdateRouteStatistics<T>(this RoutedMessage<T> message, bool deliverySuccess, TimeSpan deliveryTime)
    {
        var key = $"{message.Route.TargetType}_{message.MessageType}";
        var stats = _routeStats.GetOrAdd(key, _ => new RouteStatistics());

        lock (stats)
        {
            stats.TotalMessages++;

            switch (message.Route.TargetType)
            {
                case RouteTargetType.Unicast:
                    stats.UnicastMessages++;
                    break;
                case RouteTargetType.Multicast:
                    stats.MulticastMessages++;
                    break;
                case RouteTargetType.Broadcast:
                case RouteTargetType.RoomBroadcast:
                case RouteTargetType.AreaBroadcast:
                case RouteTargetType.RoleTypeBroadcast:
                    stats.BroadcastMessages++;
                    break;
            }

            if (!deliverySuccess)
            {
                stats.FailedDeliveries++;
            }

            // 更新平均传输时间 (指数移动平均)
            double alpha = 0.1; // 平滑因子
            stats.AverageDeliveryTime = stats.AverageDeliveryTime * (1 - alpha) + 
                                       deliveryTime.TotalMilliseconds * alpha;

            stats.LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// 获取路由统计信息
    /// </summary>
    public static RouteStatistics? GetRouteStatistics(RouteTargetType routeType, string? messageType = null)
    {
        var key = messageType != null ? $"{routeType}_{messageType}" : routeType.ToString();
        return _routeStats.TryGetValue(key, out var stats) ? stats : null;
    }

    /// <summary>
    /// 清理过期的路由统计
    /// </summary>
    public static void CleanupExpiredRouteStats(TimeSpan maxAge)
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(maxAge).ToUnixTimeMilliseconds();
        var keysToRemove = _routeStats
            .Where(kvp => kvp.Value.LastUpdated < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _routeStats.TryRemove(key, out _);
        }
    }

    #endregion

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
    /// 智能压缩数据 - 根据数据大小和特性选择最优算法
    /// </summary>
    public static (byte[] data, CompressionType type, CompressionStats stats) CompressDataIntelligent(byte[] data, string? dataTypeHint = null)
    {
        var stats = new CompressionStats
        {
            OriginalSize = data.Length,
            StartTime = DateTimeOffset.UtcNow
        };

        if (data.Length < CompressionThreshold)
        {
            stats.Algorithm = "None";
            stats.EndTime = DateTimeOffset.UtcNow;
            return (data, CompressionType.None, stats); // 小数据不压缩
        }

        // 根据数据大小选择压缩策略
        var strategy = SelectCompressionStrategy(data.Length, dataTypeHint);
        var results = new List<(byte[] compressed, CompressionType type, TimeSpan duration, double ratio)>();

        // 测试选定的压缩算法
        foreach (var algorithmType in strategy.AlgorithmsToTest)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var compressed = CompressWithAlgorithm(data, algorithmType);
                sw.Stop();

                var ratio = (double)compressed.Length / data.Length;
                results.Add((compressed, algorithmType, sw.Elapsed, ratio));
            }
            catch
            {
                sw.Stop();
                // 压缩失败，跳过此算法
            }
        }

        // 选择最优结果
        var bestResult = SelectBestCompression(results, data, strategy.MaxCpuTime);
        
        stats.Algorithm = bestResult.type.ToString();
        stats.CompressedSize = bestResult.compressed?.Length ?? data.Length;
        stats.CompressionTime = bestResult.duration;
        stats.EndTime = DateTimeOffset.UtcNow;

        // 更新性能缓存
        if (dataTypeHint != null)
        {
            UpdatePerformanceCache(dataTypeHint, bestResult.type, stats);
        }

        return (bestResult.compressed ?? data, bestResult.type, stats);
    }

    /// <summary>
    /// 压缩数据 (兼容性方法，使用智能压缩)
    /// </summary>
    public static byte[] CompressData(byte[] data)
    {
        var (compressed, _, _) = CompressDataIntelligent(data);
        return compressed;
    }

    /// <summary>
    /// 解压缩数据 (智能检测算法)
    /// </summary>
    public static byte[] DecompressData(byte[] compressedData, CompressionType type = CompressionType.None)
    {
        try
        {
            if (type == CompressionType.None)
            {
                // 尝试自动检测压缩类型（简单检测）
                type = DetectCompressionType(compressedData);
            }

            return type switch
            {
                CompressionType.Gzip => DecompressGzip(compressedData),
                CompressionType.Lz4 => DecompressLz4(compressedData),
                CompressionType.Brotli => DecompressBrotli(compressedData),
                _ => compressedData
            };
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

    #region 私有方法

    /// <summary>
    /// 选择压缩策略
    /// </summary>
    private static CompressionStrategy SelectCompressionStrategy(int dataSize, string? dataTypeHint)
    {
        var strategy = new CompressionStrategy();

        // 检查缓存的性能数据
        if (dataTypeHint != null && _performanceCache.TryGetValue(dataTypeHint, out var profile))
        {
            strategy.AlgorithmsToTest.Add(profile.RecommendedAlgorithm);
            strategy.MaxCpuTime = profile.AverageCompressionTime.Add(TimeSpan.FromMilliseconds(10));
            strategy.Priority = 1; // 基于历史数据的平衡策略
            return strategy;
        }

        // 根据数据大小选择策略
        if (dataSize < MediumMessageThreshold)
        {
            // 小到中等消息：速度优先
            strategy.AlgorithmsToTest.AddRange(new[] { CompressionType.Lz4, CompressionType.Gzip });
            strategy.MaxCpuTime = TimeSpan.FromMilliseconds(5);
            strategy.Priority = 0;
        }
        else if (dataSize < LargeMessageThreshold)
        {
            // 中等到大消息：平衡策略
            strategy.AlgorithmsToTest.AddRange(new[] { CompressionType.Gzip, CompressionType.Brotli, CompressionType.Lz4 });
            strategy.MaxCpuTime = TimeSpan.FromMilliseconds(20);
            strategy.Priority = 1;
        }
        else
        {
            // 大消息：压缩率优先
            strategy.AlgorithmsToTest.AddRange(new[] { CompressionType.Brotli, CompressionType.Gzip, CompressionType.Lz4 });
            strategy.MaxCpuTime = TimeSpan.FromMilliseconds(50);
            strategy.Priority = 2;
        }

        return strategy;
    }

    /// <summary>
    /// 使用指定算法压缩数据
    /// </summary>
    private static byte[] CompressWithAlgorithm(byte[] data, CompressionType algorithm)
    {
        return algorithm switch
        {
            CompressionType.Gzip => CompressGzip(data),
            CompressionType.Lz4 => CompressLz4(data),
            CompressionType.Brotli => CompressBrotli(data),
            _ => data
        };
    }

    /// <summary>
    /// 选择最佳压缩结果
    /// </summary>
    private static (byte[]? compressed, CompressionType type, TimeSpan duration, double ratio) SelectBestCompression(
        List<(byte[] compressed, CompressionType type, TimeSpan duration, double ratio)> results,
        byte[] originalData,
        TimeSpan maxCpuTime)
    {
        if (results.Count == 0)
        {
            return (originalData, CompressionType.None, TimeSpan.Zero, 1.0);
        }

        // 过滤出压缩效果好且CPU时间可接受的结果
        var validResults = results
            .Where(r => r.ratio < MinCompressionRatio && r.duration <= maxCpuTime)
            .ToList();

        if (validResults.Count == 0)
        {
            return (originalData, CompressionType.None, TimeSpan.Zero, 1.0);
        }

        // 选择最优结果：综合考虑压缩比和CPU时间
        var bestResult = validResults
            .OrderBy(r => r.ratio * 0.7 + (r.duration.TotalMilliseconds / maxCpuTime.TotalMilliseconds) * 0.3)
            .First();

        return (bestResult.compressed, bestResult.type, bestResult.duration, bestResult.ratio);
    }

    /// <summary>
    /// 更新性能缓存
    /// </summary>
    private static void UpdatePerformanceCache(string dataType, CompressionType algorithm, CompressionStats stats)
    {
        var key = $"{dataType}";
        _performanceCache.AddOrUpdate(key, 
            new CompressionPerformanceProfile
            {
                RecommendedAlgorithm = algorithm,
                AverageCompressionRatio = stats.CompressionRatio,
                AverageCompressionTime = stats.CompressionTime,
                SampleCount = 1,
                LastUpdated = DateTimeOffset.UtcNow
            },
            (k, existing) => new CompressionPerformanceProfile
            {
                RecommendedAlgorithm = stats.CompressionRatio < existing.AverageCompressionRatio ? algorithm : existing.RecommendedAlgorithm,
                AverageCompressionRatio = (existing.AverageCompressionRatio * existing.SampleCount + stats.CompressionRatio) / (existing.SampleCount + 1),
                AverageCompressionTime = TimeSpan.FromTicks((existing.AverageCompressionTime.Ticks * existing.SampleCount + stats.CompressionTime.Ticks) / (existing.SampleCount + 1)),
                SampleCount = existing.SampleCount + 1,
                LastUpdated = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// 检测压缩类型（基于文件头）
    /// </summary>
    private static CompressionType DetectCompressionType(byte[] data)
    {
        if (data.Length < 4) return CompressionType.None;

        // Gzip magic number: 1f 8b
        if (data[0] == 0x1f && data[1] == 0x8b) return CompressionType.Gzip;
        
        // Brotli不容易检测，跳过
        // LZ4的MessagePack包装也不容易检测，跳过

        return CompressionType.None;
    }

    /// <summary>
    /// 解压缩 - Gzip
    /// </summary>
    private static byte[] DecompressGzip(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// 解压缩 - LZ4
    /// </summary>
    private static byte[] DecompressLz4(byte[] compressedData)
    {
        using var stream = new MemoryStream(compressedData);
        return MessagePackSerializer.Deserialize<byte[]>(stream, MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4BlockArray));
    }

    /// <summary>
    /// 解压缩 - Brotli
    /// </summary>
    private static byte[] DecompressBrotli(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// 压缩算法实现 - Gzip
    /// </summary>
    private static byte[] CompressGzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    /// <summary>
    /// 压缩算法实现 - LZ4 (通过MessagePack)
    /// </summary>
    private static byte[] CompressLz4(byte[] data)
    {
        using var stream = new MemoryStream();
        MessagePackSerializer.Serialize(stream, data, MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4BlockArray));
        return stream.ToArray();
    }

    /// <summary>
    /// 压缩算法实现 - Brotli
    /// </summary>
    private static byte[] CompressBrotli(byte[] data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal))
        {
            brotli.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    #endregion
}

/// <summary>
/// 压缩策略
/// </summary>
public class CompressionStrategy
{
    public List<CompressionType> AlgorithmsToTest { get; set; } = new();
    public TimeSpan MaxCpuTime { get; set; }
    public int Priority { get; set; } // 0=速度优先, 1=平衡, 2=压缩率优先
}

/// <summary>
/// 压缩性能配置文件
/// </summary>
public class CompressionPerformanceProfile
{
    public CompressionType RecommendedAlgorithm { get; set; }
    public double AverageCompressionRatio { get; set; }
    public TimeSpan AverageCompressionTime { get; set; }
    public int SampleCount { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}

/// <summary>
/// 压缩统计信息
/// </summary>
public class CompressionStats
{
    public int OriginalSize { get; set; }
    public int CompressedSize { get; set; }
    public string Algorithm { get; set; } = "None";
    public TimeSpan CompressionTime { get; set; }
    public double CompressionRatio => OriginalSize > 0 ? (double)CompressedSize / OriginalSize : 1.0;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public bool CpuOverheadAcceptable => CompressionTime.TotalMilliseconds / 1000.0 < MessageExtensions.MaxCpuOverheadRatio;
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
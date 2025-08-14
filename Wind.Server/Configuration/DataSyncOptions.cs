using System.ComponentModel.DataAnnotations;

namespace Wind.Server.Configuration;

/// <summary>
/// 数据同步配置选项
/// </summary>
public class DataSyncOptions
{
    public const string SectionName = "DataSync";

    /// <summary>
    /// Write-Behind批量刷新间隔（毫秒）
    /// </summary>
    [Range(1000, 300000)]
    public int FlushIntervalMs { get; set; } = 5000;

    /// <summary>
    /// 每次批量刷新的最大项目数
    /// </summary>
    [Range(10, 10000)]
    public int FlushBatchSize { get; set; } = 100;

    /// <summary>
    /// Write-Behind队列最大长度，超过时立即刷新
    /// </summary>
    [Range(100, 100000)]
    public int MaxPendingWrites { get; set; } = 1000;

    /// <summary>
    /// 默认缓存过期时间（秒）
    /// </summary>
    [Range(60, 86400)]
    public int DefaultCacheExpirySeconds { get; set; } = 3600;

    /// <summary>
    /// 是否启用统计信息收集
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// 数据类型到MongoDB集合的映射
    /// </summary>
    public Dictionary<string, string> TypeCollectionMapping { get; set; } = new()
    {
        { "PlayerState", "players" },
        { "RoomState", "rooms" },
        { "MessageInfo", "messages" },
        { "UserSession", "sessions" }
    };

    /// <summary>
    /// 所有MongoDB集合名称
    /// </summary>
    public string[] MongoCollections { get; set; } = 
    [
        "players",
        "rooms", 
        "messages",
        "sessions"
    ];

    /// <summary>
    /// 同步策略配置
    /// </summary>
    public SyncStrategyConfig SyncStrategy { get; set; } = new();

    /// <summary>
    /// 获取指定类型对应的MongoDB集合名称
    /// </summary>
    public string GetCollectionName<T>() where T : class
    {
        var typeName = typeof(T).Name;
        return TypeCollectionMapping.TryGetValue(typeName, out var collectionName) 
            ? collectionName 
            : typeName.ToLowerInvariant();
    }

    /// <summary>
    /// 验证配置选项
    /// </summary>
    public void Validate()
    {
        if (FlushIntervalMs < 1000 || FlushIntervalMs > 300000)
        {
            throw new ArgumentOutOfRangeException(nameof(FlushIntervalMs), 
                "刷新间隔必须在1秒到5分钟之间");
        }

        if (FlushBatchSize < 10 || FlushBatchSize > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(FlushBatchSize), 
                "批量大小必须在10到10000之间");
        }

        if (MaxPendingWrites < 100 || MaxPendingWrites > 100000)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPendingWrites), 
                "最大待处理写入数必须在100到100000之间");
        }

        if (DefaultCacheExpirySeconds < 60 || DefaultCacheExpirySeconds > 86400)
        {
            throw new ArgumentOutOfRangeException(nameof(DefaultCacheExpirySeconds), 
                "默认缓存过期时间必须在1分钟到1天之间");
        }

        if (MongoCollections.Length == 0)
        {
            throw new ArgumentException("必须配置至少一个MongoDB集合", nameof(MongoCollections));
        }

        SyncStrategy.Validate();
    }
}

/// <summary>
/// 同步策略配置
/// </summary>
public class SyncStrategyConfig
{
    /// <summary>
    /// 默认同步策略
    /// </summary>
    public SyncStrategyType DefaultStrategy { get; set; } = SyncStrategyType.WriteThrough;

    /// <summary>
    /// 针对特定数据类型的策略覆盖
    /// </summary>
    public Dictionary<string, SyncStrategyType> TypeStrategyOverrides { get; set; } = new()
    {
        { "PlayerState", SyncStrategyType.WriteThrough },
        { "RoomState", SyncStrategyType.WriteThrough },
        { "MessageInfo", SyncStrategyType.WriteBehind },
        { "UserSession", SyncStrategyType.CacheAside }
    };

    /// <summary>
    /// Write-Behind策略的特定配置
    /// </summary>
    public WriteBehindConfig WriteBehind { get; set; } = new();

    /// <summary>
    /// Cache-Aside策略的特定配置
    /// </summary>
    public CacheAsideConfig CacheAside { get; set; } = new();

    /// <summary>
    /// 获取指定类型的同步策略
    /// </summary>
    public SyncStrategyType GetStrategy<T>() where T : class
    {
        var typeName = typeof(T).Name;
        return TypeStrategyOverrides.TryGetValue(typeName, out var strategy) 
            ? strategy 
            : DefaultStrategy;
    }

    /// <summary>
    /// 验证同步策略配置
    /// </summary>
    public void Validate()
    {
        WriteBehind.Validate();
        CacheAside.Validate();
    }
}

/// <summary>
/// Write-Behind策略特定配置
/// </summary>
public class WriteBehindConfig
{
    /// <summary>
    /// 高优先级数据类型（更频繁刷新）
    /// </summary>
    public string[] HighPriorityTypes { get; set; } = ["PlayerState", "RoomState"];

    /// <summary>
    /// 高优先级数据刷新间隔（毫秒）
    /// </summary>
    [Range(500, 10000)]
    public int HighPriorityFlushIntervalMs { get; set; } = 2000;

    /// <summary>
    /// 最大数据年龄，超过此时间强制刷新（毫秒）
    /// </summary>
    [Range(5000, 600000)]
    public int MaxDataAgeMs { get; set; } = 30000;

    /// <summary>
    /// 验证Write-Behind配置
    /// </summary>
    public void Validate()
    {
        if (HighPriorityFlushIntervalMs < 500 || HighPriorityFlushIntervalMs > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(HighPriorityFlushIntervalMs), 
                "高优先级刷新间隔必须在0.5秒到10秒之间");
        }

        if (MaxDataAgeMs < 5000 || MaxDataAgeMs > 600000)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxDataAgeMs), 
                "最大数据年龄必须在5秒到10分钟之间");
        }
    }
}

/// <summary>
/// Cache-Aside策略特定配置
/// </summary>
public class CacheAsideConfig
{
    /// <summary>
    /// 预加载数据类型（启动时预先加载到缓存）
    /// </summary>
    public string[] PreloadTypes { get; set; } = ["UserSession"];

    /// <summary>
    /// 不同数据类型的缓存TTL配置
    /// </summary>
    public Dictionary<string, int> TypeCacheTtlSeconds { get; set; } = new()
    {
        { "PlayerState", 1800 },    // 30分钟
        { "RoomState", 900 },       // 15分钟
        { "MessageInfo", 3600 },    // 1小时
        { "UserSession", 7200 }     // 2小时
    };

    /// <summary>
    /// 获取指定类型的缓存TTL
    /// </summary>
    public TimeSpan GetCacheTtl<T>() where T : class
    {
        var typeName = typeof(T).Name;
        var seconds = TypeCacheTtlSeconds.TryGetValue(typeName, out var ttl) ? ttl : 3600;
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// 验证Cache-Aside配置
    /// </summary>
    public void Validate()
    {
        foreach (var ttl in TypeCacheTtlSeconds.Values)
        {
            if (ttl < 60 || ttl > 86400)
            {
                throw new ArgumentOutOfRangeException(nameof(TypeCacheTtlSeconds), 
                    "缓存TTL必须在1分钟到1天之间");
            }
        }
    }
}

/// <summary>
/// 同步策略类型
/// </summary>
public enum SyncStrategyType
{
    /// <summary>
    /// Write-Through：同步写入缓存和持久化存储
    /// </summary>
    WriteThrough = 0,

    /// <summary>
    /// Write-Behind：立即写缓存，异步批量持久化
    /// </summary>
    WriteBehind = 1,

    /// <summary>
    /// Cache-Aside：应用层控制缓存
    /// </summary>
    CacheAside = 2
}
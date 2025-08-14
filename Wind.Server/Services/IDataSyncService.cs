using Wind.Shared.Protocols;

namespace Wind.Server.Services;

/// <summary>
/// 数据同步服务接口
/// 定义Redis缓存与MongoDB持久化之间的数据同步策略
/// </summary>
public interface IDataSyncService
{
    /// <summary>
    /// Write-Through策略：同步写入缓存和持久化存储
    /// </summary>
    Task WriteThrough<T>(string key, T data, TimeSpan? expiry = null) where T : class;

    /// <summary>
    /// Write-Behind策略：立即写入缓存，异步批量持久化
    /// </summary>
    Task WriteBehind<T>(string key, T data, TimeSpan? expiry = null) where T : class;

    /// <summary>
    /// Cache-Aside策略：读取时检查缓存，未命中时从持久化存储加载
    /// </summary>
    Task<T?> CacheAside<T>(string key, Func<Task<T?>> loadFromPersistence) where T : class;

    /// <summary>
    /// 删除数据（同时从缓存和持久化存储删除）
    /// </summary>
    Task Delete(string key);

    /// <summary>
    /// 批量同步待处理的Write-Behind数据到持久化存储
    /// </summary>
    Task FlushPendingWrites();

    /// <summary>
    /// 获取同步统计信息
    /// </summary>
    Task<DataSyncStats> GetSyncStats();
}

/// <summary>
/// 数据同步统计信息
/// </summary>
public class DataSyncStats
{
    /// <summary>
    /// 缓存命中次数
    /// </summary>
    public long CacheHits { get; set; }

    /// <summary>
    /// 缓存未命中次数
    /// </summary>
    public long CacheMisses { get; set; }

    /// <summary>
    /// 缓存命中率
    /// </summary>
    public double HitRate => CacheHits + CacheMisses > 0 ? (double)CacheHits / (CacheHits + CacheMisses) : 0;

    /// <summary>
    /// Write-Through操作次数
    /// </summary>
    public long WriteThroughCount { get; set; }

    /// <summary>
    /// Write-Behind操作次数
    /// </summary>
    public long WriteBehindCount { get; set; }

    /// <summary>
    /// 待处理的Write-Behind操作数量
    /// </summary>
    public long PendingWriteBehindCount { get; set; }

    /// <summary>
    /// 上次批量同步时间
    /// </summary>
    public DateTime LastFlushTime { get; set; }

    /// <summary>
    /// 同步失败次数
    /// </summary>
    public long SyncFailureCount { get; set; }
}
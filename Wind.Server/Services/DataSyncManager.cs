using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Wind.Server.Configuration;
using Wind.Shared.Protocols;

namespace Wind.Server.Services;

/// <summary>
/// 数据同步管理器
/// 根据配置的策略自动选择合适的同步方式
/// </summary>
public class DataSyncManager : IDisposable
{
    private readonly IDataSyncService _syncService;
    private readonly MongoDbConnectionManager _mongoManager;
    private readonly DataSyncOptions _options;
    private readonly ILogger<DataSyncManager> _logger;
    private volatile bool _disposed = false;

    public DataSyncManager(
        IDataSyncService syncService,
        MongoDbConnectionManager mongoManager,
        IOptions<DataSyncOptions> options,
        ILogger<DataSyncManager> logger)
    {
        _syncService = syncService;
        _mongoManager = mongoManager;
        _options = options.Value;
        _logger = logger;

        _logger.LogInformation("数据同步管理器已初始化");
    }

    /// <summary>
    /// 保存数据（根据配置的策略自动选择同步方式）
    /// </summary>
    public async Task SaveAsync<T>(string key, T data, TimeSpan? expiry = null) where T : class
    {
        ThrowIfDisposed();
        
        var strategy = _options.SyncStrategy.GetStrategy<T>();
        
        switch (strategy)
        {
            case SyncStrategyType.WriteThrough:
                await _syncService.WriteThrough(key, data, expiry);
                _logger.LogDebug("使用Write-Through策略保存: {Key}, Type: {Type}", key, typeof(T).Name);
                break;
                
            case SyncStrategyType.WriteBehind:
                await _syncService.WriteBehind(key, data, expiry);
                _logger.LogDebug("使用Write-Behind策略保存: {Key}, Type: {Type}", key, typeof(T).Name);
                break;
                
            case SyncStrategyType.CacheAside:
                // Cache-Aside通常用于读取，写入时直接更新缓存
                await _syncService.WriteBehind(key, data, expiry);
                _logger.LogDebug("使用Cache-Aside策略保存: {Key}, Type: {Type}", key, typeof(T).Name);
                break;
                
            default:
                throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "未知的同步策略");
        }
    }

    /// <summary>
    /// 加载数据（根据配置的策略自动选择同步方式）
    /// </summary>
    public async Task<T?> LoadAsync<T>(string key) where T : class
    {
        ThrowIfDisposed();
        
        var strategy = _options.SyncStrategy.GetStrategy<T>();
        
        switch (strategy)
        {
            case SyncStrategyType.WriteThrough:
            case SyncStrategyType.WriteBehind:
                // 直接从缓存读取
                return await _syncService.CacheAside<T>(key, () => LoadFromMongo<T>(key));
                
            case SyncStrategyType.CacheAside:
                // 使用Cache-Aside策略
                return await _syncService.CacheAside<T>(key, () => LoadFromMongo<T>(key));
                
            default:
                throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "未知的同步策略");
        }
    }

    /// <summary>
    /// 删除数据
    /// </summary>
    public async Task DeleteAsync(string key)
    {
        ThrowIfDisposed();
        await _syncService.Delete(key);
        _logger.LogDebug("删除数据: {Key}", key);
    }

    /// <summary>
    /// 强制刷新所有待处理的写入操作
    /// </summary>
    public async Task FlushAsync()
    {
        ThrowIfDisposed();
        await _syncService.FlushPendingWrites();
        _logger.LogDebug("强制刷新所有待处理写入");
    }

    /// <summary>
    /// 获取同步统计信息
    /// </summary>
    public async Task<DataSyncStats> GetStatsAsync()
    {
        ThrowIfDisposed();
        return await _syncService.GetSyncStats();
    }

    /// <summary>
    /// 批量保存数据
    /// </summary>
    public async Task SaveBatchAsync<T>(IDictionary<string, T> items, TimeSpan? expiry = null) where T : class
    {
        ThrowIfDisposed();
        
        if (!items.Any())
        {
            return;
        }

        var tasks = items.Select(kvp => SaveAsync(kvp.Key, kvp.Value, expiry));
        await Task.WhenAll(tasks);
        
        _logger.LogDebug("批量保存完成，数量: {Count}, Type: {Type}", items.Count, typeof(T).Name);
    }

    /// <summary>
    /// 批量加载数据
    /// </summary>
    public async Task<Dictionary<string, T?>> LoadBatchAsync<T>(IEnumerable<string> keys) where T : class
    {
        ThrowIfDisposed();
        
        var keyList = keys.ToList();
        if (!keyList.Any())
        {
            return new Dictionary<string, T?>();
        }

        var tasks = keyList.Select(async key => new { Key = key, Value = await LoadAsync<T>(key) });
        var results = await Task.WhenAll(tasks);
        
        var dictionary = results.ToDictionary(r => r.Key, r => r.Value);
        
        _logger.LogDebug("批量加载完成，数量: {Count}, Type: {Type}", keyList.Count, typeof(T).Name);
        return dictionary;
    }

    /// <summary>
    /// 预加载指定类型的所有数据到缓存
    /// </summary>
    public async Task PreloadCacheAsync<T>() where T : class
    {
        ThrowIfDisposed();
        
        if (!_options.SyncStrategy.CacheAside.PreloadTypes.Contains(typeof(T).Name))
        {
            _logger.LogDebug("类型 {Type} 未配置为预加载类型", typeof(T).Name);
            return;
        }

        try
        {
            var collection = _mongoManager.GetCollection<T>(_options.GetCollectionName<T>());
            var allData = await collection.Find(_ => true).ToListAsync();
            
            var tasks = allData.Select(async item =>
            {
                // 假设所有数据都有_id字段作为key
                var idProperty = typeof(T).GetProperty("Id") ?? typeof(T).GetProperty("_id");
                if (idProperty != null)
                {
                    var key = idProperty.GetValue(item)?.ToString();
                    if (!string.IsNullOrEmpty(key))
                    {
                        var expiry = _options.SyncStrategy.CacheAside.GetCacheTtl<T>();
                        await _syncService.WriteThrough(key, item, expiry);
                    }
                }
            });

            await Task.WhenAll(tasks);
            _logger.LogInformation("预加载缓存完成，类型: {Type}, 数量: {Count}", typeof(T).Name, allData.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预加载缓存失败，类型: {Type}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// 从MongoDB加载数据
    /// </summary>
    private async Task<T?> LoadFromMongo<T>(string key) where T : class
    {
        try
        {
            var collection = _mongoManager.GetCollection<T>(_options.GetCollectionName<T>());
            var filter = MongoDB.Driver.Builders<T>.Filter.Eq("_id", key);
            var result = await collection.Find(filter).FirstOrDefaultAsync();
            
            _logger.LogDebug("从MongoDB加载数据: {Key}, Type: {Type}, Found: {Found}", 
                key, typeof(T).Name, result != null);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从MongoDB加载数据失败: {Key}, Type: {Type}", key, typeof(T).Name);
            return null;
        }
    }

    /// <summary>
    /// 检查对象是否已释放
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DataSyncManager));
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        
        try
        {
            // 在释放前确保所有数据都已同步
            FlushAsync().Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "释放时刷新数据失败");
        }

        _logger.LogInformation("数据同步管理器已释放");
    }
}
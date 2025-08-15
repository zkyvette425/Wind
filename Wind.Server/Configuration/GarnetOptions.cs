using System.ComponentModel.DataAnnotations;

namespace Wind.Server.Configuration;

/// <summary>
/// Garnet连接配置选项
/// 基于RedisOptions但针对Garnet的特性进行了调整
/// </summary>
public class GarnetOptions
{
    /// <summary>
    /// 配置节点名称
    /// </summary>
    public const string SectionName = "Garnet";

    /// <summary>
    /// Garnet连接字符串
    /// 示例: "localhost:6380" 或 "server1:6380,server2:6380,server3:6380"
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = "localhost:6380";

    /// <summary>
    /// 数据库索引 (0-15)
    /// Garnet支持多数据库，与Redis相同
    /// </summary>
    [Range(0, 15)]
    public int Database { get; set; } = 0;

    /// <summary>
    /// 连接超时时间 (毫秒)
    /// Garnet推荐较短的超时时间以利用其低延迟优势
    /// </summary>
    [Range(1000, 60000)]
    public int ConnectTimeout { get; set; } = 3000;

    /// <summary>
    /// 同步超时时间 (毫秒)
    /// </summary>
    [Range(1000, 60000)]
    public int SyncTimeout { get; set; } = 3000;

    /// <summary>
    /// 异步超时时间 (毫秒)
    /// </summary>
    [Range(1000, 60000)]
    public int AsyncTimeout { get; set; } = 3000;

    /// <summary>
    /// 连接池大小
    /// Garnet在高并发下表现更好，可以设置较大的池
    /// </summary>
    [Range(1, 100)]
    public int PoolSize { get; set; } = 15;

    /// <summary>
    /// 是否启用SSL
    /// </summary>
    public bool EnableSsl { get; set; } = false;

    /// <summary>
    /// 认证密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 用户名 (支持认证)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 是否启用集群模式
    /// Garnet支持集群，但实现方式可能与Redis有差异
    /// </summary>
    public bool EnableCluster { get; set; } = false;

    /// <summary>
    /// 键前缀，用于区分Garnet和Redis的数据
    /// </summary>
    public string KeyPrefix { get; set; } = "Wind:Garnet:";

    /// <summary>
    /// 默认TTL (秒)，0表示永不过期
    /// </summary>
    [Range(0, int.MaxValue)]
    public int DefaultTtlSeconds { get; set; } = 3600; // 1小时

    /// <summary>
    /// 是否启用数据压缩
    /// Garnet的内存管理更高效，可以考虑禁用压缩以获得更好性能
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// 重试次数
    /// Garnet的稳定性很好，可以减少重试
    /// </summary>
    [Range(0, 10)]
    public int RetryCount { get; set; } = 2;

    /// <summary>
    /// 重试间隔 (毫秒)
    /// </summary>
    [Range(100, 10000)]
    public int RetryDelay { get; set; } = 500;

    /// <summary>
    /// 是否启用健康检查
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    /// 健康检查间隔 (秒)
    /// Garnet响应快，可以更频繁检查
    /// </summary>
    [Range(10, 300)]
    public int HealthCheckIntervalSeconds { get; set; } = 20;

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new ArgumentException("Garnet连接字符串不能为空", nameof(ConnectionString));
        }

        if (EnableCluster && ConnectionString.Split(',').Length < 3)
        {
            throw new ArgumentException("Garnet集群模式至少需要3个节点", nameof(ConnectionString));
        }

        if (!string.IsNullOrEmpty(KeyPrefix) && !KeyPrefix.EndsWith(':'))
        {
            KeyPrefix += ':';
        }
    }

    /// <summary>
    /// 获取Garnet配置字符串
    /// 使用与Redis相同的配置格式，确保StackExchange.Redis兼容性
    /// </summary>
    public string GetConfigurationString()
    {
        var config = ConnectionString;

        if (!string.IsNullOrEmpty(Password))
        {
            config += $",password={Password}";
        }

        if (!string.IsNullOrEmpty(Username))
        {
            config += $",user={Username}";
        }

        config += $",connectTimeout={ConnectTimeout}";
        config += $",syncTimeout={SyncTimeout}";
        config += $",asyncTimeout={AsyncTimeout}";

        if (EnableSsl)
        {
            config += ",ssl=true";
        }

        if (EnableCluster)
        {
            config += ",abortConnect=false";
        }

        // Garnet特定配置
        // 确保使用RESP2协议以获得最佳兼容性
        config += ",protocol=2";

        return config;
    }
}
using System.ComponentModel.DataAnnotations;

namespace Wind.Server.Configuration;

/// <summary>
/// Redis连接配置选项
/// 支持单机和集群模式配置
/// </summary>
public class RedisOptions
{
    /// <summary>
    /// 配置节点名称
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis连接字符串
    /// 示例: "localhost:6379" 或 "server1:6379,server2:6379,server3:6379"
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// 数据库索引 (0-15)
    /// </summary>
    [Range(0, 15)]
    public int Database { get; set; } = 0;

    /// <summary>
    /// 连接超时时间 (毫秒)
    /// </summary>
    [Range(1000, 60000)]
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// 同步超时时间 (毫秒)
    /// </summary>
    [Range(1000, 60000)]
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// 异步超时时间 (毫秒)
    /// </summary>
    [Range(1000, 60000)]
    public int AsyncTimeout { get; set; } = 5000;

    /// <summary>
    /// 连接池大小
    /// </summary>
    [Range(1, 100)]
    public int PoolSize { get; set; } = 10;

    /// <summary>
    /// 是否启用SSL
    /// </summary>
    public bool EnableSsl { get; set; } = false;

    /// <summary>
    /// 认证密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 用户名 (Redis 6.0+)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 是否启用集群模式
    /// </summary>
    public bool EnableCluster { get; set; } = false;

    /// <summary>
    /// 键前缀，用于多租户或环境隔离
    /// </summary>
    public string KeyPrefix { get; set; } = "Wind:";

    /// <summary>
    /// 默认TTL (秒)，0表示永不过期
    /// </summary>
    [Range(0, int.MaxValue)]
    public int DefaultTtlSeconds { get; set; } = 3600; // 1小时

    /// <summary>
    /// 是否启用数据压缩
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// 重试次数
    /// </summary>
    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔 (毫秒)
    /// </summary>
    [Range(100, 10000)]
    public int RetryDelay { get; set; } = 1000;

    /// <summary>
    /// 是否启用健康检查
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    /// 健康检查间隔 (秒)
    /// </summary>
    [Range(10, 300)]
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new ArgumentException("Redis连接字符串不能为空", nameof(ConnectionString));
        }

        if (EnableCluster && ConnectionString.Split(',').Length < 3)
        {
            throw new ArgumentException("集群模式至少需要3个节点", nameof(ConnectionString));
        }

        if (!string.IsNullOrEmpty(KeyPrefix) && !KeyPrefix.EndsWith(':'))
        {
            KeyPrefix += ':';
        }
    }

    /// <summary>
    /// 获取Redis配置字符串
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

        return config;
    }
}
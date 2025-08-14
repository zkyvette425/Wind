using System.ComponentModel.DataAnnotations;

namespace Wind.Server.Configuration;

/// <summary>
/// MongoDB连接配置选项
/// 支持单节点、副本集和分片集群配置
/// </summary>
public class MongoDbOptions
{
    /// <summary>
    /// 配置节点名称
    /// </summary>
    public const string SectionName = "MongoDB";

    /// <summary>
    /// MongoDB连接字符串
    /// 示例: "mongodb://localhost:27017" 或 "mongodb://user:pass@localhost:27017/dbname"
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    /// <summary>
    /// 数据库名称
    /// </summary>
    [Required]
    public string DatabaseName { get; set; } = "windgame";

    /// <summary>
    /// 连接超时时间 (毫秒)
    /// </summary>
    [Range(1000, 60000)]
    public int ConnectTimeout { get; set; } = 10000;

    /// <summary>
    /// Socket超时时间 (毫秒)
    /// </summary>
    [Range(1000, 60000)]
    public int SocketTimeout { get; set; } = 30000;

    /// <summary>
    /// 服务器选择超时时间 (毫秒)
    /// </summary>
    [Range(1000, 60000)]
    public int ServerSelectionTimeout { get; set; } = 10000;

    /// <summary>
    /// 最大连接池大小
    /// </summary>
    [Range(1, 1000)]
    public int MaxConnectionPoolSize { get; set; } = 100;

    /// <summary>
    /// 最小连接池大小
    /// </summary>
    [Range(0, 100)]
    public int MinConnectionPoolSize { get; set; } = 0;

    /// <summary>
    /// 连接最大空闲时间 (毫秒)
    /// </summary>
    [Range(10000, 3600000)]
    public int MaxConnectionIdleTime { get; set; } = 600000; // 10分钟

    /// <summary>
    /// 连接最大生存时间 (毫秒)
    /// </summary>
    [Range(60000, 7200000)]
    public int MaxConnectionLifeTime { get; set; } = 1800000; // 30分钟

    /// <summary>
    /// 是否启用SSL
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// 读偏好设置
    /// </summary>
    public string ReadPreference { get; set; } = "Primary";

    /// <summary>
    /// 写关注设置
    /// </summary>
    public string WriteConcern { get; set; } = "Acknowledged";

    /// <summary>
    /// 读关注设置
    /// </summary>
    public string ReadConcern { get; set; } = "Local";

    /// <summary>
    /// 重试写入操作
    /// </summary>
    public bool RetryWrites { get; set; } = true;

    /// <summary>
    /// 重试读取操作
    /// </summary>
    public bool RetryReads { get; set; } = true;

    /// <summary>
    /// 集合名称配置
    /// </summary>
    public CollectionNames Collections { get; set; } = new();

    /// <summary>
    /// 是否启用健康检查
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    /// 健康检查间隔 (秒)
    /// </summary>
    [Range(10, 300)]
    public int HealthCheckIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new ArgumentException("MongoDB连接字符串不能为空", nameof(ConnectionString));
        }

        if (string.IsNullOrWhiteSpace(DatabaseName))
        {
            throw new ArgumentException("数据库名称不能为空", nameof(DatabaseName));
        }

        // 验证连接字符串格式
        if (!ConnectionString.StartsWith("mongodb://") && !ConnectionString.StartsWith("mongodb+srv://"))
        {
            throw new ArgumentException("MongoDB连接字符串格式不正确", nameof(ConnectionString));
        }

        // 验证读偏好设置
        var validReadPreferences = new[] { "Primary", "PrimaryPreferred", "Secondary", "SecondaryPreferred", "Nearest" };
        if (!validReadPreferences.Contains(ReadPreference))
        {
            throw new ArgumentException($"无效的读偏好设置: {ReadPreference}", nameof(ReadPreference));
        }

        // 验证写关注设置
        var validWriteConcerns = new[] { "Acknowledged", "Unacknowledged", "W1", "W2", "W3", "Majority", "WMajority" };
        if (!validWriteConcerns.Contains(WriteConcern))
        {
            throw new ArgumentException($"无效的写关注设置: {WriteConcern}", nameof(WriteConcern));
        }

        // 验证读关注设置
        var validReadConcerns = new[] { "Local", "Available", "Majority", "Linearizable", "Snapshot" };
        if (!validReadConcerns.Contains(ReadConcern))
        {
            throw new ArgumentException($"无效的读关注设置: {ReadConcern}", nameof(ReadConcern));
        }
    }

    /// <summary>
    /// 获取完整的MongoDB连接字符串
    /// </summary>
    public string GetConnectionString()
    {
        var uri = new UriBuilder(ConnectionString);
        
        // 添加数据库名称到路径
        if (string.IsNullOrEmpty(uri.Path) || uri.Path == "/")
        {
            uri.Path = $"/{DatabaseName}";
        }

        // 构建查询参数
        var queryParams = new List<string>();
        
        queryParams.Add($"connectTimeoutMS={ConnectTimeout}");
        queryParams.Add($"socketTimeoutMS={SocketTimeout}");
        queryParams.Add($"serverSelectionTimeoutMS={ServerSelectionTimeout}");
        queryParams.Add($"maxPoolSize={MaxConnectionPoolSize}");
        queryParams.Add($"minPoolSize={MinConnectionPoolSize}");
        queryParams.Add($"maxIdleTimeMS={MaxConnectionIdleTime}");
        queryParams.Add($"maxLifeTimeMS={MaxConnectionLifeTime}");
        
        if (UseSsl)
        {
            queryParams.Add("ssl=true");
        }
        
        queryParams.Add($"readPreference={ReadPreference}");
        queryParams.Add($"w={WriteConcern}");
        queryParams.Add($"readConcernLevel={ReadConcern}");
        
        if (RetryWrites)
        {
            queryParams.Add("retryWrites=true");
        }
        
        if (RetryReads)
        {
            queryParams.Add("retryReads=true");
        }

        // 合并现有查询参数
        if (!string.IsNullOrEmpty(uri.Query))
        {
            var existingParams = uri.Query.TrimStart('?');
            queryParams.Insert(0, existingParams);
        }

        uri.Query = string.Join("&", queryParams);
        
        return uri.ToString();
    }
}

/// <summary>
/// 集合名称配置
/// </summary>
public class CollectionNames
{
    /// <summary>
    /// 玩家数据集合
    /// </summary>
    public string Players { get; set; } = "players";

    /// <summary>
    /// 房间数据集合
    /// </summary>
    public string Rooms { get; set; } = "rooms";

    /// <summary>
    /// 匹配数据集合
    /// </summary>
    public string Matchmaking { get; set; } = "matchmaking";

    /// <summary>
    /// 游戏记录集合
    /// </summary>
    public string GameRecords { get; set; } = "game_records";

    /// <summary>
    /// 玩家统计集合
    /// </summary>
    public string PlayerStats { get; set; } = "player_stats";

    /// <summary>
    /// 系统日志集合
    /// </summary>
    public string SystemLogs { get; set; } = "system_logs";

    /// <summary>
    /// 审计日志集合
    /// </summary>
    public string AuditLogs { get; set; } = "audit_logs";
}
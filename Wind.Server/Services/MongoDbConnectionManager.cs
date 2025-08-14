using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using Wind.Server.Configuration;

namespace Wind.Server.Services;

/// <summary>
/// MongoDB连接管理器
/// 提供连接池、健康检查、重连机制功能
/// </summary>
public class MongoDbConnectionManager : IDisposable
{
    private readonly MongoDbOptions _options;
    private readonly ILogger<MongoDbConnectionManager> _logger;
    private readonly Lazy<IMongoClient> _client;
    private readonly Lazy<IMongoDatabase> _database;
    private Timer? _healthCheckTimer;
    private volatile bool _disposed = false;

    public MongoDbConnectionManager(IOptions<MongoDbOptions> options, ILogger<MongoDbConnectionManager> logger)
    {
        _options = options.Value;
        _logger = logger;

        // 验证配置
        _options.Validate();

        // 延迟初始化客户端和数据库
        _client = new Lazy<IMongoClient>(CreateClient);
        _database = new Lazy<IMongoDatabase>(() => _client.Value.GetDatabase(_options.DatabaseName));

        // 启动健康检查
        if (_options.EnableHealthCheck)
        {
            _healthCheckTimer = new Timer(PerformHealthCheck, null,
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds),
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds));
        }

        _logger.LogInformation("MongoDB连接管理器已初始化，数据库: {DatabaseName}",
            _options.DatabaseName);
    }

    /// <summary>
    /// 获取MongoDB客户端
    /// </summary>
    public IMongoClient GetClient()
    {
        ThrowIfDisposed();
        return _client.Value;
    }

    /// <summary>
    /// 获取数据库实例
    /// </summary>
    public IMongoDatabase GetDatabase()
    {
        ThrowIfDisposed();
        return _database.Value;
    }

    /// <summary>
    /// 获取指定集合
    /// </summary>
    public IMongoCollection<T> GetCollection<T>(string collectionName)
    {
        ThrowIfDisposed();
        return GetDatabase().GetCollection<T>(collectionName);
    }

    /// <summary>
    /// 创建MongoDB客户端
    /// </summary>
    private IMongoClient CreateClient()
    {
        try
        {
            var connectionString = _options.GetConnectionString();
            
            _logger.LogInformation("正在连接MongoDB服务器: {Database}", _options.DatabaseName);
            _logger.LogDebug("连接字符串: {ConnectionString}", 
                connectionString.Replace(_options.ConnectionString.Split('@').FirstOrDefault()?.Split("://").LastOrDefault() ?? "", "****"));

            var settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
            
            // 配置连接设置
            settings.ConnectTimeout = TimeSpan.FromMilliseconds(_options.ConnectTimeout);
            settings.SocketTimeout = TimeSpan.FromMilliseconds(_options.SocketTimeout);
            settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(_options.ServerSelectionTimeout);
            settings.MaxConnectionPoolSize = _options.MaxConnectionPoolSize;
            settings.MinConnectionPoolSize = _options.MinConnectionPoolSize;
            settings.MaxConnectionIdleTime = TimeSpan.FromMilliseconds(_options.MaxConnectionIdleTime);
            settings.MaxConnectionLifeTime = TimeSpan.FromMilliseconds(_options.MaxConnectionLifeTime);

            // 配置读偏好
            settings.ReadPreference = _options.ReadPreference switch
            {
                "Primary" => ReadPreference.Primary,
                "PrimaryPreferred" => ReadPreference.PrimaryPreferred,
                "Secondary" => ReadPreference.Secondary,
                "SecondaryPreferred" => ReadPreference.SecondaryPreferred,
                "Nearest" => ReadPreference.Nearest,
                _ => ReadPreference.Primary
            };

            // 配置写关注
            settings.WriteConcern = _options.WriteConcern switch
            {
                "Acknowledged" => WriteConcern.Acknowledged,
                "Unacknowledged" => WriteConcern.Unacknowledged,
                "W1" => WriteConcern.W1,
                "W2" => WriteConcern.W2,
                "W3" => WriteConcern.W3,
                "Majority" => WriteConcern.WMajority,
                "WMajority" => WriteConcern.WMajority,
                _ => WriteConcern.Acknowledged
            };

            // 配置读关注
            settings.ReadConcern = _options.ReadConcern switch
            {
                "Local" => ReadConcern.Local,
                "Available" => ReadConcern.Available,
                "Majority" => ReadConcern.Majority,
                "Linearizable" => ReadConcern.Linearizable,
                "Snapshot" => ReadConcern.Snapshot,
                _ => ReadConcern.Local
            };

            // 重试设置
            settings.RetryWrites = _options.RetryWrites;
            settings.RetryReads = _options.RetryReads;

            // 应用名称，用于监控和调试
            settings.ApplicationName = "Wind.GameServer";

            var client = new MongoClient(settings);

            // 测试连接
            var adminDb = client.GetDatabase("admin");
            var pingResult = adminDb.RunCommand<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));

            _logger.LogInformation("MongoDB连接成功建立: {DatabaseName}", _options.DatabaseName);
            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB连接失败");
            throw new InvalidOperationException($"无法连接到MongoDB服务器: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    private void PerformHealthCheck(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // 检查客户端是否已初始化
            if (!_client.IsValueCreated)
            {
                return;
            }

            var database = GetDatabase();
            var start = DateTime.UtcNow;
            
            // 执行ping命令
            var result = database.RunCommand<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
            var duration = DateTime.UtcNow - start;

            if (result.Contains("ok") && result["ok"].AsDouble == 1.0)
            {
                _logger.LogDebug("MongoDB健康检查通过，响应时间: {Duration}ms", duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("MongoDB健康检查异常，响应: {Response}", result.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MongoDB健康检查失败");
        }
    }

    /// <summary>
    /// 获取连接统计信息
    /// </summary>
    public async Task<string> GetConnectionInfoAsync()
    {
        ThrowIfDisposed();

        try
        {
            if (!_client.IsValueCreated)
            {
                return "客户端未初始化";
            }

            var client = GetClient();
            var database = GetDatabase();
            
            // 获取服务器状态
            var serverStatus = await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("serverStatus", 1));

            var info = new
            {
                DatabaseName = _options.DatabaseName,
                IsConnected = true,
                ServerInfo = new
                {
                    Version = serverStatus.GetValue("version", "Unknown"),
                    Uptime = serverStatus.GetValue("uptime", 0),
                    Connections = serverStatus.GetValue("connections", new MongoDB.Bson.BsonDocument())
                },
                Settings = new
                {
                    ConnectTimeout = _options.ConnectTimeout,
                    SocketTimeout = _options.SocketTimeout,
                    MaxPoolSize = _options.MaxConnectionPoolSize,
                    MinPoolSize = _options.MinConnectionPoolSize,
                    ReadPreference = _options.ReadPreference,
                    WriteConcern = _options.WriteConcern,
                    ReadConcern = _options.ReadConcern
                }
            };

            return System.Text.Json.JsonSerializer.Serialize(info, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            return $"获取连接信息失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 检查对象是否已释放
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MongoDbConnectionManager));
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

        _healthCheckTimer?.Dispose();

        if (_client.IsValueCreated)
        {
            // MongoDB客户端会自动处理连接池的清理
            _logger.LogInformation("MongoDB连接管理器已释放");
        }
    }
}
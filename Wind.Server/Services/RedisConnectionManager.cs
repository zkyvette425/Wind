using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wind.Server.Configuration;
using System.Collections.Concurrent;

namespace Wind.Server.Services;

/// <summary>
/// Redis连接管理器
/// 提供连接池、重连机制、健康检查功能
/// </summary>
public class RedisConnectionManager : IDisposable
{
    private readonly RedisOptions _options;
    private readonly ILogger<RedisConnectionManager> _logger;
    private readonly ConcurrentDictionary<int, IDatabase> _databases;
    private ConnectionMultiplexer? _connection;
    private readonly object _lockObject = new();
    private volatile bool _disposed = false;
    private Timer? _healthCheckTimer;

    public RedisConnectionManager(IOptions<RedisOptions> options, ILogger<RedisConnectionManager> logger)
    {
        _options = options.Value;
        _logger = logger;
        _databases = new ConcurrentDictionary<int, IDatabase>();

        // 验证配置
        _options.Validate();

        // 启动健康检查
        if (_options.EnableHealthCheck)
        {
            _healthCheckTimer = new Timer(PerformHealthCheck, null, 
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds), 
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds));
        }

        _logger.LogInformation("Redis连接管理器已初始化，配置: {Config}", 
            _options.GetConfigurationString().Replace(_options.Password ?? "", "****"));
    }

    /// <summary>
    /// 获取Redis连接
    /// </summary>
    public ConnectionMultiplexer GetConnection()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RedisConnectionManager));
        }

        if (_connection != null && _connection.IsConnected)
        {
            return _connection;
        }

        lock (_lockObject)
        {
            if (_connection != null && _connection.IsConnected)
            {
                return _connection;
            }

            // 关闭旧连接
            _connection?.Dispose();
            _databases.Clear();

            // 创建新连接
            _connection = CreateConnection();
            return _connection;
        }
    }

    /// <summary>
    /// 获取数据库实例
    /// </summary>
    public IDatabase GetDatabase(int database = -1)
    {
        var db = database == -1 ? _options.Database : database;
        
        return _databases.GetOrAdd(db, dbIndex =>
        {
            var connection = GetConnection();
            return connection.GetDatabase(dbIndex);
        });
    }

    /// <summary>
    /// 获取服务器实例
    /// </summary>
    public IServer GetServer(int index = 0)
    {
        var connection = GetConnection();
        var endpoints = connection.GetEndPoints();
        
        if (endpoints.Length == 0)
        {
            throw new InvalidOperationException("没有可用的Redis服务器端点");
        }

        var endpoint = endpoints[Math.Min(index, endpoints.Length - 1)];
        return connection.GetServer(endpoint);
    }

    /// <summary>
    /// 获取订阅器
    /// </summary>
    public ISubscriber GetSubscriber()
    {
        var connection = GetConnection();
        return connection.GetSubscriber();
    }

    /// <summary>
    /// 创建Redis连接
    /// </summary>
    private ConnectionMultiplexer CreateConnection()
    {
        var configString = _options.GetConfigurationString();
        var config = ConfigurationOptions.Parse(configString);
        
        // 配置连接选项
        config.ConnectRetry = _options.RetryCount;
        config.ConnectTimeout = _options.ConnectTimeout;
        config.SyncTimeout = _options.SyncTimeout;
        config.AsyncTimeout = _options.AsyncTimeout;
        config.AbortOnConnectFail = !_options.EnableCluster;

        var retryCount = 0;
        ConnectionMultiplexer? connection = null;

        while (retryCount <= _options.RetryCount)
        {
            try
            {
                _logger.LogInformation("正在连接Redis服务器，尝试次数: {Attempt}/{MaxAttempts}", 
                    retryCount + 1, _options.RetryCount + 1);

                connection = ConnectionMultiplexer.Connect(config);

                // 注册连接事件
                RegisterConnectionEvents(connection);

                // 验证连接
                var database = connection.GetDatabase(_options.Database);
                var pingResult = database.Ping();

                _logger.LogInformation("Redis连接成功建立，Ping延迟: {Latency}ms", 
                    pingResult.TotalMilliseconds);

                return connection;
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "Redis连接失败，尝试次数: {Attempt}/{MaxAttempts}", 
                    retryCount, _options.RetryCount + 1);

                connection?.Dispose();

                if (retryCount <= _options.RetryCount)
                {
                    Thread.Sleep(_options.RetryDelay);
                }
            }
        }

        throw new InvalidOperationException($"无法连接到Redis服务器，已尝试 {_options.RetryCount + 1} 次");
    }

    /// <summary>
    /// 注册连接事件
    /// </summary>
    private void RegisterConnectionEvents(ConnectionMultiplexer connection)
    {
        connection.ConnectionFailed += (sender, args) =>
        {
            _logger.LogError("Redis连接失败: {Endpoint}, 异常: {Exception}", 
                args.EndPoint, args.Exception?.Message);
        };

        connection.ConnectionRestored += (sender, args) =>
        {
            _logger.LogInformation("Redis连接已恢复: {Endpoint}", args.EndPoint);
        };

        connection.ErrorMessage += (sender, args) =>
        {
            _logger.LogError("Redis错误消息: {Message}", args.Message);
        };

        connection.InternalError += (sender, args) =>
        {
            _logger.LogError(args.Exception, "Redis内部错误: {Origin}", args.Origin);
        };
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    private void PerformHealthCheck(object? state)
    {
        if (_disposed || _connection == null)
        {
            return;
        }

        try
        {
            var database = GetDatabase();
            var pingResult = database.Ping();
            
            _logger.LogDebug("Redis健康检查通过，Ping延迟: {Latency}ms", 
                pingResult.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis健康检查失败");
            
            // 尝试重新连接
            try
            {
                lock (_lockObject)
                {
                    _connection?.Dispose();
                    _connection = null;
                    _databases.Clear();
                }
                
                GetConnection(); // 触发重连
            }
            catch (Exception reconnectEx)
            {
                _logger.LogError(reconnectEx, "Redis重连失败");
            }
        }
    }

    /// <summary>
    /// 获取连接统计信息
    /// </summary>
    public string GetConnectionInfo()
    {
        if (_connection == null)
        {
            return "未连接";
        }

        var info = new
        {
            IsConnected = _connection.IsConnected,
            ClientName = _connection.ClientName,
            Configuration = _connection.Configuration,
            TimeoutMilliseconds = _connection.TimeoutMilliseconds,
            OperationCount = _connection.OperationCount
        };

        return System.Text.Json.JsonSerializer.Serialize(info, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
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
        _databases.Clear();
        _connection?.Dispose();

        _logger.LogInformation("Redis连接管理器已释放");
    }
}
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wind.Server.Configuration;
using System.Collections.Concurrent;

namespace Wind.Server.Services;

/// <summary>
/// Garnet连接管理器
/// 提供连接池、重连机制、健康检查功能
/// 与Redis连接管理器相同的接口，但针对Garnet进行了优化
/// </summary>
public class GarnetConnectionManager : IDisposable
{
    private readonly GarnetOptions _options;
    private readonly ILogger<GarnetConnectionManager> _logger;
    private readonly ConcurrentDictionary<int, IDatabase> _databases;
    private ConnectionMultiplexer? _connection;
    private readonly object _lockObject = new();
    private volatile bool _disposed = false;
    private Timer? _healthCheckTimer;

    public GarnetConnectionManager(IOptions<GarnetOptions> options, ILogger<GarnetConnectionManager> logger)
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

        _logger.LogInformation("Garnet连接管理器已初始化，配置: {Config}", 
            _options.GetConfigurationString().Replace(_options.Password ?? "", "****"));
    }

    /// <summary>
    /// 获取Garnet连接
    /// </summary>
    public ConnectionMultiplexer GetConnection()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GarnetConnectionManager));
        }

        if (_connection?.IsConnected == true)
        {
            return _connection;
        }

        lock (_lockObject)
        {
            if (_connection?.IsConnected == true)
            {
                return _connection;
            }

            try
            {
                _connection?.Dispose();
                _connection = CreateConnection();
                _logger.LogInformation("Garnet连接已建立");
                return _connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建Garnet连接失败");
                throw;
            }
        }
    }

    /// <summary>
    /// 获取指定数据库
    /// </summary>
    public IDatabase GetDatabase(int database = -1)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GarnetConnectionManager));
        }

        var dbIndex = database == -1 ? _options.Database : database;

        return _databases.GetOrAdd(dbIndex, index =>
        {
            var connection = GetConnection();
            var db = connection.GetDatabase(index);
            _logger.LogDebug("获取Garnet数据库 {Database}", index);
            return db;
        });
    }

    /// <summary>
    /// 创建连接
    /// </summary>
    private ConnectionMultiplexer CreateConnection()
    {
        var configOptions = ConfigurationOptions.Parse(_options.GetConfigurationString());
        
        // Garnet特定配置调整
        configOptions.ConnectTimeout = _options.ConnectTimeout;
        configOptions.SyncTimeout = _options.SyncTimeout;
        configOptions.AsyncTimeout = _options.AsyncTimeout;
        configOptions.ConnectRetry = _options.RetryCount;
        configOptions.Ssl = _options.EnableSsl;
        
        // 针对Garnet的特殊配置
        // Garnet支持RESP2协议，确保兼容性
        configOptions.DefaultDatabase = _options.Database;
        
        var connection = ConnectionMultiplexer.Connect(configOptions);

        // 连接事件处理
        connection.ConnectionFailed += (sender, e) =>
        {
            _logger.LogWarning("Garnet连接失败: {EndPoint} - {FailureType}", e.EndPoint, e.FailureType);
        };

        connection.ConnectionRestored += (sender, e) =>
        {
            _logger.LogInformation("Garnet连接已恢复: {EndPoint}", e.EndPoint);
        };

        connection.ErrorMessage += (sender, e) =>
        {
            _logger.LogError("Garnet错误: {EndPoint} - {Message}", e.EndPoint, e.Message);
        };

        return connection;
    }

    /// <summary>
    /// 执行健康检查
    /// </summary>
    private async void PerformHealthCheck(object? state)
    {
        try
        {
            if (_disposed || _connection?.IsConnected != true)
            {
                return;
            }

            var database = GetDatabase();
            await database.PingAsync();
            
            _logger.LogDebug("Garnet健康检查通过");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Garnet健康检查失败，将尝试重连");
            
            // 健康检查失败时，清理连接以触发重连
            lock (_lockObject)
            {
                _connection?.Dispose();
                _connection = null;
                _databases.Clear();
            }
        }
    }

    /// <summary>
    /// 获取连接统计信息
    /// </summary>
    public string GetConnectionStats()
    {
        if (_connection?.IsConnected != true)
        {
            return "Garnet连接未建立";
        }

        var server = _connection.GetServer(_connection.GetEndPoints().First());
        return $"Garnet连接状态: 已连接, 数据库数量: {_databases.Count}, 服务器: {server.EndPoint}";
    }

    /// <summary>
    /// 测试连接
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var database = GetDatabase();
            await database.PingAsync();
            _logger.LogInformation("Garnet连接测试成功");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Garnet连接测试失败");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _healthCheckTimer?.Dispose();
        _connection?.Dispose();
        _databases.Clear();

        _logger.LogInformation("Garnet连接管理器已释放");
    }
}
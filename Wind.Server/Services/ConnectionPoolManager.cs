using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;

namespace Wind.Server.Services
{
    /// <summary>
    /// 连接池管理器 - 管理MagicOnion客户端连接的生命周期和复用
    /// </summary>
    public class ConnectionPoolManager : IDisposable
    {
        private readonly ILogger<ConnectionPoolManager> _logger;
        private readonly ConnectionPoolOptions _options;
        private readonly ConcurrentDictionary<string, ClientConnection> _connections;
        private readonly ConcurrentDictionary<string, ConnectionGroup> _connectionGroups;
        private readonly Timer _cleanupTimer;
        private readonly object _lock = new object();
        private bool _disposed = false;

        public ConnectionPoolManager(ILogger<ConnectionPoolManager> logger, IOptions<ConnectionPoolOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            _connections = new ConcurrentDictionary<string, ClientConnection>();
            _connectionGroups = new ConcurrentDictionary<string, ConnectionGroup>();
            
            // 启动定期清理任务
            _cleanupTimer = new Timer(CleanupExpiredConnections, null, 
                TimeSpan.FromSeconds(_options.CleanupIntervalSeconds), 
                TimeSpan.FromSeconds(_options.CleanupIntervalSeconds));

            _logger.LogInformation("ConnectionPoolManager initialized with MaxPoolSize={MaxPoolSize}, ConnectionTimeout={ConnectionTimeout}s",
                _options.MaxPoolSize, _options.ConnectionTimeoutSeconds);
        }

        /// <summary>
        /// 注册新的客户端连接
        /// </summary>
        public async Task<bool> RegisterConnectionAsync(string connectionId, string playerId, string hubType, IPEndPoint? clientEndPoint = null)
        {
            try
            {
                if (_connections.Count >= _options.MaxPoolSize)
                {
                    _logger.LogWarning("Connection pool is full. MaxPoolSize={MaxPoolSize}, Current={Current}", 
                        _options.MaxPoolSize, _connections.Count);
                    
                    // 尝试清理过期连接释放空间
                    await CleanupExpiredConnectionsAsync();
                    
                    if (_connections.Count >= _options.MaxPoolSize)
                    {
                        return false; // 池已满，拒绝连接
                    }
                }

                var connection = new ClientConnection
                {
                    ConnectionId = connectionId,
                    PlayerId = playerId,
                    HubType = hubType,
                    ClientEndPoint = clientEndPoint,
                    ConnectedAt = DateTime.UtcNow,
                    LastActiveAt = DateTime.UtcNow,
                    IsActive = true,
                    Metadata = new Dictionary<string, object>()
                };

                if (_connections.TryAdd(connectionId, connection))
                {
                    // 添加到连接组
                    var groupKey = GetConnectionGroupKey(hubType, playerId);
                    _connectionGroups.AddOrUpdate(groupKey, 
                        new ConnectionGroup { GroupKey = groupKey, Connections = [connectionId] },
                        (key, group) => 
                        {
                            lock (group.Connections)
                            {
                                if (!group.Connections.Contains(connectionId))
                                {
                                    group.Connections.Add(connectionId);
                                }
                            }
                            return group;
                        });

                    _logger.LogInformation("Connection registered: {ConnectionId} for Player={PlayerId}, Hub={HubType}, Endpoint={EndPoint}", 
                        connectionId, playerId, hubType, clientEndPoint);
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register connection {ConnectionId} for player {PlayerId}", connectionId, playerId);
                return false;
            }
        }

        /// <summary>
        /// 注销客户端连接
        /// </summary>
        public async Task<bool> UnregisterConnectionAsync(string connectionId, string reason = "Normal disconnect")
        {
            try
            {
                if (_connections.TryRemove(connectionId, out var connection))
                {
                    connection.IsActive = false;
                    connection.DisconnectedAt = DateTime.UtcNow;
                    connection.DisconnectReason = reason;

                    // 从连接组中移除
                    var groupKey = GetConnectionGroupKey(connection.HubType, connection.PlayerId);
                    if (_connectionGroups.TryGetValue(groupKey, out var group))
                    {
                        lock (group.Connections)
                        {
                            group.Connections.Remove(connectionId);
                            if (group.Connections.Count == 0)
                            {
                                _connectionGroups.TryRemove(groupKey, out _);
                            }
                        }
                    }

                    _logger.LogInformation("Connection unregistered: {ConnectionId} for Player={PlayerId}, Reason={Reason}, Duration={Duration}s", 
                        connectionId, connection.PlayerId, reason, 
                        (DateTime.UtcNow - connection.ConnectedAt).TotalSeconds);
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unregister connection {ConnectionId}, reason: {Reason}", connectionId, reason);
                return false;
            }
        }

        /// <summary>
        /// 更新连接活跃时间
        /// </summary>
        public bool UpdateConnectionActivity(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.LastActiveAt = DateTime.UtcNow;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取连接信息
        /// </summary>
        public ClientConnection? GetConnection(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var connection);
            return connection;
        }

        /// <summary>
        /// 获取玩家的活跃连接
        /// </summary>
        public List<ClientConnection> GetPlayerConnections(string playerId)
        {
            return _connections.Values
                .Where(c => c.PlayerId == playerId && c.IsActive)
                .ToList();
        }

        /// <summary>
        /// 获取指定Hub类型的所有活跃连接
        /// </summary>
        public List<ClientConnection> GetHubConnections(string hubType)
        {
            return _connections.Values
                .Where(c => c.HubType == hubType && c.IsActive)
                .ToList();
        }

        /// <summary>
        /// 获取连接池统计信息
        /// </summary>
        public ConnectionPoolStats GetStats()
        {
            var activeConnections = _connections.Values.Where(c => c.IsActive).ToList();
            var now = DateTime.UtcNow;
            
            return new ConnectionPoolStats
            {
                TotalConnections = _connections.Count,
                ActiveConnections = activeConnections.Count,
                IdleConnections = activeConnections.Count(c => (now - c.LastActiveAt).TotalSeconds > _options.IdleTimeoutSeconds),
                ConnectionsByHubType = activeConnections.GroupBy(c => c.HubType).ToDictionary(g => g.Key, g => g.Count()),
                AverageConnectionDuration = activeConnections.Count > 0 ? 
                    activeConnections.Average(c => (now - c.ConnectedAt).TotalSeconds) : 0,
                OldestConnectionAge = activeConnections.Count > 0 ? 
                    activeConnections.Max(c => (now - c.ConnectedAt).TotalSeconds) : 0
            };
        }

        /// <summary>
        /// 检查连接是否健康
        /// </summary>
        public async Task<ConnectionHealthStatus> CheckConnectionHealthAsync(string connectionId)
        {
            if (!_connections.TryGetValue(connectionId, out var connection))
            {
                return new ConnectionHealthStatus { IsHealthy = false, Reason = "Connection not found" };
            }

            if (!connection.IsActive)
            {
                return new ConnectionHealthStatus { IsHealthy = false, Reason = "Connection inactive" };
            }

            var inactiveTime = (DateTime.UtcNow - connection.LastActiveAt).TotalSeconds;
            if (inactiveTime > _options.ConnectionTimeoutSeconds)
            {
                return new ConnectionHealthStatus 
                { 
                    IsHealthy = false, 
                    Reason = $"Connection timeout: {inactiveTime}s > {_options.ConnectionTimeoutSeconds}s" 
                };
            }

            // 可以在这里添加更多健康检查逻辑，如发送ping消息等
            return new ConnectionHealthStatus { IsHealthy = true };
        }

        /// <summary>
        /// 清理过期连接
        /// </summary>
        private async Task CleanupExpiredConnectionsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredConnections = _connections.Values
                    .Where(c => c.IsActive && (now - c.LastActiveAt).TotalSeconds > _options.ConnectionTimeoutSeconds)
                    .ToList();

                foreach (var connection in expiredConnections)
                {
                    await UnregisterConnectionAsync(connection.ConnectionId, "Connection timeout");
                }

                if (expiredConnections.Count > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired connections", expiredConnections.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection cleanup");
            }
        }

        private void CleanupExpiredConnections(object? state)
        {
            _ = Task.Run(CleanupExpiredConnectionsAsync);
        }

        private static string GetConnectionGroupKey(string hubType, string playerId)
        {
            return $"{hubType}:{playerId}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                
                // 清理所有连接
                foreach (var connection in _connections.Values)
                {
                    if (connection.IsActive)
                    {
                        connection.IsActive = false;
                        connection.DisconnectedAt = DateTime.UtcNow;
                        connection.DisconnectReason = "Service shutdown";
                    }
                }
                
                _connections.Clear();
                _connectionGroups.Clear();
                _disposed = true;
                
                _logger.LogInformation("ConnectionPoolManager disposed");
            }
        }
    }

    /// <summary>
    /// 客户端连接信息
    /// </summary>
    public class ClientConnection
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string HubType { get; set; } = string.Empty;
        public IPEndPoint? ClientEndPoint { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime LastActiveAt { get; set; }
        public DateTime? DisconnectedAt { get; set; }
        public bool IsActive { get; set; }
        public string? DisconnectReason { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// 连接组 - 按Hub类型和玩家分组管理连接
    /// </summary>
    public class ConnectionGroup
    {
        public string GroupKey { get; set; } = string.Empty;
        public List<string> Connections { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 连接池统计信息
    /// </summary>
    public class ConnectionPoolStats
    {
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public Dictionary<string, int> ConnectionsByHubType { get; set; } = new();
        public double AverageConnectionDuration { get; set; }
        public double OldestConnectionAge { get; set; }
    }

    /// <summary>
    /// 连接健康状态
    /// </summary>
    public class ConnectionHealthStatus
    {
        public bool IsHealthy { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// 连接池配置选项
    /// </summary>
    public class ConnectionPoolOptions
    {
        public int MaxPoolSize { get; set; } = 10000; // 最大连接数
        public int ConnectionTimeoutSeconds { get; set; } = 300; // 连接超时时间(秒)
        public int IdleTimeoutSeconds { get; set; } = 120; // 空闲超时时间(秒)
        public int CleanupIntervalSeconds { get; set; } = 60; // 清理间隔(秒)
        public bool EnableConnectionMetrics { get; set; } = true; // 是否启用连接指标收集
        public bool EnableHealthCheck { get; set; } = true; // 是否启用健康检查
    }
}
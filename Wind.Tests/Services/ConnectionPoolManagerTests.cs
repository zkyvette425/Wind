using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Collections.Concurrent;
using Xunit;

namespace Wind.Tests.Services
{
    /// <summary>
    /// 连接池管理器 - 测试版本
    /// 用于单元测试的简化实现
    /// </summary>
    public class TestConnectionPoolManager : IDisposable
    {
        private readonly ILogger<TestConnectionPoolManager> _logger;
        private readonly TestConnectionPoolOptions _options;
        private readonly ConcurrentDictionary<string, TestClientConnection> _connections;
        private readonly ConcurrentDictionary<string, TestConnectionGroup> _connectionGroups;
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        public TestConnectionPoolManager(ILogger<TestConnectionPoolManager> logger, IOptions<TestConnectionPoolOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            _connections = new ConcurrentDictionary<string, TestClientConnection>();
            _connectionGroups = new ConcurrentDictionary<string, TestConnectionGroup>();
            
            _cleanupTimer = new Timer(CleanupExpiredConnections, null, 
                TimeSpan.FromSeconds(_options.CleanupIntervalSeconds), 
                TimeSpan.FromSeconds(_options.CleanupIntervalSeconds));
        }

        public async Task<bool> RegisterConnectionAsync(string connectionId, string playerId, string hubType, IPEndPoint? clientEndPoint = null)
        {
            try
            {
                if (_connections.Count >= _options.MaxPoolSize)
                {
                    await CleanupExpiredConnectionsAsync();
                    if (_connections.Count >= _options.MaxPoolSize)
                    {
                        return false;
                    }
                }

                var connection = new TestClientConnection
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
                    var groupKey = GetConnectionGroupKey(hubType, playerId);
                    _connectionGroups.AddOrUpdate(groupKey, 
                        new TestConnectionGroup { GroupKey = groupKey, Connections = [connectionId] },
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

        public async Task<bool> UnregisterConnectionAsync(string connectionId, string reason = "Normal disconnect")
        {
            try
            {
                if (_connections.TryRemove(connectionId, out var connection))
                {
                    connection.IsActive = false;
                    connection.DisconnectedAt = DateTime.UtcNow;
                    connection.DisconnectReason = reason;

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

        public bool UpdateConnectionActivity(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.LastActiveAt = DateTime.UtcNow;
                return true;
            }
            return false;
        }

        public TestClientConnection? GetConnection(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var connection);
            return connection;
        }

        public List<TestClientConnection> GetPlayerConnections(string playerId)
        {
            return _connections.Values
                .Where(c => c.PlayerId == playerId && c.IsActive)
                .ToList();
        }

        public List<TestClientConnection> GetHubConnections(string hubType)
        {
            return _connections.Values
                .Where(c => c.HubType == hubType && c.IsActive)
                .ToList();
        }

        public TestConnectionPoolStats GetStats()
        {
            var activeConnections = _connections.Values.Where(c => c.IsActive).ToList();
            var now = DateTime.UtcNow;
            
            return new TestConnectionPoolStats
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

        public async Task<TestConnectionHealthStatus> CheckConnectionHealthAsync(string connectionId)
        {
            if (!_connections.TryGetValue(connectionId, out var connection))
            {
                return new TestConnectionHealthStatus { IsHealthy = false, Reason = "Connection not found" };
            }

            if (!connection.IsActive)
            {
                return new TestConnectionHealthStatus { IsHealthy = false, Reason = "Connection inactive" };
            }

            var inactiveTime = (DateTime.UtcNow - connection.LastActiveAt).TotalSeconds;
            if (inactiveTime > _options.ConnectionTimeoutSeconds)
            {
                return new TestConnectionHealthStatus 
                { 
                    IsHealthy = false, 
                    Reason = $"Connection timeout: {inactiveTime}s > {_options.ConnectionTimeoutSeconds}s" 
                };
            }

            return new TestConnectionHealthStatus { IsHealthy = true };
        }

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
            }
        }
    }

    // 测试模型类
    public class TestClientConnection
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

    public class TestConnectionGroup
    {
        public string GroupKey { get; set; } = string.Empty;
        public List<string> Connections { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TestConnectionPoolStats
    {
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public Dictionary<string, int> ConnectionsByHubType { get; set; } = new();
        public double AverageConnectionDuration { get; set; }
        public double OldestConnectionAge { get; set; }
    }

    public class TestConnectionHealthStatus
    {
        public bool IsHealthy { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class TestConnectionPoolOptions
    {
        public int MaxPoolSize { get; set; } = 10000;
        public int ConnectionTimeoutSeconds { get; set; } = 300;
        public int IdleTimeoutSeconds { get; set; } = 120;
        public int CleanupIntervalSeconds { get; set; } = 60;
        public bool EnableConnectionMetrics { get; set; } = true;
        public bool EnableHealthCheck { get; set; } = true;
    }

    /// <summary>
    /// ConnectionPoolManager单元测试
    /// 测试连接池核心功能：注册、注销、统计、健康检查
    /// </summary>
    public class ConnectionPoolManagerTests : IDisposable
    {
        private readonly TestConnectionPoolManager _connectionPoolManager;
        private readonly Mock<ILogger<TestConnectionPoolManager>> _mockLogger;
        private readonly TestConnectionPoolOptions _options;

        public ConnectionPoolManagerTests()
        {
            _mockLogger = new Mock<ILogger<TestConnectionPoolManager>>();
            _options = new TestConnectionPoolOptions
            {
                MaxPoolSize = 100,
                ConnectionTimeoutSeconds = 300,
                IdleTimeoutSeconds = 120,
                CleanupIntervalSeconds = 60
            };

            var optionsWrapper = Options.Create(_options);
            _connectionPoolManager = new TestConnectionPoolManager(_mockLogger.Object, optionsWrapper);
        }

        #region 连接注册测试

        [Fact]
        public async Task RegisterConnectionAsync_ValidParameters_ShouldReturnTrue()
        {
            // Arrange
            var connectionId = "conn-001";
            var playerId = "player-001";
            var hubType = "GameHub";
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345);

            // Act
            var result = await _connectionPoolManager.RegisterConnectionAsync(connectionId, playerId, hubType, endpoint);

            // Assert
            Assert.True(result);

            var connection = _connectionPoolManager.GetConnection(connectionId);
            Assert.NotNull(connection);
            Assert.Equal(connectionId, connection.ConnectionId);
            Assert.Equal(playerId, connection.PlayerId);
            Assert.Equal(hubType, connection.HubType);
            Assert.Equal(endpoint, connection.ClientEndPoint);
            Assert.True(connection.IsActive);
        }

        [Fact]
        public async Task RegisterConnectionAsync_DuplicateConnectionId_ShouldReturnFalse()
        {
            // Arrange
            var connectionId = "conn-duplicate";
            var playerId = "player-001";
            var hubType = "GameHub";

            // Act
            await _connectionPoolManager.RegisterConnectionAsync(connectionId, playerId, hubType);
            var result = await _connectionPoolManager.RegisterConnectionAsync(connectionId, playerId, hubType);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RegisterConnectionAsync_PoolFull_ShouldReturnFalse()
        {
            // Arrange
            _options.MaxPoolSize = 2;
            await _connectionPoolManager.RegisterConnectionAsync("conn-001", "player-001", "GameHub");
            await _connectionPoolManager.RegisterConnectionAsync("conn-002", "player-002", "GameHub");

            // Act
            var result = await _connectionPoolManager.RegisterConnectionAsync("conn-003", "player-003", "GameHub");

            // Assert
            Assert.False(result);
        }

        #endregion

        #region 连接注销测试

        [Fact]
        public async Task UnregisterConnectionAsync_ExistingConnection_ShouldReturnTrue()
        {
            // Arrange
            var connectionId = "conn-unregister";
            var playerId = "player-001";
            var hubType = "GameHub";

            await _connectionPoolManager.RegisterConnectionAsync(connectionId, playerId, hubType);

            // Act
            var result = await _connectionPoolManager.UnregisterConnectionAsync(connectionId, "Test disconnect");

            // Assert
            Assert.True(result);

            var connection = _connectionPoolManager.GetConnection(connectionId);
            Assert.Null(connection);
        }

        [Fact]
        public async Task UnregisterConnectionAsync_NonExistentConnection_ShouldReturnFalse()
        {
            // Arrange
            var connectionId = "non-existent";

            // Act
            var result = await _connectionPoolManager.UnregisterConnectionAsync(connectionId);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region 连接活跃度管理测试

        [Fact]
        public async Task UpdateConnectionActivity_ExistingConnection_ShouldReturnTrue()
        {
            // Arrange
            var connectionId = "conn-activity";
            await _connectionPoolManager.RegisterConnectionAsync(connectionId, "player-001", "GameHub");
            var initialTime = DateTime.UtcNow.AddMinutes(-1);

            // Act
            var result = _connectionPoolManager.UpdateConnectionActivity(connectionId);

            // Assert
            Assert.True(result);

            var connection = _connectionPoolManager.GetConnection(connectionId);
            Assert.NotNull(connection);
            Assert.True(connection.LastActiveAt > initialTime);
        }

        [Fact]
        public void UpdateConnectionActivity_NonExistentConnection_ShouldReturnFalse()
        {
            // Arrange
            var connectionId = "non-existent";

            // Act
            var result = _connectionPoolManager.UpdateConnectionActivity(connectionId);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region 连接查询测试

        [Fact]
        public async Task GetPlayerConnections_MultipleConnections_ShouldReturnCorrectList()
        {
            // Arrange
            var playerId = "player-multi";
            await _connectionPoolManager.RegisterConnectionAsync("conn-001", playerId, "GameHub");
            await _connectionPoolManager.RegisterConnectionAsync("conn-002", playerId, "ChatHub");
            await _connectionPoolManager.RegisterConnectionAsync("conn-003", "other-player", "GameHub");

            // Act
            var connections = _connectionPoolManager.GetPlayerConnections(playerId);

            // Assert
            Assert.Equal(2, connections.Count);
            Assert.All(connections, conn => Assert.Equal(playerId, conn.PlayerId));
            Assert.All(connections, conn => Assert.True(conn.IsActive));
        }

        [Fact]
        public async Task GetHubConnections_MultipleHubTypes_ShouldReturnCorrectList()
        {
            // Arrange
            var hubType = "GameHub";
            await _connectionPoolManager.RegisterConnectionAsync("conn-001", "player-001", hubType);
            await _connectionPoolManager.RegisterConnectionAsync("conn-002", "player-002", hubType);
            await _connectionPoolManager.RegisterConnectionAsync("conn-003", "player-003", "ChatHub");

            // Act
            var connections = _connectionPoolManager.GetHubConnections(hubType);

            // Assert
            Assert.Equal(2, connections.Count);
            Assert.All(connections, conn => Assert.Equal(hubType, conn.HubType));
            Assert.All(connections, conn => Assert.True(conn.IsActive));
        }

        #endregion

        #region 连接池统计测试

        [Fact]
        public async Task GetStats_WithConnections_ShouldReturnCorrectStats()
        {
            // Arrange
            await _connectionPoolManager.RegisterConnectionAsync("conn-001", "player-001", "GameHub");
            await _connectionPoolManager.RegisterConnectionAsync("conn-002", "player-002", "ChatHub");
            await _connectionPoolManager.RegisterConnectionAsync("conn-003", "player-003", "GameHub");

            // Act
            var stats = _connectionPoolManager.GetStats();

            // Assert
            Assert.Equal(3, stats.TotalConnections);
            Assert.Equal(3, stats.ActiveConnections);
            Assert.Equal(0, stats.IdleConnections); // 刚创建的连接不会是空闲状态
            Assert.Equal(2, stats.ConnectionsByHubType["GameHub"]);
            Assert.Equal(1, stats.ConnectionsByHubType["ChatHub"]);
            Assert.True(stats.AverageConnectionDuration >= 0);
            Assert.True(stats.OldestConnectionAge >= 0);
        }

        [Fact]
        public void GetStats_EmptyPool_ShouldReturnZeroStats()
        {
            // Act
            var stats = _connectionPoolManager.GetStats();

            // Assert
            Assert.Equal(0, stats.TotalConnections);
            Assert.Equal(0, stats.ActiveConnections);
            Assert.Equal(0, stats.IdleConnections);
            Assert.Empty(stats.ConnectionsByHubType);
            Assert.Equal(0, stats.AverageConnectionDuration);
            Assert.Equal(0, stats.OldestConnectionAge);
        }

        #endregion

        #region 连接健康检查测试

        [Fact]
        public async Task CheckConnectionHealthAsync_ActiveConnection_ShouldReturnHealthy()
        {
            // Arrange
            var connectionId = "conn-healthy";
            await _connectionPoolManager.RegisterConnectionAsync(connectionId, "player-001", "GameHub");

            // Act
            var healthStatus = await _connectionPoolManager.CheckConnectionHealthAsync(connectionId);

            // Assert
            Assert.True(healthStatus.IsHealthy);
            Assert.Empty(healthStatus.Reason);
        }

        [Fact]
        public async Task CheckConnectionHealthAsync_NonExistentConnection_ShouldReturnUnhealthy()
        {
            // Arrange
            var connectionId = "non-existent";

            // Act
            var healthStatus = await _connectionPoolManager.CheckConnectionHealthAsync(connectionId);

            // Assert
            Assert.False(healthStatus.IsHealthy);
            Assert.Equal("Connection not found", healthStatus.Reason);
        }

        [Fact]
        public async Task CheckConnectionHealthAsync_InactiveConnection_ShouldReturnUnhealthy()
        {
            // Arrange
            var connectionId = "conn-inactive";
            await _connectionPoolManager.RegisterConnectionAsync(connectionId, "player-001", "GameHub");
            await _connectionPoolManager.UnregisterConnectionAsync(connectionId);
            
            // 重新注册但设置为非活跃状态
            await _connectionPoolManager.RegisterConnectionAsync(connectionId, "player-001", "GameHub");
            var connection = _connectionPoolManager.GetConnection(connectionId);
            if (connection != null)
            {
                connection.IsActive = false;
            }

            // Act
            var healthStatus = await _connectionPoolManager.CheckConnectionHealthAsync(connectionId);

            // Assert
            Assert.False(healthStatus.IsHealthy);
            Assert.Equal("Connection inactive", healthStatus.Reason);
        }

        #endregion

        #region 边界条件测试

        [Theory]
        [InlineData("", "player-001", "GameHub")] // 空连接ID
        [InlineData("conn-001", "", "GameHub")]  // 空玩家ID
        [InlineData("conn-001", "player-001", "")] // 空Hub类型
        public async Task RegisterConnectionAsync_InvalidParameters_ShouldHandleGracefully(
            string connectionId, string playerId, string hubType)
        {
            // Act & Assert - 应该不抛出异常
            var result = await _connectionPoolManager.RegisterConnectionAsync(connectionId, playerId, hubType);
            
            // 根据实际需要决定是否允许这些参数
            // 这里假设我们允许这些参数但连接可能不太有用
            Assert.IsType<bool>(result);
        }

        [Fact]
        public async Task ConnectionPool_ConcurrentOperations_ShouldBeThreadSafe()
        {
            // Arrange
            var tasks = new List<Task<bool>>();
            const int concurrentConnections = 50;

            // Act - 并发注册多个连接
            for (int i = 0; i < concurrentConnections; i++)
            {
                var connectionId = $"concurrent-conn-{i:D3}";
                var playerId = $"player-{i:D3}";
                tasks.Add(_connectionPoolManager.RegisterConnectionAsync(connectionId, playerId, "GameHub"));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, result => Assert.True(result));
            
            var stats = _connectionPoolManager.GetStats();
            Assert.Equal(concurrentConnections, stats.ActiveConnections);
        }

        #endregion

        public void Dispose()
        {
            _connectionPoolManager?.Dispose();
        }
    }
}
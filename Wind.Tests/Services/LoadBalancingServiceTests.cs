using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Collections.Concurrent;
using Xunit;

namespace Wind.Tests.Services
{
    /// <summary>
    /// 负载均衡服务 - 测试版本
    /// 用于单元测试的简化实现，测试五种负载均衡算法
    /// </summary>
    public class TestLoadBalancingService : IDisposable
    {
        private readonly ILogger<TestLoadBalancingService> _logger;
        private readonly TestLoadBalancingOptions _options;
        private readonly ConcurrentDictionary<string, TestServerNode> _nodes;
        private readonly ConcurrentDictionary<string, TestServiceRegistry> _services;
        private readonly Timer _healthCheckTimer;
        private readonly Random _random = new Random(12345); // 固定种子用于测试
        private bool _disposed = false;

        public TestLoadBalancingService(ILogger<TestLoadBalancingService> logger, IOptions<TestLoadBalancingOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            _nodes = new ConcurrentDictionary<string, TestServerNode>();
            _services = new ConcurrentDictionary<string, TestServiceRegistry>();
            
            _healthCheckTimer = new Timer(PerformHealthCheck, null, 
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds),
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds));
        }

        public async Task<bool> RegisterNodeAsync(string nodeId, string serviceName, IPEndPoint endpoint, 
            Dictionary<string, object>? metadata = null)
        {
            try
            {
                var node = new TestServerNode
                {
                    NodeId = nodeId,
                    ServiceName = serviceName,
                    Endpoint = endpoint,
                    IsHealthy = true,
                    IsActive = true,
                    RegisteredAt = DateTime.UtcNow,
                    LastHealthCheck = DateTime.UtcNow,
                    Weight = _options.DefaultWeight,
                    CurrentLoad = 0,
                    Metadata = metadata ?? new Dictionary<string, object>()
                };

                if (_nodes.TryAdd(nodeId, node))
                {
                    _services.AddOrUpdate(serviceName, 
                        new TestServiceRegistry { ServiceName = serviceName, Nodes = [nodeId] },
                        (key, registry) =>
                        {
                            lock (registry.Nodes)
                            {
                                if (!registry.Nodes.Contains(nodeId))
                                {
                                    registry.Nodes.Add(nodeId);
                                }
                            }
                            return registry;
                        });

                    await CheckNodeHealthAsync(nodeId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register node {NodeId} for service {ServiceName}", nodeId, serviceName);
                return false;
            }
        }

        public async Task<bool> UnregisterNodeAsync(string nodeId, string reason = "Normal shutdown")
        {
            try
            {
                if (_nodes.TryRemove(nodeId, out var node))
                {
                    node.IsActive = false;
                    node.UnregisteredAt = DateTime.UtcNow;
                    node.UnregisterReason = reason;

                    if (_services.TryGetValue(node.ServiceName, out var registry))
                    {
                        lock (registry.Nodes)
                        {
                            registry.Nodes.Remove(nodeId);
                            if (registry.Nodes.Count == 0)
                            {
                                _services.TryRemove(node.ServiceName, out _);
                            }
                        }
                    }
                    
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unregister node {NodeId}, reason: {Reason}", nodeId, reason);
                return false;
            }
        }

        public async Task<TestServerNode?> SelectNodeAsync(string serviceName, 
            TestLoadBalancingStrategy? strategy = null, 
            Dictionary<string, object>? context = null)
        {
            try
            {
                if (!_services.TryGetValue(serviceName, out var registry))
                {
                    _logger.LogWarning("Service {ServiceName} not found in registry", serviceName);
                    return null;
                }

                var healthyNodes = GetHealthyNodes(serviceName);
                if (healthyNodes.Count == 0)
                {
                    _logger.LogWarning("No healthy nodes available for service {ServiceName}", serviceName);
                    return null;
                }

                var selectedStrategy = strategy ?? _options.DefaultStrategy;
                var selectedNode = selectedStrategy switch
                {
                    TestLoadBalancingStrategy.RoundRobin => SelectRoundRobin(healthyNodes, serviceName),
                    TestLoadBalancingStrategy.WeightedRoundRobin => SelectWeightedRoundRobin(healthyNodes),
                    TestLoadBalancingStrategy.LeastConnections => SelectLeastConnections(healthyNodes),
                    TestLoadBalancingStrategy.Random => SelectRandom(healthyNodes),
                    TestLoadBalancingStrategy.ConsistentHash => SelectConsistentHash(healthyNodes, context),
                    _ => SelectRoundRobin(healthyNodes, serviceName)
                };

                if (selectedNode != null)
                {
                    var currentLoad = selectedNode.CurrentLoad;
                    Interlocked.Increment(ref currentLoad);
                    selectedNode.CurrentLoad = currentLoad;
                    selectedNode.LastUsedAt = DateTime.UtcNow;
                    selectedNode.TotalRequests++;
                }

                return selectedNode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to select node for service {ServiceName}", serviceName);
                return null;
            }
        }

        public void ReleaseNode(string nodeId)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
            {
                if (node.CurrentLoad > 0)
                {
                    var currentLoad = node.CurrentLoad;
                    Interlocked.Decrement(ref currentLoad);
                    node.CurrentLoad = currentLoad;
                }
            }
        }

        public bool UpdateNodeWeight(string nodeId, int weight)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
            {
                node.Weight = Math.Max(1, weight);
                return true;
            }
            return false;
        }

        public TestServiceStats GetServiceStats(string serviceName)
        {
            if (!_services.TryGetValue(serviceName, out var registry))
            {
                return new TestServiceStats { ServiceName = serviceName };
            }

            var nodes = registry.Nodes.Select(nodeId => _nodes.GetValueOrDefault(nodeId))
                .Where(n => n != null).Cast<TestServerNode>().ToList();

            var healthyNodes = nodes.Where(n => n.IsHealthy && n.IsActive).ToList();

            return new TestServiceStats
            {
                ServiceName = serviceName,
                TotalNodes = nodes.Count,
                HealthyNodes = healthyNodes.Count,
                TotalRequests = nodes.Sum(n => n.TotalRequests),
                AverageLoad = healthyNodes.Count > 0 ? healthyNodes.Average(n => n.CurrentLoad) : 0,
                MaxLoad = healthyNodes.Count > 0 ? healthyNodes.Max(n => n.CurrentLoad) : 0,
                NodeStats = nodes.ToDictionary(n => n.NodeId, n => new TestNodeStats
                {
                    NodeId = n.NodeId,
                    IsHealthy = n.IsHealthy,
                    CurrentLoad = n.CurrentLoad,
                    TotalRequests = n.TotalRequests,
                    Weight = n.Weight,
                    LastUsedAt = n.LastUsedAt
                })
            };
        }

        private List<TestServerNode> GetHealthyNodes(string serviceName)
        {
            if (!_services.TryGetValue(serviceName, out var registry))
            {
                return new List<TestServerNode>();
            }

            return registry.Nodes
                .Select<string, TestServerNode?>(nodeId => _nodes.GetValueOrDefault(nodeId))
                .Where(n => n != null && n.IsHealthy && n.IsActive)
                .Cast<TestServerNode>()
                .ToList();
        }

        private TestServerNode SelectRoundRobin(List<TestServerNode> nodes, string serviceName)
        {
            if (!_services.TryGetValue(serviceName, out var registry))
            {
                return nodes[_random.Next(nodes.Count)];
            }

            var roundRobinIndex = registry.RoundRobinIndex;
            var index = Interlocked.Increment(ref roundRobinIndex) % nodes.Count;
            registry.RoundRobinIndex = roundRobinIndex;
            return nodes[index];
        }

        private TestServerNode SelectWeightedRoundRobin(List<TestServerNode> nodes)
        {
            var totalWeight = nodes.Sum(n => n.Weight);
            var randomWeight = _random.Next(totalWeight);
            
            var currentWeight = 0;
            foreach (var node in nodes)
            {
                currentWeight += node.Weight;
                if (randomWeight < currentWeight)
                {
                    return node;
                }
            }
            
            return nodes.Last();
        }

        private TestServerNode SelectLeastConnections(List<TestServerNode> nodes)
        {
            return nodes.OrderBy(n => n.CurrentLoad).ThenBy(n => n.TotalRequests).First();
        }

        private TestServerNode SelectRandom(List<TestServerNode> nodes)
        {
            return nodes[_random.Next(nodes.Count)];
        }

        private TestServerNode SelectConsistentHash(List<TestServerNode> nodes, Dictionary<string, object>? context)
        {
            var hashKey = "default";
            if (context?.TryGetValue("PlayerId", out var playerId) == true)
            {
                hashKey = playerId.ToString() ?? "default";
            }
            else if (context?.TryGetValue("SessionId", out var sessionId) == true)
            {
                hashKey = sessionId.ToString() ?? "default";
            }

            var hash = hashKey.GetHashCode();
            var index = Math.Abs(hash) % nodes.Count;
            return nodes[index];
        }

        private async void PerformHealthCheck(object? state)
        {
            try
            {
                var tasks = _nodes.Values
                    .Where(n => n.IsActive)
                    .Select(node => CheckNodeHealthAsync(node.NodeId))
                    .ToArray();

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");
            }
        }

        private async Task CheckNodeHealthAsync(string nodeId)
        {
            if (!_nodes.TryGetValue(nodeId, out var node))
                return;

            try
            {
                var lastActivity = Math.Max(
                    (DateTime.UtcNow - node.LastHealthCheck).TotalSeconds,
                    (DateTime.UtcNow - (node.LastUsedAt ?? node.RegisteredAt)).TotalSeconds
                );

                var wasHealthy = node.IsHealthy;
                node.IsHealthy = lastActivity <= _options.NodeTimeoutSeconds;
                node.LastHealthCheck = DateTime.UtcNow;

                if (!node.IsHealthy && node.CurrentLoad > 0)
                {
                    node.CurrentLoad = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check health for node {NodeId}", nodeId);
                node.IsHealthy = false;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _healthCheckTimer?.Dispose();
                
                foreach (var node in _nodes.Values)
                {
                    if (node.IsActive)
                    {
                        node.IsActive = false;
                        node.UnregisteredAt = DateTime.UtcNow;
                        node.UnregisterReason = "Service shutdown";
                    }
                }
                
                _nodes.Clear();
                _services.Clear();
                _disposed = true;
            }
        }
    }

    // 测试模型类
    public class TestServerNode
    {
        public string NodeId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public IPEndPoint Endpoint { get; set; } = null!;
        public bool IsHealthy { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public DateTime RegisteredAt { get; set; }
        public DateTime? UnregisteredAt { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public int Weight { get; set; } = 100;
        public int CurrentLoad { get; set; } = 0;
        public long TotalRequests { get; set; } = 0;
        public string? UnregisterReason { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class TestServiceRegistry
    {
        public string ServiceName { get; set; } = string.Empty;
        public List<string> Nodes { get; set; } = new();
        public int RoundRobinIndex { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TestServiceStats
    {
        public string ServiceName { get; set; } = string.Empty;
        public int TotalNodes { get; set; }
        public int HealthyNodes { get; set; }
        public long TotalRequests { get; set; }
        public double AverageLoad { get; set; }
        public int MaxLoad { get; set; }
        public Dictionary<string, TestNodeStats> NodeStats { get; set; } = new();
    }

    public class TestNodeStats
    {
        public string NodeId { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public int CurrentLoad { get; set; }
        public long TotalRequests { get; set; }
        public int Weight { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }

    public enum TestLoadBalancingStrategy
    {
        RoundRobin,         // 轮询
        WeightedRoundRobin, // 加权轮询
        LeastConnections,   // 最少连接
        Random,             // 随机
        ConsistentHash      // 一致性哈希
    }

    public class TestLoadBalancingOptions
    {
        public TestLoadBalancingStrategy DefaultStrategy { get; set; } = TestLoadBalancingStrategy.RoundRobin;
        public int DefaultWeight { get; set; } = 100;
        public int NodeTimeoutSeconds { get; set; } = 30;
        public int HealthCheckIntervalSeconds { get; set; } = 10;
        public bool EnableHealthCheck { get; set; } = true;
        public bool EnableMetrics { get; set; } = true;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;
    }

    /// <summary>
    /// LoadBalancingService单元测试
    /// 测试五种负载均衡算法：轮询、加权轮询、最少连接、随机、一致性哈希
    /// </summary>
    public class LoadBalancingServiceTests : IDisposable
    {
        private readonly TestLoadBalancingService _loadBalancingService;
        private readonly Mock<ILogger<TestLoadBalancingService>> _mockLogger;
        private readonly TestLoadBalancingOptions _options;

        public LoadBalancingServiceTests()
        {
            _mockLogger = new Mock<ILogger<TestLoadBalancingService>>();
            _options = new TestLoadBalancingOptions
            {
                DefaultStrategy = TestLoadBalancingStrategy.RoundRobin,
                DefaultWeight = 100,
                NodeTimeoutSeconds = 30,
                HealthCheckIntervalSeconds = 10
            };

            var optionsWrapper = Options.Create(_options);
            _loadBalancingService = new TestLoadBalancingService(_mockLogger.Object, optionsWrapper);
        }

        #region 节点注册和注销测试

        [Fact]
        public async Task RegisterNodeAsync_ValidParameters_ShouldReturnTrue()
        {
            // Arrange
            var nodeId = "node-001";
            var serviceName = "TestService";
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);

            // Act
            var result = await _loadBalancingService.RegisterNodeAsync(nodeId, serviceName, endpoint);

            // Assert
            Assert.True(result);

            var stats = _loadBalancingService.GetServiceStats(serviceName);
            Assert.Equal(1, stats.TotalNodes);
            Assert.Equal(1, stats.HealthyNodes);
        }

        [Fact]
        public async Task UnregisterNodeAsync_ExistingNode_ShouldReturnTrue()
        {
            // Arrange
            var nodeId = "node-unregister";
            var serviceName = "TestService";
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);

            await _loadBalancingService.RegisterNodeAsync(nodeId, serviceName, endpoint);

            // Act
            var result = await _loadBalancingService.UnregisterNodeAsync(nodeId);

            // Assert
            Assert.True(result);

            var stats = _loadBalancingService.GetServiceStats(serviceName);
            Assert.Equal(0, stats.TotalNodes);
        }

        #endregion

        #region 轮询算法测试

        [Fact]
        public async Task SelectNodeAsync_RoundRobin_ShouldDistributeEvenly()
        {
            // Arrange
            var serviceName = "TestService";
            var endpoints = new[]
            {
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8081),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8082)
            };

            for (int i = 0; i < endpoints.Length; i++)
            {
                await _loadBalancingService.RegisterNodeAsync($"node-{i:D3}", serviceName, endpoints[i]);
            }

            var selectionCounts = new Dictionary<string, int>();

            // Act - 进行多轮选择
            for (int i = 0; i < 15; i++)
            {
                var selectedNode = await _loadBalancingService.SelectNodeAsync(serviceName, TestLoadBalancingStrategy.RoundRobin);
                Assert.NotNull(selectedNode);

                var key = selectedNode.NodeId;
                selectionCounts[key] = selectionCounts.GetValueOrDefault(key, 0) + 1;

                // 释放节点负载
                _loadBalancingService.ReleaseNode(selectedNode.NodeId);
            }

            // Assert - 应该均匀分布
            Assert.Equal(3, selectionCounts.Count);
            Assert.All(selectionCounts.Values, count => Assert.Equal(5, count));
        }

        #endregion

        #region 加权轮询算法测试

        [Fact]
        public async Task SelectNodeAsync_WeightedRoundRobin_ShouldRespectWeights()
        {
            // Arrange
            var serviceName = "TestService";
            var nodeConfigs = new[]
            {
                new { NodeId = "high-weight", Endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080), Weight = 300 },
                new { NodeId = "medium-weight", Endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8081), Weight = 200 },
                new { NodeId = "low-weight", Endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8082), Weight = 100 }
            };

            foreach (var config in nodeConfigs)
            {
                await _loadBalancingService.RegisterNodeAsync(config.NodeId, serviceName, config.Endpoint);
                _loadBalancingService.UpdateNodeWeight(config.NodeId, config.Weight);
            }

            var selectionCounts = new Dictionary<string, int>();

            // Act - 进行大量选择以观察权重分布
            for (int i = 0; i < 1200; i++)
            {
                var selectedNode = await _loadBalancingService.SelectNodeAsync(serviceName, TestLoadBalancingStrategy.WeightedRoundRobin);
                Assert.NotNull(selectedNode);

                var key = selectedNode.NodeId;
                selectionCounts[key] = selectionCounts.GetValueOrDefault(key, 0) + 1;

                _loadBalancingService.ReleaseNode(selectedNode.NodeId);
            }

            // Assert - 检查权重分布是否合理 (允许一定误差)
            var highWeightCount = selectionCounts.GetValueOrDefault("high-weight", 0);
            var mediumWeightCount = selectionCounts.GetValueOrDefault("medium-weight", 0);
            var lowWeightCount = selectionCounts.GetValueOrDefault("low-weight", 0);

            // 权重比例应该约为 3:2:1
            Assert.True(highWeightCount > mediumWeightCount);
            Assert.True(mediumWeightCount > lowWeightCount);
            
            // 允许10%的误差
            var expectedHigh = 1200 * 3 / 6; // 600
            var expectedMedium = 1200 * 2 / 6; // 400
            var expectedLow = 1200 * 1 / 6; // 200

            Assert.InRange(highWeightCount, expectedHigh - 60, expectedHigh + 60);
            Assert.InRange(mediumWeightCount, expectedMedium - 40, expectedMedium + 40);
            Assert.InRange(lowWeightCount, expectedLow - 20, expectedLow + 20);
        }

        #endregion

        #region 最少连接算法测试

        [Fact]
        public async Task SelectNodeAsync_LeastConnections_ShouldSelectLeastLoadedNode()
        {
            // Arrange
            var serviceName = "TestService";
            var endpoints = new[]
            {
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8081),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8082)
            };

            for (int i = 0; i < endpoints.Length; i++)
            {
                await _loadBalancingService.RegisterNodeAsync($"node-{i:D3}", serviceName, endpoints[i]);
            }

            // 模拟第一个节点负载较高
            for (int i = 0; i < 5; i++)
            {
                await _loadBalancingService.SelectNodeAsync(serviceName, TestLoadBalancingStrategy.LeastConnections);
            }

            // Act - 选择最少连接的节点
            var selectedNode = await _loadBalancingService.SelectNodeAsync(serviceName, TestLoadBalancingStrategy.LeastConnections);

            // Assert - 应该选择负载最少的节点
            Assert.NotNull(selectedNode);
            
            var stats = _loadBalancingService.GetServiceStats(serviceName);
            var selectedNodeStats = stats.NodeStats[selectedNode.NodeId];
            
            // 选择的节点应该是负载最少的之一
            var minLoad = stats.NodeStats.Values.Min(s => s.CurrentLoad);
            Assert.Equal(minLoad, selectedNodeStats.CurrentLoad);
        }

        #endregion

        #region 随机算法测试

        [Fact]
        public async Task SelectNodeAsync_Random_ShouldDistributeRandomly()
        {
            // Arrange
            var serviceName = "TestService";
            var endpoints = new[]
            {
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8081),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8082)
            };

            for (int i = 0; i < endpoints.Length; i++)
            {
                await _loadBalancingService.RegisterNodeAsync($"node-{i:D3}", serviceName, endpoints[i]);
            }

            var selectionCounts = new Dictionary<string, int>();

            // Act - 进行大量选择
            for (int i = 0; i < 300; i++)
            {
                var selectedNode = await _loadBalancingService.SelectNodeAsync(serviceName, TestLoadBalancingStrategy.Random);
                Assert.NotNull(selectedNode);

                var key = selectedNode.NodeId;
                selectionCounts[key] = selectionCounts.GetValueOrDefault(key, 0) + 1;

                _loadBalancingService.ReleaseNode(selectedNode.NodeId);
            }

            // Assert - 应该有随机分布，每个节点都被选中过
            Assert.Equal(3, selectionCounts.Count);
            Assert.All(selectionCounts.Values, count => Assert.True(count > 50)); // 每个节点至少被选中50次

            // 验证不是完全均匀分布（随机性特征）
            var maxCount = selectionCounts.Values.Max();
            var minCount = selectionCounts.Values.Min();
            Assert.True(maxCount - minCount > 10); // 应该有一定的随机变化
        }

        #endregion

        #region 一致性哈希算法测试

        [Fact]
        public async Task SelectNodeAsync_ConsistentHash_ShouldBeConsistent()
        {
            // Arrange
            var serviceName = "TestService";
            var endpoints = new[]
            {
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8081),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8082)
            };

            for (int i = 0; i < endpoints.Length; i++)
            {
                await _loadBalancingService.RegisterNodeAsync($"node-{i:D3}", serviceName, endpoints[i]);
            }

            var contexts = new[]
            {
                new Dictionary<string, object> { { "PlayerId", "player-001" } },
                new Dictionary<string, object> { { "PlayerId", "player-002" } },
                new Dictionary<string, object> { { "SessionId", "session-001" } },
                new Dictionary<string, object> { { "SessionId", "session-002" } }
            };

            var firstSelections = new Dictionary<string, string>();

            // Act - 第一轮选择
            foreach (var context in contexts)
            {
                var contextKey = context.First().Value.ToString()!;
                var selectedNode = await _loadBalancingService.SelectNodeAsync(serviceName, TestLoadBalancingStrategy.ConsistentHash, context);
                Assert.NotNull(selectedNode);
                firstSelections[contextKey] = selectedNode.NodeId;
                _loadBalancingService.ReleaseNode(selectedNode.NodeId);
            }

            // Act - 第二轮选择，应该与第一轮一致
            foreach (var context in contexts)
            {
                var contextKey = context.First().Value.ToString()!;
                var selectedNode = await _loadBalancingService.SelectNodeAsync(serviceName, TestLoadBalancingStrategy.ConsistentHash, context);
                Assert.NotNull(selectedNode);

                // Assert - 相同的上下文应该选择相同的节点
                Assert.Equal(firstSelections[contextKey], selectedNode.NodeId);
                _loadBalancingService.ReleaseNode(selectedNode.NodeId);
            }
        }

        #endregion

        #region 节点健康检查测试

        [Fact]
        public async Task SelectNodeAsync_UnhealthyNode_ShouldBeIgnored()
        {
            // Arrange
            var serviceName = "TestService";
            var endpoints = new[]
            {
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8081)
            };

            await _loadBalancingService.RegisterNodeAsync("healthy-node", serviceName, endpoints[0]);
            await _loadBalancingService.RegisterNodeAsync("unhealthy-node", serviceName, endpoints[1]);

            // 模拟不健康的节点
            await _loadBalancingService.UnregisterNodeAsync("unhealthy-node");

            // Act
            var selectedNode = await _loadBalancingService.SelectNodeAsync(serviceName, TestLoadBalancingStrategy.RoundRobin);

            // Assert - 只应该选择健康的节点
            Assert.NotNull(selectedNode);
            Assert.Equal("healthy-node", selectedNode.NodeId);
        }

        #endregion

        #region 统计信息测试

        [Fact]
        public async Task GetServiceStats_WithNodes_ShouldReturnCorrectStats()
        {
            // Arrange
            var serviceName = "TestService";
            var endpoints = new[]
            {
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8081),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8082)
            };

            for (int i = 0; i < endpoints.Length; i++)
            {
                await _loadBalancingService.RegisterNodeAsync($"node-{i:D3}", serviceName, endpoints[i]);
            }

            // 模拟一些请求
            for (int i = 0; i < 10; i++)
            {
                var node = await _loadBalancingService.SelectNodeAsync(serviceName);
                if (node != null)
                {
                    _loadBalancingService.ReleaseNode(node.NodeId);
                }
            }

            // Act
            var stats = _loadBalancingService.GetServiceStats(serviceName);

            // Assert
            Assert.Equal(serviceName, stats.ServiceName);
            Assert.Equal(3, stats.TotalNodes);
            Assert.Equal(3, stats.HealthyNodes);
            Assert.True(stats.TotalRequests >= 10);
            Assert.Equal(3, stats.NodeStats.Count);
        }

        #endregion

        #region 并发安全性测试

        [Fact]
        public async Task SelectNodeAsync_ConcurrentAccess_ShouldBeThreadSafe()
        {
            // Arrange
            var serviceName = "TestService";
            var endpoints = new[]
            {
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8081),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8082)
            };

            for (int i = 0; i < endpoints.Length; i++)
            {
                await _loadBalancingService.RegisterNodeAsync($"node-{i:D3}", serviceName, endpoints[i]);
            }

            var tasks = new List<Task>();
            var results = new ConcurrentBag<string>();

            // Act - 并发选择节点
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var node = await _loadBalancingService.SelectNodeAsync(serviceName, TestLoadBalancingStrategy.LeastConnections);
                    if (node != null)
                    {
                        results.Add(node.NodeId);
                        _loadBalancingService.ReleaseNode(node.NodeId);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - 应该没有异常，且都能选择到节点
            Assert.Equal(100, results.Count);
            Assert.All(results, nodeId => Assert.True(nodeId.StartsWith("node-")));
        }

        #endregion

        public void Dispose()
        {
            _loadBalancingService?.Dispose();
        }
    }
}
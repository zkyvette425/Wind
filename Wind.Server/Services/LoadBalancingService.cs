using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;

namespace Wind.Server.Services
{
    /// <summary>
    /// 负载均衡服务 - 管理多节点间的请求分发和故障转移
    /// </summary>
    public class LoadBalancingService : IDisposable
    {
        private readonly ILogger<LoadBalancingService> _logger;
        private readonly LoadBalancingOptions _options;
        private readonly ConcurrentDictionary<string, ServerNode> _nodes;
        private readonly ConcurrentDictionary<string, ServiceRegistry> _services;
        private readonly Timer _healthCheckTimer;
        private readonly object _lock = new object();
        private bool _disposed = false;

        public LoadBalancingService(ILogger<LoadBalancingService> logger, IOptions<LoadBalancingOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            _nodes = new ConcurrentDictionary<string, ServerNode>();
            _services = new ConcurrentDictionary<string, ServiceRegistry>();
            
            // 启动定期健康检查
            _healthCheckTimer = new Timer(PerformHealthCheck, null, 
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds),
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds));

            _logger.LogInformation("LoadBalancingService initialized with Strategy={Strategy}, HealthCheck={HealthCheckInterval}s",
                _options.DefaultStrategy, _options.HealthCheckIntervalSeconds);
        }

        /// <summary>
        /// 注册服务节点
        /// </summary>
        public async Task<bool> RegisterNodeAsync(string nodeId, string serviceName, IPEndPoint endpoint, 
            Dictionary<string, object>? metadata = null)
        {
            try
            {
                var node = new ServerNode
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

                // 注册节点
                if (_nodes.TryAdd(nodeId, node))
                {
                    // 添加到服务注册表
                    _services.AddOrUpdate(serviceName, 
                        new ServiceRegistry { ServiceName = serviceName, Nodes = [nodeId] },
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

                    _logger.LogInformation("Node registered: {NodeId} for Service={ServiceName}, Endpoint={Endpoint}",
                        nodeId, serviceName, endpoint);

                    // 执行初始健康检查
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

        /// <summary>
        /// 注销服务节点
        /// </summary>
        public async Task<bool> UnregisterNodeAsync(string nodeId, string reason = "Normal shutdown")
        {
            try
            {
                if (_nodes.TryRemove(nodeId, out var node))
                {
                    node.IsActive = false;
                    node.UnregisteredAt = DateTime.UtcNow;
                    node.UnregisterReason = reason;

                    // 从服务注册表中移除
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

                    _logger.LogInformation("Node unregistered: {NodeId} for Service={ServiceName}, Reason={Reason}",
                        nodeId, node.ServiceName, reason);
                    
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

        /// <summary>
        /// 选择服务节点
        /// </summary>
        public async Task<ServerNode?> SelectNodeAsync(string serviceName, 
            LoadBalancingStrategy? strategy = null, 
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
                    LoadBalancingStrategy.RoundRobin => SelectRoundRobin(healthyNodes, serviceName),
                    LoadBalancingStrategy.WeightedRoundRobin => SelectWeightedRoundRobin(healthyNodes),
                    LoadBalancingStrategy.LeastConnections => SelectLeastConnections(healthyNodes),
                    LoadBalancingStrategy.Random => SelectRandom(healthyNodes),
                    LoadBalancingStrategy.ConsistentHash => SelectConsistentHash(healthyNodes, context),
                    _ => SelectRoundRobin(healthyNodes, serviceName)
                };

                if (selectedNode != null)
                {
                    // 更新负载计数
                    var currentLoad = selectedNode.CurrentLoad;
                    Interlocked.Increment(ref currentLoad);
                    selectedNode.CurrentLoad = currentLoad;
                    selectedNode.LastUsedAt = DateTime.UtcNow;
                    selectedNode.TotalRequests++;

                    _logger.LogDebug("Selected node {NodeId} for service {ServiceName} using {Strategy}",
                        selectedNode.NodeId, serviceName, selectedStrategy);
                }

                return selectedNode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to select node for service {ServiceName}", serviceName);
                return null;
            }
        }

        /// <summary>
        /// 释放节点负载
        /// </summary>
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

        /// <summary>
        /// 更新节点权重
        /// </summary>
        public bool UpdateNodeWeight(string nodeId, int weight)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
            {
                node.Weight = Math.Max(1, weight); // 权重最小为1
                _logger.LogInformation("Updated weight for node {NodeId} to {Weight}", nodeId, weight);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取服务统计信息
        /// </summary>
        public ServiceStats GetServiceStats(string serviceName)
        {
            if (!_services.TryGetValue(serviceName, out var registry))
            {
                return new ServiceStats { ServiceName = serviceName };
            }

            var nodes = registry.Nodes.Select(nodeId => _nodes.GetValueOrDefault(nodeId))
                .Where(n => n != null).Cast<ServerNode>().ToList();

            var healthyNodes = nodes.Where(n => n.IsHealthy && n.IsActive).ToList();

            return new ServiceStats
            {
                ServiceName = serviceName,
                TotalNodes = nodes.Count,
                HealthyNodes = healthyNodes.Count,
                TotalRequests = nodes.Sum(n => n.TotalRequests),
                AverageLoad = healthyNodes.Count > 0 ? healthyNodes.Average(n => n.CurrentLoad) : 0,
                MaxLoad = healthyNodes.Count > 0 ? healthyNodes.Max(n => n.CurrentLoad) : 0,
                NodeStats = nodes.ToDictionary(n => n.NodeId, n => new NodeStats
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

        /// <summary>
        /// 获取健康节点列表
        /// </summary>
        private List<ServerNode> GetHealthyNodes(string serviceName)
        {
            if (!_services.TryGetValue(serviceName, out var registry))
            {
                return new List<ServerNode>();
            }

            return registry.Nodes
                .Select<string, ServerNode?>(nodeId => _nodes.GetValueOrDefault(nodeId))
                .Where(n => n != null && n.IsHealthy && n.IsActive)
                .Cast<ServerNode>()
                .ToList();
        }

        /// <summary>
        /// 轮询选择策略
        /// </summary>
        private ServerNode SelectRoundRobin(List<ServerNode> nodes, string serviceName)
        {
            if (!_services.TryGetValue(serviceName, out var registry))
            {
                return nodes[Random.Shared.Next(nodes.Count)];
            }

            var roundRobinIndex = registry.RoundRobinIndex;
            var index = Interlocked.Increment(ref roundRobinIndex) % nodes.Count;
            registry.RoundRobinIndex = roundRobinIndex;
            return nodes[index];
        }

        /// <summary>
        /// 加权轮询选择策略
        /// </summary>
        private ServerNode SelectWeightedRoundRobin(List<ServerNode> nodes)
        {
            var totalWeight = nodes.Sum(n => n.Weight);
            var randomWeight = Random.Shared.Next(totalWeight);
            
            var currentWeight = 0;
            foreach (var node in nodes)
            {
                currentWeight += node.Weight;
                if (randomWeight < currentWeight)
                {
                    return node;
                }
            }
            
            return nodes.Last(); // 回退到最后一个节点
        }

        /// <summary>
        /// 最少连接选择策略
        /// </summary>
        private ServerNode SelectLeastConnections(List<ServerNode> nodes)
        {
            return nodes.OrderBy(n => n.CurrentLoad).ThenBy(n => n.TotalRequests).First();
        }

        /// <summary>
        /// 随机选择策略
        /// </summary>
        private ServerNode SelectRandom(List<ServerNode> nodes)
        {
            return nodes[Random.Shared.Next(nodes.Count)];
        }

        /// <summary>
        /// 一致性哈希选择策略
        /// </summary>
        private ServerNode SelectConsistentHash(List<ServerNode> nodes, Dictionary<string, object>? context)
        {
            // 简化版一致性哈希，基于客户端标识符
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

        /// <summary>
        /// 执行健康检查
        /// </summary>
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

        /// <summary>
        /// 检查单个节点健康状态
        /// </summary>
        private async Task CheckNodeHealthAsync(string nodeId)
        {
            if (!_nodes.TryGetValue(nodeId, out var node))
                return;

            try
            {
                // 这里可以实现实际的健康检查逻辑，比如发送HTTP请求、TCP连接测试等
                // 暂时使用简单的超时检查
                var lastActivity = Math.Max(
                    (DateTime.UtcNow - node.LastHealthCheck).TotalSeconds,
                    (DateTime.UtcNow - (node.LastUsedAt ?? node.RegisteredAt)).TotalSeconds
                );

                var wasHealthy = node.IsHealthy;
                node.IsHealthy = lastActivity <= _options.NodeTimeoutSeconds;
                node.LastHealthCheck = DateTime.UtcNow;

                if (wasHealthy != node.IsHealthy)
                {
                    _logger.LogWarning("Node {NodeId} health status changed: {OldStatus} -> {NewStatus}",
                        nodeId, wasHealthy ? "Healthy" : "Unhealthy", node.IsHealthy ? "Healthy" : "Unhealthy");
                }

                // 如果节点不健康且负载大于0，逐步减少负载
                if (!node.IsHealthy && node.CurrentLoad > 0)
                {
                    node.CurrentLoad = 0;
                    _logger.LogWarning("Reset load for unhealthy node {NodeId}", nodeId);
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
                
                // 清理所有节点
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
                
                _logger.LogInformation("LoadBalancingService disposed");
            }
        }
    }

    /// <summary>
    /// 服务器节点信息
    /// </summary>
    public class ServerNode
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

    /// <summary>
    /// 服务注册表
    /// </summary>
    public class ServiceRegistry
    {
        public string ServiceName { get; set; } = string.Empty;
        public List<string> Nodes { get; set; } = new();
        public int RoundRobinIndex { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 服务统计信息
    /// </summary>
    public class ServiceStats
    {
        public string ServiceName { get; set; } = string.Empty;
        public int TotalNodes { get; set; }
        public int HealthyNodes { get; set; }
        public long TotalRequests { get; set; }
        public double AverageLoad { get; set; }
        public int MaxLoad { get; set; }
        public Dictionary<string, NodeStats> NodeStats { get; set; } = new();
    }

    /// <summary>
    /// 节点统计信息
    /// </summary>
    public class NodeStats
    {
        public string NodeId { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public int CurrentLoad { get; set; }
        public long TotalRequests { get; set; }
        public int Weight { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }

    /// <summary>
    /// 负载均衡策略枚举
    /// </summary>
    public enum LoadBalancingStrategy
    {
        RoundRobin,         // 轮询
        WeightedRoundRobin, // 加权轮询
        LeastConnections,   // 最少连接
        Random,             // 随机
        ConsistentHash      // 一致性哈希
    }

    /// <summary>
    /// 负载均衡配置选项
    /// </summary>
    public class LoadBalancingOptions
    {
        public LoadBalancingStrategy DefaultStrategy { get; set; } = LoadBalancingStrategy.RoundRobin;
        public int DefaultWeight { get; set; } = 100;
        public int NodeTimeoutSeconds { get; set; } = 30;
        public int HealthCheckIntervalSeconds { get; set; } = 10;
        public bool EnableHealthCheck { get; set; } = true;
        public bool EnableMetrics { get; set; } = true;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;
    }
}
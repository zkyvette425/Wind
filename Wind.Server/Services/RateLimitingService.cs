using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Wind.Server.Services
{
    /// <summary>
    /// API限流服务
    /// 实现滑动窗口算法，支持多级限流策略
    /// </summary>
    public class RateLimitingService
    {
        private readonly ILogger<RateLimitingService> _logger;
        private readonly RateLimitOptions _options;
        
        // 客户端限流记录：IP/UserId -> 限流窗口
        private readonly ConcurrentDictionary<string, SlidingWindow> _clientWindows = new();
        
        // 全局限流记录：API端点 -> 限流窗口
        private readonly ConcurrentDictionary<string, SlidingWindow> _globalWindows = new();
        
        // 清理定时器
        private readonly Timer _cleanupTimer;

        public RateLimitingService(ILogger<RateLimitingService> logger, IOptions<RateLimitOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            
            // 启动定时清理过期窗口
            _cleanupTimer = new Timer(CleanupExpiredWindows, null, 
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 检查请求是否应被限流
        /// </summary>
        /// <param name="clientIdentifier">客户端标识符(IP或用户ID)</param>
        /// <param name="endpoint">API端点</param>
        /// <param name="policy">限流策略</param>
        /// <returns>限流检查结果</returns>
        public RateLimitCheckResult CheckRateLimit(string clientIdentifier, string endpoint, RateLimitPolicy policy)
        {
            try
            {
                var now = DateTime.UtcNow;
                
                // 1. 检查客户端级别限流
                var clientResult = CheckClientRateLimit(clientIdentifier, policy, now);
                if (!clientResult.IsAllowed)
                {
                    return clientResult;
                }

                // 2. 检查全局API端点限流
                var globalResult = CheckGlobalRateLimit(endpoint, policy, now);
                if (!globalResult.IsAllowed)
                {
                    return globalResult;
                }

                // 3. 记录成功请求
                RecordRequest(clientIdentifier, endpoint, now);

                return new RateLimitCheckResult
                {
                    IsAllowed = true,
                    ClientIdentifier = clientIdentifier,
                    Endpoint = endpoint,
                    RemainingRequests = Math.Min(clientResult.RemainingRequests, globalResult.RemainingRequests),
                    WindowResetTime = clientResult.WindowResetTime > globalResult.WindowResetTime ? 
                        clientResult.WindowResetTime : globalResult.WindowResetTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "限流检查时发生错误: Client={ClientId}, Endpoint={Endpoint}", 
                    clientIdentifier, endpoint);
                
                // 发生错误时允许请求通过，避免系统不可用
                return new RateLimitCheckResult
                {
                    IsAllowed = true,
                    ClientIdentifier = clientIdentifier,
                    Endpoint = endpoint,
                    RemainingRequests = 1000,
                    WindowResetTime = DateTime.UtcNow.AddMinutes(1)
                };
            }
        }

        /// <summary>
        /// 获取限流统计信息
        /// </summary>
        public RateLimitStats GetStats()
        {
            return new RateLimitStats
            {
                TotalClientWindows = _clientWindows.Count,
                TotalGlobalWindows = _globalWindows.Count,
                ActiveClients = _clientWindows.Values.Count(w => w.IsActive(DateTime.UtcNow)),
                TotalRequestsInWindow = _clientWindows.Values.Sum(w => w.RequestCount) + 
                                      _globalWindows.Values.Sum(w => w.RequestCount),
                AverageRequestsPerClient = _clientWindows.Values.Any() ? 
                    _clientWindows.Values.Average(w => w.RequestCount) : 0,
                LastCleanupTime = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 根据用户ID或IP获取限流策略
        /// </summary>
        public RateLimitPolicy GetPolicyForClient(string clientIdentifier, string endpoint)
        {
            // 检查是否是白名单客户端
            if (_options.WhitelistedClients?.Contains(clientIdentifier) == true)
            {
                return _options.WhitelistPolicy;
            }

            // 检查端点特定策略
            if (_options.EndpointPolicies?.TryGetValue(endpoint, out var endpointPolicy) == true)
            {
                return endpointPolicy;
            }

            // 返回默认策略
            return _options.DefaultPolicy;
        }

        private RateLimitCheckResult CheckClientRateLimit(string clientIdentifier, RateLimitPolicy policy, DateTime now)
        {
            var window = _clientWindows.GetOrAdd(clientIdentifier, 
                _ => new SlidingWindow(policy.WindowSize, policy.MaxRequests));

            return CheckWindow(window, policy, clientIdentifier, "client", now);
        }

        private RateLimitCheckResult CheckGlobalRateLimit(string endpoint, RateLimitPolicy policy, DateTime now)
        {
            if (policy.GlobalMaxRequests <= 0)
            {
                // 没有全局限制
                return new RateLimitCheckResult
                {
                    IsAllowed = true,
                    RemainingRequests = int.MaxValue,
                    WindowResetTime = now.AddSeconds(policy.WindowSize.TotalSeconds)
                };
            }

            var window = _globalWindows.GetOrAdd(endpoint, 
                _ => new SlidingWindow(policy.WindowSize, policy.GlobalMaxRequests));

            return CheckWindow(window, policy, endpoint, "global", now);
        }

        private RateLimitCheckResult CheckWindow(SlidingWindow window, RateLimitPolicy policy, 
            string identifier, string type, DateTime now)
        {
            lock (window)
            {
                // 清理过期请求
                window.CleanupExpiredRequests(now);

                var currentCount = window.RequestCount;
                var maxRequests = type == "global" ? policy.GlobalMaxRequests : policy.MaxRequests;

                if (currentCount >= maxRequests)
                {
                    _logger.LogWarning("限流触发: {Type}={Identifier}, 当前请求数={Current}, 限制={Limit}", 
                        type, identifier, currentCount, maxRequests);

                    return new RateLimitCheckResult
                    {
                        IsAllowed = false,
                        ClientIdentifier = identifier,
                        Endpoint = type == "global" ? identifier : "",
                        RemainingRequests = 0,
                        WindowResetTime = window.GetWindowResetTime(now),
                        LimitType = type,
                        CurrentRequests = currentCount,
                        MaxRequests = maxRequests,
                        RetryAfter = window.GetRetryAfter(now, policy.WindowSize)
                    };
                }

                return new RateLimitCheckResult
                {
                    IsAllowed = true,
                    ClientIdentifier = identifier,
                    RemainingRequests = maxRequests - currentCount - 1, // -1 for current request
                    WindowResetTime = window.GetWindowResetTime(now)
                };
            }
        }

        private void RecordRequest(string clientIdentifier, string endpoint, DateTime now)
        {
            // 记录客户端请求
            if (_clientWindows.TryGetValue(clientIdentifier, out var clientWindow))
            {
                lock (clientWindow)
                {
                    clientWindow.AddRequest(now);
                }
            }

            // 记录全局请求
            if (_globalWindows.TryGetValue(endpoint, out var globalWindow))
            {
                lock (globalWindow)
                {
                    globalWindow.AddRequest(now);
                }
            }
        }

        private void CleanupExpiredWindows(object? state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var cleanupThreshold = now.AddHours(-1); // 清理1小时前的窗口

                // 清理客户端窗口
                var clientKeysToRemove = _clientWindows
                    .Where(kvp => !kvp.Value.IsActive(cleanupThreshold))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in clientKeysToRemove)
                {
                    _clientWindows.TryRemove(key, out _);
                }

                // 清理全局窗口
                var globalKeysToRemove = _globalWindows
                    .Where(kvp => !kvp.Value.IsActive(cleanupThreshold))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in globalKeysToRemove)
                {
                    _globalWindows.TryRemove(key, out _);
                }

                if (clientKeysToRemove.Count > 0 || globalKeysToRemove.Count > 0)
                {
                    _logger.LogInformation("清理过期限流窗口: 客户端={ClientCount}, 全局={GlobalCount}", 
                        clientKeysToRemove.Count, globalKeysToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期限流窗口时发生错误");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }

    /// <summary>
    /// 滑动窗口实现
    /// </summary>
    public class SlidingWindow
    {
        private readonly TimeSpan _windowSize;
        private readonly int _maxRequests;
        private readonly Queue<DateTime> _requests = new();

        public SlidingWindow(TimeSpan windowSize, int maxRequests)
        {
            _windowSize = windowSize;
            _maxRequests = maxRequests;
        }

        public int RequestCount => _requests.Count;

        public void AddRequest(DateTime timestamp)
        {
            _requests.Enqueue(timestamp);
        }

        public void CleanupExpiredRequests(DateTime now)
        {
            var cutoff = now - _windowSize;
            
            while (_requests.Count > 0 && _requests.Peek() < cutoff)
            {
                _requests.Dequeue();
            }
        }

        public bool IsActive(DateTime threshold)
        {
            CleanupExpiredRequests(threshold);
            return _requests.Count > 0;
        }

        public DateTime GetWindowResetTime(DateTime now)
        {
            if (_requests.Count == 0)
                return now.Add(_windowSize);

            return _requests.Peek().Add(_windowSize);
        }

        public TimeSpan GetRetryAfter(DateTime now, TimeSpan windowSize)
        {
            if (_requests.Count == 0)
                return TimeSpan.Zero;

            var oldestRequest = _requests.Peek();
            var resetTime = oldestRequest.Add(windowSize);
            return resetTime > now ? resetTime - now : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// 限流检查结果
    /// </summary>
    public class RateLimitCheckResult
    {
        public bool IsAllowed { get; set; }
        public string ClientIdentifier { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public int RemainingRequests { get; set; }
        public DateTime WindowResetTime { get; set; }
        public string LimitType { get; set; } = string.Empty;
        public int CurrentRequests { get; set; }
        public int MaxRequests { get; set; }
        public TimeSpan RetryAfter { get; set; }
    }

    /// <summary>
    /// 限流统计信息
    /// </summary>
    public class RateLimitStats
    {
        public int TotalClientWindows { get; set; }
        public int TotalGlobalWindows { get; set; }
        public int ActiveClients { get; set; }
        public long TotalRequestsInWindow { get; set; }
        public double AverageRequestsPerClient { get; set; }
        public DateTime LastCleanupTime { get; set; }
    }

    /// <summary>
    /// 限流策略
    /// </summary>
    public class RateLimitPolicy
    {
        /// <summary>
        /// 时间窗口大小
        /// </summary>
        public TimeSpan WindowSize { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 单个客户端在窗口内的最大请求数
        /// </summary>
        public int MaxRequests { get; set; } = 100;

        /// <summary>
        /// 全局在窗口内的最大请求数（0表示无限制）
        /// </summary>
        public int GlobalMaxRequests { get; set; } = 0;

        /// <summary>
        /// 策略名称
        /// </summary>
        public string Name { get; set; } = "Default";
    }

    /// <summary>
    /// 限流配置选项
    /// </summary>
    public class RateLimitOptions
    {
        /// <summary>
        /// 默认限流策略
        /// </summary>
        public RateLimitPolicy DefaultPolicy { get; set; } = new RateLimitPolicy
        {
            Name = "Default",
            WindowSize = TimeSpan.FromMinutes(1),
            MaxRequests = 100,
            GlobalMaxRequests = 10000
        };

        /// <summary>
        /// 白名单策略（更宽松）
        /// </summary>
        public RateLimitPolicy WhitelistPolicy { get; set; } = new RateLimitPolicy
        {
            Name = "Whitelist",
            WindowSize = TimeSpan.FromMinutes(1),
            MaxRequests = 1000,
            GlobalMaxRequests = 0 // 无全局限制
        };

        /// <summary>
        /// 端点特定策略
        /// </summary>
        public Dictionary<string, RateLimitPolicy> EndpointPolicies { get; set; } = new()
        {
            ["LoginAsync"] = new RateLimitPolicy
            {
                Name = "Login",
                WindowSize = TimeSpan.FromMinutes(1),
                MaxRequests = 10, // 登录API更严格
                GlobalMaxRequests = 1000
            },
            ["RegisterAsync"] = new RateLimitPolicy
            {
                Name = "Register",
                WindowSize = TimeSpan.FromMinutes(5),
                MaxRequests = 3, // 注册API最严格
                GlobalMaxRequests = 100
            }
        };

        /// <summary>
        /// 白名单客户端列表
        /// </summary>
        public List<string> WhitelistedClients { get; set; } = new();

        /// <summary>
        /// 是否启用限流
        /// </summary>
        public bool EnableRateLimit { get; set; } = true;

        /// <summary>
        /// 是否记录限流日志
        /// </summary>
        public bool EnableLogging { get; set; } = true;
    }
}
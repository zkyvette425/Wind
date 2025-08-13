using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wind.Server.Services;
using Xunit;

namespace Wind.Tests.RateLimitTests
{
    /// <summary>
    /// API限流服务单元测试
    /// 验证滑动窗口算法和限流策略的正确性
    /// </summary>
    public class RateLimitingServiceTests : IDisposable
    {
        private readonly RateLimitingService _rateLimitingService;
        private readonly ILogger<RateLimitingService> _logger;
        private readonly RateLimitOptions _options;

        public RateLimitingServiceTests()
        {
            _logger = new LoggerFactory().CreateLogger<RateLimitingService>();
            _options = new RateLimitOptions
            {
                DefaultPolicy = new RateLimitPolicy
                {
                    Name = "Test",
                    WindowSize = TimeSpan.FromSeconds(10),
                    MaxRequests = 5,
                    GlobalMaxRequests = 20
                }
            };

            var optionsWrapper = Options.Create(_options);
            _rateLimitingService = new RateLimitingService(_logger, optionsWrapper);
        }

        [Fact]
        public void CheckRateLimit_允许正常请求()
        {
            // Arrange
            var clientId = "test-client-1";
            var endpoint = "TestEndpoint";
            var policy = _options.DefaultPolicy;

            // Act
            var result = _rateLimitingService.CheckRateLimit(clientId, endpoint, policy);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(4, result.RemainingRequests); // 5 - 1 = 4
            Assert.Equal(clientId, result.ClientIdentifier);
        }

        [Fact]
        public void CheckRateLimit_超出客户端限制时拒绝请求()
        {
            // Arrange
            var clientId = "test-client-2";
            var endpoint = "TestEndpoint";
            var policy = _options.DefaultPolicy;

            // Act - 发送6个请求（超过限制5个）
            for (int i = 0; i < 5; i++)
            {
                var allowedResult = _rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
                Assert.True(allowedResult.IsAllowed);
            }

            var deniedResult = _rateLimitingService.CheckRateLimit(clientId, endpoint, policy);

            // Assert
            Assert.False(deniedResult.IsAllowed);
            Assert.Equal("client", deniedResult.LimitType);
            Assert.Equal(5, deniedResult.CurrentRequests);
            Assert.Equal(5, deniedResult.MaxRequests);
            Assert.True(deniedResult.RetryAfter > TimeSpan.Zero);
        }

        [Fact]
        public void CheckRateLimit_不同客户端独立计数()
        {
            // Arrange
            var client1 = "test-client-3";
            var client2 = "test-client-4";
            var endpoint = "TestEndpoint";
            var policy = _options.DefaultPolicy;

            // Act - client1达到限制
            for (int i = 0; i < 5; i++)
            {
                _rateLimitingService.CheckRateLimit(client1, endpoint, policy);
            }

            var client1Result = _rateLimitingService.CheckRateLimit(client1, endpoint, policy);
            var client2Result = _rateLimitingService.CheckRateLimit(client2, endpoint, policy);

            // Assert
            Assert.False(client1Result.IsAllowed); // client1被限制
            Assert.True(client2Result.IsAllowed);  // client2正常
        }

        [Fact]
        public void CheckRateLimit_全局限制测试()
        {
            // Arrange
            var policy = new RateLimitPolicy
            {
                Name = "GlobalTest",
                WindowSize = TimeSpan.FromSeconds(10),
                MaxRequests = 100, // 客户端限制很高
                GlobalMaxRequests = 3 // 全局限制很低
            };
            var endpoint = "GlobalTestEndpoint";

            // Act - 使用不同客户端发送请求，触发全局限制
            for (int i = 0; i < 3; i++)
            {
                var clientId = $"client-{i}";
                var result = _rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
                Assert.True(result.IsAllowed);
            }

            // 第4个请求应该被全局限制阻止
            var deniedResult = _rateLimitingService.CheckRateLimit("client-4", endpoint, policy);

            // Assert
            Assert.False(deniedResult.IsAllowed);
            Assert.Equal("global", deniedResult.LimitType);
        }

        [Fact]
        public void GetPolicyForClient_端点特定策略测试()
        {
            // Arrange
            var options = new RateLimitOptions
            {
                DefaultPolicy = new RateLimitPolicy { MaxRequests = 100 },
                EndpointPolicies = new Dictionary<string, RateLimitPolicy>
                {
                    ["LoginAsync"] = new RateLimitPolicy { MaxRequests = 10 }
                }
            };
            var service = new RateLimitingService(_logger, Options.Create(options));

            // Act
            var loginPolicy = service.GetPolicyForClient("client", "LoginAsync");
            var defaultPolicy = service.GetPolicyForClient("client", "OtherEndpoint");

            // Assert
            Assert.Equal(10, loginPolicy.MaxRequests);
            Assert.Equal(100, defaultPolicy.MaxRequests);
        }

        [Fact]
        public void GetPolicyForClient_白名单客户端测试()
        {
            // Arrange
            var whitelistClient = "whitelist-client";
            var options = new RateLimitOptions
            {
                DefaultPolicy = new RateLimitPolicy { MaxRequests = 10 },
                WhitelistPolicy = new RateLimitPolicy { MaxRequests = 1000 },
                WhitelistedClients = new List<string> { whitelistClient }
            };
            var service = new RateLimitingService(_logger, Options.Create(options));

            // Act
            var whitelistPolicy = service.GetPolicyForClient(whitelistClient, "TestEndpoint");
            var normalPolicy = service.GetPolicyForClient("normal-client", "TestEndpoint");

            // Assert
            Assert.Equal(1000, whitelistPolicy.MaxRequests);
            Assert.Equal(10, normalPolicy.MaxRequests);
        }

        [Fact]
        public void GetStats_统计信息测试()
        {
            // Arrange
            var clientId = "stats-test-client";
            var endpoint = "StatsTestEndpoint";
            var policy = _options.DefaultPolicy;

            // Act - 发送一些请求
            for (int i = 0; i < 3; i++)
            {
                _rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
            }

            var stats = _rateLimitingService.GetStats();

            // Assert
            Assert.True(stats.TotalClientWindows > 0);
            Assert.True(stats.TotalGlobalWindows > 0);
            Assert.True(stats.ActiveClients > 0);
            Assert.True(stats.TotalRequestsInWindow > 0);
        }

        [Fact]
        public void SlidingWindow_时间窗口过期测试()
        {
            // Arrange
            var policy = new RateLimitPolicy
            {
                Name = "ExpireTest",
                WindowSize = TimeSpan.FromMilliseconds(100), // 100毫秒窗口
                MaxRequests = 2,
                GlobalMaxRequests = 10
            };
            var clientId = "expire-test-client";
            var endpoint = "ExpireTestEndpoint";

            // Act - 达到限制
            _rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
            _rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
            var deniedResult = _rateLimitingService.CheckRateLimit(clientId, endpoint, policy);

            Assert.False(deniedResult.IsAllowed);

            // 等待窗口过期
            Thread.Sleep(150);

            // 窗口过期后应该允许新请求
            var allowedResult = _rateLimitingService.CheckRateLimit(clientId, endpoint, policy);

            // Assert
            Assert.True(allowedResult.IsAllowed);
        }

        public void Dispose()
        {
            _rateLimitingService?.Dispose();
        }
    }
}
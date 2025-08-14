using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.TestingHost;
using Wind.Server.Services;
using Wind.Shared.Services;
using Wind.Shared.Protocols;
using Wind.Tests.TestFixtures;
using Xunit;
using MagicOnion.Client;
using Grpc.Net.Client;

namespace Wind.Tests.RateLimitTests
{
    /// <summary>
    /// API限流集成测试
    /// 验证整个限流系统在真实环境中的工作情况
    /// </summary>
    public class RateLimitIntegrationTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _clusterFixture;

        public RateLimitIntegrationTests(ClusterFixture clusterFixture)
        {
            _clusterFixture = clusterFixture;
        }

        private RateLimitingService CreateTestRateLimitingService()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            
            services.Configure<RateLimitOptions>(options =>
            {
                options.DefaultPolicy = new RateLimitPolicy
                {
                    Name = "IntegrationTestDefault",
                    WindowSize = TimeSpan.FromSeconds(5),
                    MaxRequests = 10,
                    GlobalMaxRequests = 100
                };
                options.EndpointPolicies = new Dictionary<string, RateLimitPolicy>
                {
                    ["LoginAsync"] = new RateLimitPolicy
                    {
                        Name = "LoginTest", 
                        WindowSize = TimeSpan.FromSeconds(10),
                        MaxRequests = 3,
                        GlobalMaxRequests = 20
                    }
                };
                options.WhitelistedClients = new List<string>();
                options.EnableRateLimit = true;
                options.EnableLogging = false; // 测试时关闭限流日志
            });

            services.AddSingleton<RateLimitingService>();
            
            var serviceProvider = services.BuildServiceProvider();
            return serviceProvider.GetRequiredService<RateLimitingService>();
        }

        [Fact]
        public async Task RateLimitingService_基本功能测试()
        {
            // Arrange - 创建测试专用的RateLimitingService
            var rateLimitingService = CreateTestRateLimitingService();
            
            var policy = new RateLimitPolicy
            {
                Name = "IntegrationTest",
                WindowSize = TimeSpan.FromSeconds(10),
                MaxRequests = 3,
                GlobalMaxRequests = 10
            };

            var clientId = "integration-test-client";
            var endpoint = "TestEndpoint";

            // Act & Assert - 前3个请求应该被允许
            for (int i = 0; i < 3; i++)
            {
                var result = rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
                Assert.True(result.IsAllowed, $"第{i + 1}个请求应该被允许");
                Assert.Equal(2 - i, result.RemainingRequests);
            }

            // 第4个请求应该被拒绝
            var deniedResult = rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
            Assert.False(deniedResult.IsAllowed, "第4个请求应该被拒绝");
            Assert.Equal("client", deniedResult.LimitType);
        }

        [Fact]
        public async Task RateLimitingService_多客户端并发测试()
        {
            // Arrange - 从Silo的ServiceProvider获取RateLimitingService
            var rateLimitingService = CreateTestRateLimitingService();
            
            var policy = new RateLimitPolicy
            {
                Name = "ConcurrentTest",
                WindowSize = TimeSpan.FromSeconds(10),
                MaxRequests = 2,
                GlobalMaxRequests = 5 // 全局限制5个
            };

            var endpoint = "ConcurrentTestEndpoint";
            var clientCount = 10;
            var requestsPerClient = 3;

            // Act - 并发发送请求
            var tasks = Enumerable.Range(0, clientCount).Select(async clientIndex =>
            {
                var clientId = $"concurrent-client-{clientIndex}";
                var results = new List<RateLimitCheckResult>();

                for (int i = 0; i < requestsPerClient; i++)
                {
                    var result = rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
                    results.Add(result);
                    
                    // 小延迟避免竞争条件
                    await Task.Delay(1);
                }

                return new { ClientId = clientId, Results = results };
            });

            var allResults = await Task.WhenAll(tasks);

            // Assert - 验证结果
            var totalAllowedRequests = allResults.SelectMany(r => r.Results).Count(r => r.IsAllowed);
            var totalDeniedRequests = allResults.SelectMany(r => r.Results).Count(r => !r.IsAllowed);

            // 由于全局限制是5，所以允许的请求数应该 <= 5
            Assert.True(totalAllowedRequests <= 5, $"允许的请求数({totalAllowedRequests})应该 <= 全局限制(5)");
            Assert.True(totalDeniedRequests > 0, "应该有请求被拒绝");

            // 验证每个客户端的限制
            foreach (var clientResult in allResults)
            {
                var clientAllowedCount = clientResult.Results.Count(r => r.IsAllowed);
                Assert.True(clientAllowedCount <= 2, $"客户端 {clientResult.ClientId} 允许的请求数({clientAllowedCount})应该 <= 客户端限制(2)");
            }
        }

        [Fact]
        public async Task RateLimitingService_时间窗口恢复测试()
        {
            // Arrange
            var rateLimitingService = CreateTestRateLimitingService();
            
            var policy = new RateLimitPolicy
            {
                Name = "RecoveryTest",
                WindowSize = TimeSpan.FromMilliseconds(500), // 500ms窗口
                MaxRequests = 2,
                GlobalMaxRequests = 10
            };

            var clientId = "recovery-test-client";
            var endpoint = "RecoveryTestEndpoint";

            // Act - 先达到限制
            var result1 = rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
            var result2 = rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
            var deniedResult = rateLimitingService.CheckRateLimit(clientId, endpoint, policy);

            Assert.True(result1.IsAllowed);
            Assert.True(result2.IsAllowed);
            Assert.False(deniedResult.IsAllowed);

            // 等待窗口恢复
            await Task.Delay(600);

            // 窗口恢复后应该允许新请求
            var recoveredResult = rateLimitingService.CheckRateLimit(clientId, endpoint, policy);

            // Assert
            Assert.True(recoveredResult.IsAllowed, "窗口恢复后应该允许新请求");
            Assert.Equal(1, recoveredResult.RemainingRequests);
        }

        [Fact]
        public async Task RateLimitingService_统计信息验证()
        {
            // Arrange
            var rateLimitingService = CreateTestRateLimitingService();
            
            var policy = new RateLimitPolicy
            {
                Name = "StatsTest",
                WindowSize = TimeSpan.FromSeconds(10),
                MaxRequests = 5,
                GlobalMaxRequests = 20
            };

            // Act - 发送一些请求
            var clientIds = new[] { "stats-client-1", "stats-client-2", "stats-client-3" };
            var endpoints = new[] { "StatsEndpoint1", "StatsEndpoint2" };

            foreach (var clientId in clientIds)
            {
                foreach (var endpoint in endpoints)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
                    }
                }
            }

            var stats = rateLimitingService.GetStats();

            // Assert
            Assert.True(stats.TotalClientWindows >= clientIds.Length, 
                $"客户端窗口数({stats.TotalClientWindows})应该 >= 客户端数({clientIds.Length})");
            Assert.True(stats.TotalGlobalWindows >= endpoints.Length, 
                $"全局窗口数({stats.TotalGlobalWindows})应该 >= 端点数({endpoints.Length})");
            Assert.True(stats.ActiveClients >= clientIds.Length, 
                $"活跃客户端数({stats.ActiveClients})应该 >= 客户端数({clientIds.Length})");
            Assert.True(stats.TotalRequestsInWindow > 0, "总请求数应该 > 0");
            Assert.True(stats.AverageRequestsPerClient > 0, "平均请求数应该 > 0");
        }

        [Fact]
        public async Task RateLimitingService_策略选择测试()
        {
            // Arrange
            var rateLimitingService = CreateTestRateLimitingService();
            
            var clientId = "policy-test-client";

            // Act & Assert - 测试不同端点的策略
            var loginPolicy = rateLimitingService.GetPolicyForClient(clientId, "LoginAsync");
            var defaultPolicy = rateLimitingService.GetPolicyForClient(clientId, "UnknownEndpoint");

            // LoginAsync应该有特定的限制策略
            Assert.NotNull(loginPolicy);
            Assert.True(loginPolicy.MaxRequests <= 20, "LoginAsync应该有较严格的限制");
            
            Assert.NotNull(defaultPolicy);
            Assert.True(defaultPolicy.MaxRequests > 0, "默认策略应该有效");
        }

        [Fact]
        public async Task RateLimitingService_错误处理测试()
        {
            // Arrange
            var rateLimitingService = CreateTestRateLimitingService();
            
            // Act & Assert - 测试异常输入
            var nullClientResult = rateLimitingService.CheckRateLimit(null!, "TestEndpoint", new RateLimitPolicy());
            Assert.True(nullClientResult.IsAllowed, "空客户端ID应该允许请求（错误容错）");

            var nullEndpointResult = rateLimitingService.CheckRateLimit("test-client", null!, new RateLimitPolicy());
            Assert.True(nullEndpointResult.IsAllowed, "空端点应该允许请求（错误容错）");

            var nullPolicyResult = rateLimitingService.CheckRateLimit("test-client", "TestEndpoint", null!);
            Assert.True(nullPolicyResult.IsAllowed, "空策略应该允许请求（错误容错）");
        }

        [Fact]
        public async Task RateLimitingService_高并发压力测试()
        {
            // Arrange
            var rateLimitingService = CreateTestRateLimitingService();
            
            var policy = new RateLimitPolicy
            {
                Name = "StressTest",
                WindowSize = TimeSpan.FromSeconds(10),
                MaxRequests = 10,
                GlobalMaxRequests = 100
            };

            var endpoint = "StressTestEndpoint";
            var concurrentClients = 50;
            var requestsPerClient = 20;

            // Act - 高并发请求
            var tasks = Enumerable.Range(0, concurrentClients).Select(async clientIndex =>
            {
                var clientId = $"stress-client-{clientIndex}";
                var allowedCount = 0;
                var deniedCount = 0;

                var clientTasks = Enumerable.Range(0, requestsPerClient).Select(async requestIndex =>
                {
                    var result = rateLimitingService.CheckRateLimit(clientId, endpoint, policy);
                    if (result.IsAllowed)
                        Interlocked.Increment(ref allowedCount);
                    else
                        Interlocked.Increment(ref deniedCount);
                });

                await Task.WhenAll(clientTasks);
                return new { ClientId = clientId, AllowedCount = allowedCount, DeniedCount = deniedCount };
            });

            var results = await Task.WhenAll(tasks);

            // Assert - 验证限流正确性
            var totalAllowed = results.Sum(r => r.AllowedCount);
            var totalDenied = results.Sum(r => r.DeniedCount);
            var totalRequests = totalAllowed + totalDenied;

            Assert.Equal(concurrentClients * requestsPerClient, totalRequests);
            Assert.True(totalAllowed <= 100, $"总允许请求数({totalAllowed})应该 <= 全局限制(100)");
            
            // 每个客户端允许的请求数应该 <= 客户端限制
            foreach (var result in results)
            {
                Assert.True(result.AllowedCount <= 10, 
                    $"客户端 {result.ClientId} 允许的请求数({result.AllowedCount})应该 <= 客户端限制(10)");
            }
        }
    }
}
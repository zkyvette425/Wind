using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wind.Server.Services;
using Wind.Server.Filters;
using Xunit;

namespace Wind.Tests.RateLimitTests
{
    /// <summary>
    /// API限流功能测试
    /// 验证完整的限流系统功能
    /// </summary>
    public class RateLimitFunctionalTests
    {
        [Fact]
        public void RateLimitingService_完整功能测试()
        {
            // Arrange - 创建服务容器
            var services = new ServiceCollection();
            var loggerFactory = new LoggerFactory();
            
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            
            // 配置限流选项
            services.Configure<RateLimitOptions>(options =>
            {
                options.DefaultPolicy = new RateLimitPolicy
                {
                    Name = "FunctionalTest",
                    WindowSize = TimeSpan.FromSeconds(5),
                    MaxRequests = 3,
                    GlobalMaxRequests = 10
                };
                options.EndpointPolicies = new Dictionary<string, RateLimitPolicy>
                {
                    ["LoginAsync"] = new RateLimitPolicy
                    {
                        Name = "LoginTest",
                        WindowSize = TimeSpan.FromSeconds(10),
                        MaxRequests = 2,
                        GlobalMaxRequests = 5
                    }
                };
                options.EnableRateLimit = true;
                options.EnableLogging = false;
            });
            
            // 注册限流服务
            services.AddSingleton<RateLimitingService>();
            
            var serviceProvider = services.BuildServiceProvider();
            var rateLimitingService = serviceProvider.GetRequiredService<RateLimitingService>();

            // Act & Assert - 测试基本限流功能
            var clientId = "functional-test-client";
            var endpoint = "TestEndpoint";
            var policy = rateLimitingService.GetPolicyForClient(clientId, endpoint);

            // 前3个请求应该被允许
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
            Assert.Equal(3, deniedResult.CurrentRequests);
        }

        [Fact]
        public void RateLimitingService_端点特定策略测试()
        {
            // Arrange
            var services = new ServiceCollection();
            var loggerFactory = new LoggerFactory();
            
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            
            services.Configure<RateLimitOptions>(options =>
            {
                options.DefaultPolicy = new RateLimitPolicy { MaxRequests = 10 };
                options.EndpointPolicies = new Dictionary<string, RateLimitPolicy>
                {
                    ["LoginAsync"] = new RateLimitPolicy { MaxRequests = 2 }
                };
                options.EnableRateLimit = true;
            });
            
            services.AddSingleton<RateLimitingService>();
            var serviceProvider = services.BuildServiceProvider();
            var rateLimitingService = serviceProvider.GetRequiredService<RateLimitingService>();

            // Act & Assert
            var clientId = "policy-test-client";
            
            var loginPolicy = rateLimitingService.GetPolicyForClient(clientId, "LoginAsync");
            var defaultPolicy = rateLimitingService.GetPolicyForClient(clientId, "OtherEndpoint");
            
            Assert.Equal(2, loginPolicy.MaxRequests);
            Assert.Equal(10, defaultPolicy.MaxRequests);
        }

        [Fact]
        public void RateLimitingService_多客户端测试()
        {
            // Arrange
            var services = new ServiceCollection();
            var loggerFactory = new LoggerFactory();
            
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            
            services.Configure<RateLimitOptions>(options =>
            {
                options.DefaultPolicy = new RateLimitPolicy
                {
                    WindowSize = TimeSpan.FromSeconds(10),
                    MaxRequests = 2,
                    GlobalMaxRequests = 3 // 全局限制3个
                };
                options.EnableRateLimit = true;
            });
            
            services.AddSingleton<RateLimitingService>();
            var serviceProvider = services.BuildServiceProvider();
            var rateLimitingService = serviceProvider.GetRequiredService<RateLimitingService>();

            // Act & Assert
            var endpoint = "MultiClientTestEndpoint";
            var client1 = "client-1";
            var client2 = "client-2";
            
            var policy = rateLimitingService.GetPolicyForClient(client1, endpoint);

            // Client1发送2个请求（达到客户端限制）
            var client1Result1 = rateLimitingService.CheckRateLimit(client1, endpoint, policy);
            var client1Result2 = rateLimitingService.CheckRateLimit(client1, endpoint, policy);
            var client1Result3 = rateLimitingService.CheckRateLimit(client1, endpoint, policy);
            
            Assert.True(client1Result1.IsAllowed);
            Assert.True(client1Result2.IsAllowed);
            Assert.False(client1Result3.IsAllowed); // 客户端限制

            // Client2发送1个请求（应该被全局限制阻止，因为已经有2个全局请求）
            var client2Result1 = rateLimitingService.CheckRateLimit(client2, endpoint, policy);
            Assert.True(client2Result1.IsAllowed); // 第3个全局请求，刚好到限制

            // 再发送一个应该被全局限制
            var client2Result2 = rateLimitingService.CheckRateLimit(client2, endpoint, policy);
            Assert.False(client2Result2.IsAllowed); // 全局限制
            Assert.Equal("global", client2Result2.LimitType);
        }

        [Fact]
        public void RateLimitingService_统计信息测试()
        {
            // Arrange
            var services = new ServiceCollection();
            var loggerFactory = new LoggerFactory();
            
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            
            services.Configure<RateLimitOptions>(options =>
            {
                options.DefaultPolicy = new RateLimitPolicy
                {
                    WindowSize = TimeSpan.FromSeconds(10),
                    MaxRequests = 5
                };
                options.EnableRateLimit = true;
            });
            
            services.AddSingleton<RateLimitingService>();
            var serviceProvider = services.BuildServiceProvider();
            var rateLimitingService = serviceProvider.GetRequiredService<RateLimitingService>();

            // Act - 发送一些请求
            var clients = new[] { "stats-client-1", "stats-client-2" };
            var endpoints = new[] { "StatsEndpoint1", "StatsEndpoint2" };
            
            foreach (var client in clients)
            {
                foreach (var endpoint in endpoints)
                {
                    var policy = rateLimitingService.GetPolicyForClient(client, endpoint);
                    rateLimitingService.CheckRateLimit(client, endpoint, policy);
                }
            }

            var stats = rateLimitingService.GetStats();

            // Assert
            Assert.True(stats.TotalClientWindows >= clients.Length);
            Assert.True(stats.TotalGlobalWindows >= endpoints.Length);
            Assert.True(stats.ActiveClients >= clients.Length);
            Assert.True(stats.TotalRequestsInWindow > 0);
        }

        [Fact]
        public void RateLimitFilter_特性实例化测试()
        {
            // Arrange & Act
            var loginFilter = new LoginRateLimitAttribute();
            var registerFilter = new RegisterRateLimitAttribute();
            var standardFilter = new StandardRateLimitAttribute();
            var highFrequencyFilter = new HighFrequencyRateLimitAttribute();

            // Assert - 验证特性能正确实例化
            Assert.NotNull(loginFilter);
            Assert.NotNull(registerFilter);
            Assert.NotNull(standardFilter);
            Assert.NotNull(highFrequencyFilter);
            
            // 这些都继承自 RateLimitFilterBase
            Assert.IsAssignableFrom<RateLimitFilterBase>(loginFilter);
            Assert.IsAssignableFrom<RateLimitFilterBase>(registerFilter);
            Assert.IsAssignableFrom<RateLimitFilterBase>(standardFilter);
            Assert.IsAssignableFrom<RateLimitFilterBase>(highFrequencyFilter);
        }

        [Fact]
        public void RateLimitCheckResult_属性验证()
        {
            // Arrange & Act
            var result = new RateLimitCheckResult
            {
                IsAllowed = true,
                ClientIdentifier = "test-client",
                Endpoint = "TestEndpoint",
                RemainingRequests = 5,
                WindowResetTime = DateTime.UtcNow.AddMinutes(1),
                LimitType = "client",
                CurrentRequests = 2,
                MaxRequests = 10,
                RetryAfter = TimeSpan.FromSeconds(30)
            };

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal("test-client", result.ClientIdentifier);
            Assert.Equal("TestEndpoint", result.Endpoint);
            Assert.Equal(5, result.RemainingRequests);
            Assert.Equal("client", result.LimitType);
            Assert.Equal(2, result.CurrentRequests);
            Assert.Equal(10, result.MaxRequests);
            Assert.Equal(TimeSpan.FromSeconds(30), result.RetryAfter);
        }

        [Fact]
        public void RateLimitPolicy_配置验证()
        {
            // Arrange & Act
            var policy = new RateLimitPolicy
            {
                Name = "TestPolicy",
                WindowSize = TimeSpan.FromMinutes(5),
                MaxRequests = 100,
                GlobalMaxRequests = 1000
            };

            // Assert
            Assert.Equal("TestPolicy", policy.Name);
            Assert.Equal(TimeSpan.FromMinutes(5), policy.WindowSize);
            Assert.Equal(100, policy.MaxRequests);
            Assert.Equal(1000, policy.GlobalMaxRequests);
        }
    }
}
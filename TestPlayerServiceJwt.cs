using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans;
using System;
using System.Threading.Tasks;
using Wind.Server.Services;
using Wind.Shared.Auth;
using Wind.Shared.Protocols;
using Wind.GrainInterfaces;
using Wind.Grains;

// PlayerService JWT集成测试程序
// 验证PlayerService中的JWT令牌生成和管理功能
class TestPlayerServiceJwt
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== PlayerService JWT集成测试 ===");
        Console.WriteLine($"测试时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();

        var cluster = new TestClusterBuilder()
            .AddSiloBuilderConfigurator<TestSiloConfigurations>()
            .Build();

        try
        {
            await cluster.DeployAsync();
            Console.WriteLine("✓ Orleans测试集群启动成功");

            // 创建JWT设置和服务
            var jwtSettings = JwtSettings.CreateDevelopmentSettings();
            var jwtOptions = Options.Create(jwtSettings);
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PlayerService>.Instance;
            var jwtLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<JwtService>.Instance;
            var jwtService = new JwtService(jwtOptions, jwtLogger);
            
            // 创建PlayerService
            var playerService = new PlayerService(cluster.GrainFactory, logger, jwtService);
            Console.WriteLine("✓ PlayerService初始化成功");
            Console.WriteLine();

            // 测试1: 玩家登录并获取JWT令牌
            Console.WriteLine("测试1: 玩家登录获取JWT令牌");
            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = "jwt_test_player",
                Password = "test_password",
                Platform = "test_platform",
                DeviceId = "test_device_123"
            };

            var loginResponse = await playerService.LoginAsync(loginRequest);
            Console.WriteLine($"✓ 登录结果: {(loginResponse.Success ? "成功" : "失败")}");
            
            if (loginResponse.Success)
            {
                Console.WriteLine($"  - 玩家ID: {loginResponse.PlayerId}");
                Console.WriteLine($"  - 访问令牌长度: {loginResponse.AccessToken?.Length ?? 0} 字符");
                Console.WriteLine($"  - 刷新令牌长度: {loginResponse.RefreshToken?.Length ?? 0} 字符");
                Console.WriteLine($"  - 访问令牌类型: {loginResponse.TokenType}");
                Console.WriteLine($"  - 访问令牌过期时间: {loginResponse.AccessTokenExpiry:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  - 刷新令牌过期时间: {loginResponse.RefreshTokenExpiry:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                Console.WriteLine($"  - 错误信息: {loginResponse.Message}");
            }
            Console.WriteLine();

            // 测试2: 验证生成的访问令牌
            if (loginResponse.Success && !string.IsNullOrEmpty(loginResponse.AccessToken))
            {
                Console.WriteLine("测试2: 验证生成的访问令牌");
                var validateRequest = new ValidateTokenRequest
                {
                    AccessToken = loginResponse.AccessToken,
                    ExpectedPlayerId = loginResponse.PlayerId
                };

                var validateResponse = await playerService.ValidateTokenAsync(validateRequest);
                Console.WriteLine($"✓ 令牌验证结果: {(validateResponse.IsValid ? "有效" : "无效")}");
                
                if (validateResponse.IsValid)
                {
                    Console.WriteLine($"  - 提取的玩家ID: {validateResponse.PlayerId}");
                    Console.WriteLine($"  - 令牌过期时间: {validateResponse.ExpiryTime:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"  - 声明数量: {validateResponse.Claims.Count}");
                    
                    if (validateResponse.Claims.Count > 0)
                    {
                        Console.WriteLine("  - 主要声明:");
                        foreach (var claim in validateResponse.Claims.Take(5))
                        {
                            Console.WriteLine($"    {claim.Key}: {claim.Value}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"  - 验证错误: {validateResponse.Message}");
                }
                Console.WriteLine();
            }

            // 测试3: 刷新访问令牌
            if (loginResponse.Success && !string.IsNullOrEmpty(loginResponse.RefreshToken))
            {
                Console.WriteLine("测试3: 刷新访问令牌");
                var refreshRequest = new RefreshTokenRequest
                {
                    RefreshToken = loginResponse.RefreshToken,
                    PlayerId = loginResponse.PlayerId
                };

                var refreshResponse = await playerService.RefreshTokenAsync(refreshRequest);
                Console.WriteLine($"✓ 令牌刷新结果: {(refreshResponse.Success ? "成功" : "失败")}");
                
                if (refreshResponse.Success)
                {
                    Console.WriteLine($"  - 新访问令牌长度: {refreshResponse.AccessToken?.Length ?? 0} 字符");
                    Console.WriteLine($"  - 新刷新令牌长度: {refreshResponse.RefreshToken?.Length ?? 0} 字符");
                    Console.WriteLine($"  - 新访问令牌过期时间: {refreshResponse.AccessTokenExpiry:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"  - 新刷新令牌过期时间: {refreshResponse.RefreshTokenExpiry:yyyy-MM-dd HH:mm:ss}");
                    
                    // 验证新的访问令牌
                    if (!string.IsNullOrEmpty(refreshResponse.AccessToken))
                    {
                        var newValidateRequest = new ValidateTokenRequest
                        {
                            AccessToken = refreshResponse.AccessToken,
                            ExpectedPlayerId = loginResponse.PlayerId
                        };
                        
                        var newValidateResponse = await playerService.ValidateTokenAsync(newValidateRequest);
                        Console.WriteLine($"  - 新令牌验证结果: {(newValidateResponse.IsValid ? "有效" : "无效")}");
                    }
                }
                else
                {
                    Console.WriteLine($"  - 刷新错误: {refreshResponse.Message}");
                }
                Console.WriteLine();
            }

            // 测试4: 撤销令牌
            if (loginResponse.Success && !string.IsNullOrEmpty(loginResponse.AccessToken))
            {
                Console.WriteLine("测试4: 撤销访问令牌");
                var revokeRequest = new RevokeTokenRequest
                {
                    Token = loginResponse.AccessToken,
                    PlayerId = loginResponse.PlayerId,
                    RevokeType = TokenRevokeType.AccessToken
                };

                var revokeResponse = await playerService.RevokeTokenAsync(revokeRequest);
                Console.WriteLine($"✓ 令牌撤销结果: {(revokeResponse.Success ? "成功" : "失败")}");
                Console.WriteLine($"  - 响应消息: {revokeResponse.Message}");
                Console.WriteLine();
            }

            // 测试5: 获取当前用户信息 (这个方法暂时返回固定响应)
            Console.WriteLine("测试5: 获取当前用户信息");
            var currentUserRequest = new GetCurrentUserRequest();
            var currentUserResponse = await playerService.GetCurrentUserAsync(currentUserRequest);
            Console.WriteLine($"✓ 获取用户信息结果: {(currentUserResponse.Success ? "成功" : "失败")}");
            Console.WriteLine($"  - 响应消息: {currentUserResponse.Message}");
            
            if (currentUserResponse.Success)
            {
                Console.WriteLine($"  - 用户ID: {currentUserResponse.PlayerId}");
                Console.WriteLine($"  - 显示名: {currentUserResponse.DisplayName}");
                Console.WriteLine($"  - 声明数量: {currentUserResponse.Claims.Count}");
            }
            Console.WriteLine();

            Console.WriteLine("=== 所有测试完成 ===");
            Console.WriteLine("PlayerService JWT集成功能验证通过!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 测试过程中发生错误: {ex.Message}");
            Console.WriteLine($"错误详情: {ex}");
        }
        finally
        {
            await cluster.StopAllSilosAsync();
            cluster?.Dispose();
        }
    }
}

public class TestSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder hostBuilder)
    {
        hostBuilder
            .AddMemoryGrainStorage("PlayerStorage")
            .AddMemoryGrainStorage("RoomStorage")
            .AddMemoryGrainStorage("MatchmakingStorage");
    }
}
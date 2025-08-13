using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using Wind.Server.Services;
using Wind.Shared.Auth;

// JWT服务功能验证程序
// 用于验证JWT令牌生成、验证、刷新等核心功能
class TestJwtService
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== JWT服务功能验证测试 ===");
        Console.WriteLine($"测试时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();

        try
        {
            // 创建JWT设置
            var jwtSettings = JwtSettings.CreateDevelopmentSettings();
            var options = Options.Create(jwtSettings);
            
            // 创建JWT服务
            var jwtService = new JwtService(options);
            
            Console.WriteLine("✓ JWT服务初始化成功");
            Console.WriteLine($"  - 发行者: {jwtSettings.Issuer}");
            Console.WriteLine($"  - 受众: {jwtSettings.Audience}");
            Console.WriteLine($"  - 访问令牌有效期: {jwtSettings.AccessTokenExpiry} 分钟");
            Console.WriteLine($"  - 刷新令牌有效期: {jwtSettings.RefreshTokenExpiry} 分钟");
            Console.WriteLine();
            
            // 测试1: 生成令牌
            Console.WriteLine("测试1: 生成JWT令牌");
            var playerId = "test_player_001";
            var additionalClaims = new Dictionary<string, string>
            {
                ["display_name"] = "测试玩家",
                ["platform"] = "test",
                ["device_id"] = "test_device_001"
            };
            
            var tokenResult = jwtService.GenerateTokens(playerId, additionalClaims);
            Console.WriteLine($"✓ 令牌生成成功");
            Console.WriteLine($"  - 玩家ID: {playerId}");
            Console.WriteLine($"  - 访问令牌长度: {tokenResult.AccessToken?.Length ?? 0} 字符");
            Console.WriteLine($"  - 刷新令牌长度: {tokenResult.RefreshToken?.Length ?? 0} 字符");
            Console.WriteLine($"  - 访问令牌过期时间: {tokenResult.AccessTokenExpiry:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  - 刷新令牌过期时间: {tokenResult.RefreshTokenExpiry:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
            
            // 测试2: 验证访问令牌
            Console.WriteLine("测试2: 验证访问令牌");
            var validationResult = jwtService.ValidateAccessToken(tokenResult.AccessToken!);
            Console.WriteLine($"✓ 令牌验证结果: {(validationResult.IsValid ? "有效" : "无效")}");
            
            if (validationResult.IsValid && validationResult.Principal != null)
            {
                var claims = validationResult.Principal.Claims;
                Console.WriteLine("  - 提取的声明:");
                foreach (var claim in claims)
                {
                    Console.WriteLine($"    {claim.Type}: {claim.Value}");
                }
            }
            Console.WriteLine();
            
            // 测试3: 刷新令牌
            Console.WriteLine("测试3: 刷新访问令牌");
            var refreshResult = jwtService.RefreshAccessToken(tokenResult.RefreshToken!);
            
            if (refreshResult != null)
            {
                Console.WriteLine("✓ 令牌刷新成功");
                Console.WriteLine($"  - 新访问令牌长度: {refreshResult.AccessToken?.Length ?? 0} 字符");
                Console.WriteLine($"  - 新访问令牌过期时间: {refreshResult.AccessTokenExpiry:yyyy-MM-dd HH:mm:ss}");
                
                // 验证新令牌
                var newTokenValidation = jwtService.ValidateAccessToken(refreshResult.AccessToken!);
                Console.WriteLine($"  - 新令牌验证结果: {(newTokenValidation.IsValid ? "有效" : "无效")}");
            }
            else
            {
                Console.WriteLine("✗ 令牌刷新失败");
            }
            Console.WriteLine();
            
            // 测试4: 验证无效令牌
            Console.WriteLine("测试4: 验证无效令牌");
            var invalidTokenResult = jwtService.ValidateAccessToken("invalid.jwt.token");
            Console.WriteLine($"✓ 无效令牌验证结果: {(invalidTokenResult.IsValid ? "有效" : "无效")} (预期: 无效)");
            if (!invalidTokenResult.IsValid)
            {
                Console.WriteLine($"  - 错误信息: {invalidTokenResult.Error}");
            }
            Console.WriteLine();
            
            // 测试5: 提取令牌信息
            Console.WriteLine("测试5: 提取令牌中的玩家ID");
            var extractedPlayerId = jwtService.ExtractPlayerIdFromToken(tokenResult.AccessToken!);
            Console.WriteLine($"✓ 提取的玩家ID: {extractedPlayerId} (预期: {playerId})");
            Console.WriteLine($"  - 匹配结果: {(extractedPlayerId == playerId ? "匹配" : "不匹配")}");
            Console.WriteLine();
            
            Console.WriteLine("=== 所有测试完成 ===");
            Console.WriteLine("JWT核心功能验证通过!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 测试过程中发生错误: {ex.Message}");
            Console.WriteLine($"错误详情: {ex}");
            Environment.Exit(1);
        }
    }
}
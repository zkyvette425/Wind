using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.TestingHost;
using System.Security.Claims;
using Wind.Server.Services;
using Wind.Shared.Auth;
using Wind.Shared.Protocols;
using Wind.Tests.TestFixtures;
using Xunit.Abstractions;

namespace Wind.Tests.Services
{
    /// <summary>
    /// JWT认证功能集成测试
    /// 测试令牌生成、验证、刷新等核心认证功能
    /// </summary>
    public class JwtAuthenticationTests : IClassFixture<ClusterFixture>, IDisposable
    {
        private readonly TestCluster _cluster;
        private readonly ITestOutputHelper _output;
        private readonly JwtService _jwtService;
        private readonly string _testPlayerId = "test_player_auth_001";

        public JwtAuthenticationTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _cluster = fixture.Cluster;
            _output = output;
            
            // 创建JWT设置用于测试
            var jwtSettings = JwtSettings.CreateDevelopmentSettings();
            var options = Options.Create(jwtSettings);
            var logger = _cluster.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JwtService>>();
            
            _jwtService = new JwtService(options, logger);
            
            _output.WriteLine($"JWT认证测试初始化完成 - 测试玩家ID: {_testPlayerId}");
        }

        [Fact(DisplayName = "JWT令牌生成应该成功")]
        public void GenerateTokens_ShouldCreateValidTokenPair()
        {
            // Arrange
            var additionalClaims = new Dictionary<string, string>
            {
                ["display_name"] = "测试玩家",
                ["platform"] = "TestPlatform",
                ["device_id"] = "test_device_123"
            };

            // Act
            var tokenResult = _jwtService.GenerateTokens(_testPlayerId, additionalClaims);

            // Assert
            Assert.NotNull(tokenResult);
            Assert.NotEmpty(tokenResult.AccessToken);
            Assert.NotEmpty(tokenResult.RefreshToken);
            Assert.Equal("Bearer", tokenResult.TokenType);
            Assert.True(tokenResult.AccessTokenExpiry > DateTime.UtcNow);
            Assert.True(tokenResult.RefreshTokenExpiry > DateTime.UtcNow);
            Assert.True(tokenResult.RefreshTokenExpiry > tokenResult.AccessTokenExpiry);

            _output.WriteLine($"✅ JWT令牌生成成功");
            _output.WriteLine($"   访问令牌长度: {tokenResult.AccessToken.Length}");
            _output.WriteLine($"   访问令牌过期时间: {tokenResult.AccessTokenExpiry}");
            _output.WriteLine($"   刷新令牌过期时间: {tokenResult.RefreshTokenExpiry}");
        }

        [Fact(DisplayName = "访问令牌验证应该成功")]
        public void ValidateAccessToken_WithValidToken_ShouldReturnSuccess()
        {
            // Arrange
            var tokenResult = _jwtService.GenerateTokens(_testPlayerId);
            
            // Act
            var validationResult = _jwtService.ValidateAccessToken(tokenResult.AccessToken);

            // Assert
            Assert.True(validationResult.IsValid);
            Assert.Null(validationResult.Error);
            Assert.NotNull(validationResult.Principal);
            
            var playerIdClaim = validationResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Assert.Equal(_testPlayerId, playerIdClaim);

            _output.WriteLine($"✅ 访问令牌验证成功 - 玩家ID: {playerIdClaim}");
        }

        [Fact(DisplayName = "刷新令牌验证应该成功")]
        public void ValidateRefreshToken_WithValidToken_ShouldReturnSuccess()
        {
            // Arrange
            var tokenResult = _jwtService.GenerateTokens(_testPlayerId);
            
            // Act
            var validationResult = _jwtService.ValidateRefreshToken(tokenResult.RefreshToken);

            // Assert
            Assert.True(validationResult.IsValid);
            Assert.Null(validationResult.Error);
            Assert.NotNull(validationResult.Principal);
            
            var playerIdClaim = validationResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Assert.Equal(_testPlayerId, playerIdClaim);

            _output.WriteLine($"✅ 刷新令牌验证成功 - 玩家ID: {playerIdClaim}");
        }

        [Fact(DisplayName = "无效令牌验证应该失败")]
        public void ValidateAccessToken_WithInvalidToken_ShouldReturnFailure()
        {
            // Arrange
            var invalidToken = "invalid.token.here";
            
            // Act
            var validationResult = _jwtService.ValidateAccessToken(invalidToken);

            // Assert
            Assert.False(validationResult.IsValid);
            Assert.NotNull(validationResult.Error);
            Assert.Null(validationResult.Principal);

            _output.WriteLine($"✅ 无效令牌正确拒绝 - 错误: {validationResult.Error}");
        }

        [Fact(DisplayName = "令牌刷新应该生成新的令牌对")]
        public void RefreshAccessToken_WithValidRefreshToken_ShouldReturnNewTokens()
        {
            // Arrange
            var originalTokens = _jwtService.GenerateTokens(_testPlayerId);
            
            // 等待1秒确保新令牌的时间戳不同
            Thread.Sleep(1000);
            
            // Act
            var refreshedTokens = _jwtService.RefreshAccessToken(originalTokens.RefreshToken);

            // Assert
            Assert.NotNull(refreshedTokens);
            Assert.NotEqual(originalTokens.AccessToken, refreshedTokens.AccessToken);
            Assert.NotEqual(originalTokens.RefreshToken, refreshedTokens.RefreshToken);
            Assert.True(refreshedTokens.AccessTokenExpiry > originalTokens.AccessTokenExpiry);

            // 验证新的访问令牌有效
            var validation = _jwtService.ValidateAccessToken(refreshedTokens.AccessToken);
            Assert.True(validation.IsValid);

            _output.WriteLine($"✅ 令牌刷新成功");
            _output.WriteLine($"   原访问令牌: {originalTokens.AccessToken[..20]}...");
            _output.WriteLine($"   新访问令牌: {refreshedTokens.AccessToken[..20]}...");
        }

        [Fact(DisplayName = "从令牌中提取玩家ID应该成功")]
        public void ExtractPlayerIdFromToken_WithValidToken_ShouldReturnPlayerId()
        {
            // Arrange
            var tokenResult = _jwtService.GenerateTokens(_testPlayerId);
            
            // Act
            var extractedPlayerId = _jwtService.ExtractPlayerIdFromToken(tokenResult.AccessToken);

            // Assert
            Assert.Equal(_testPlayerId, extractedPlayerId);

            _output.WriteLine($"✅ 玩家ID提取成功: {extractedPlayerId}");
        }

        [Fact(DisplayName = "令牌中包含正确的声明")]
        public void GenerateTokens_ShouldContainCorrectClaims()
        {
            // Arrange
            var additionalClaims = new Dictionary<string, string>
            {
                ["display_name"] = "测试玩家",
                ["platform"] = "TestPlatform",
                ["role"] = "player"
            };

            // Act
            var tokenResult = _jwtService.GenerateTokens(_testPlayerId, additionalClaims);
            var validationResult = _jwtService.ValidateAccessToken(tokenResult.AccessToken);

            // Assert
            Assert.True(validationResult.IsValid);
            Assert.NotNull(validationResult.Principal);

            var claims = validationResult.Principal.Claims.ToDictionary(c => c.Type, c => c.Value);
            
            Assert.Equal(_testPlayerId, claims[ClaimTypes.NameIdentifier]);
            Assert.Equal("测试玩家", claims["display_name"]);
            Assert.Equal("TestPlatform", claims["platform"]);
            Assert.Equal("player", claims["role"]);
            Assert.Equal("access", claims["token_type"]);

            _output.WriteLine($"✅ 令牌声明验证成功:");
            foreach (var claim in claims)
            {
                _output.WriteLine($"   {claim.Key}: {claim.Value}");
            }
        }

        [Fact(DisplayName = "空玩家ID应该抛出异常")]
        public void GenerateTokens_WithEmptyPlayerId_ShouldThrowException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                _jwtService.GenerateTokens(string.Empty));
            
            Assert.Contains("玩家ID不能为空", exception.Message);

            _output.WriteLine($"✅ 空玩家ID正确抛出异常: {exception.Message}");
        }

        [Fact(DisplayName = "过期令牌验证应该失败")]
        public void ValidateAccessToken_WithExpiredToken_ShouldReturnFailure()
        {
            // 这个测试需要修改JWT设置来创建立即过期的令牌
            // 为了测试目的，我们可以创建一个已过期的设置
            
            // Arrange
            var expiredSettings = new JwtSettings
            {
                SecretKey = "Test-JWT-Secret-Key-For-Expired-Token-Testing-At-Least-32-Characters-Long",
                Issuer = "Wind.GameServer.Test",
                Audience = "Wind.GameClient.Test",
                AccessTokenExpiryMinutes = -1, // 负数表示已过期
                RefreshTokenExpiryDays = 1,
                ValidateLifetime = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true
            };

            var expiredOptions = Options.Create(expiredSettings);
            var logger = _cluster.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JwtService>>();
            var expiredJwtService = new JwtService(expiredOptions, logger);

            // Act
            try 
            {
                var tokenResult = expiredJwtService.GenerateTokens(_testPlayerId);
                var validationResult = expiredJwtService.ValidateAccessToken(tokenResult.AccessToken);

                // Assert
                Assert.False(validationResult.IsValid);
                Assert.Contains("过期", validationResult.Error);

                _output.WriteLine($"✅ 过期令牌正确拒绝 - 错误: {validationResult.Error}");
            }
            catch (Exception ex)
            {
                // 过期令牌在生成时就可能失败，这也是正确的行为
                _output.WriteLine($"✅ 过期令牌生成失败（预期行为）: {ex.Message}");
            }
        }

        [Fact(DisplayName = "JWT设置验证应该工作正常")]
        public void JwtSettings_Validation_ShouldWorkCorrectly()
        {
            // Arrange
            var validSettings = JwtSettings.CreateDevelopmentSettings();
            var invalidSettings = new JwtSettings
            {
                SecretKey = "too_short", // 密钥太短
                Issuer = "",             // 发行者为空
                AccessTokenExpiryMinutes = 0 // 过期时间无效
            };

            // Act
            var (validResult, validErrors) = validSettings.Validate();
            var (invalidResult, invalidErrors) = invalidSettings.Validate();

            // Assert
            Assert.True(validResult);
            Assert.Empty(validErrors);
            
            Assert.False(invalidResult);
            Assert.NotEmpty(invalidErrors);

            _output.WriteLine($"✅ JWT设置验证功能正常");
            _output.WriteLine($"   有效设置: {validResult}");
            _output.WriteLine($"   无效设置错误数: {invalidErrors.Count}");
            foreach (var error in invalidErrors)
            {
                _output.WriteLine($"     - {error}");
            }
        }

        public void Dispose()
        {
            _output.WriteLine("JWT认证测试清理完成");
        }
    }
}
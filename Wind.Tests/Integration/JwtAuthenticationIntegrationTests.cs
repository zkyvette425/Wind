using Grpc.Core;
using MagicOnion.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Wind.Server.Services;
using Wind.Shared.Auth;
using Wind.Shared.Protocols;
using Wind.Shared.Services;
using Wind.Tests.Fixtures;
using Xunit.Abstractions;

namespace Wind.Tests.Integration
{
    /// <summary>
    /// JWT认证端到端集成测试
    /// 测试完整的用户登录、令牌验证、API调用流程
    /// </summary>
    public class JwtAuthenticationIntegrationTests : IClassFixture<ClusterFixture>, IDisposable
    {
        private readonly TestCluster _cluster;
        private readonly ITestOutputHelper _output;
        private readonly string _testPlayerId = "integration_test_player_001";

        public JwtAuthenticationIntegrationTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _cluster = fixture.Cluster;
            _output = output;
            _output.WriteLine($"JWT认证集成测试初始化 - 测试玩家ID: {_testPlayerId}");
        }

        [Fact(DisplayName = "完整登录流程应该返回有效的JWT令牌")]
        public async Task PlayerLogin_ShouldReturnValidJwtTokens()
        {
            // Arrange
            var playerService = CreatePlayerService();
            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = _testPlayerId,
                DisplayName = "集成测试玩家",
                ClientVersion = "1.0.0-test",
                Platform = "IntegrationTest",
                DeviceId = "test_device_integration_001"
            };

            // Act
            var loginResponse = await playerService.LoginAsync(loginRequest);

            // Assert
            Assert.True(loginResponse.Success, $"登录失败: {loginResponse.Message}");
            Assert.NotNull(loginResponse.AccessToken);
            Assert.NotNull(loginResponse.RefreshToken);
            Assert.NotNull(loginResponse.AccessTokenExpiry);
            Assert.NotNull(loginResponse.RefreshTokenExpiry);
            Assert.Equal("Bearer", loginResponse.TokenType);

            // 验证访问令牌有效性
            Assert.True(loginResponse.AccessTokenExpiry > DateTime.UtcNow);
            Assert.True(loginResponse.RefreshTokenExpiry > DateTime.UtcNow);
            Assert.True(loginResponse.RefreshTokenExpiry > loginResponse.AccessTokenExpiry);

            _output.WriteLine($"✅ 登录成功，获得有效JWT令牌");
            _output.WriteLine($"   玩家ID: {_testPlayerId}");
            _output.WriteLine($"   访问令牌过期: {loginResponse.AccessTokenExpiry}");
            _output.WriteLine($"   刷新令牌过期: {loginResponse.RefreshTokenExpiry}");
        }

        [Fact(DisplayName = "令牌验证API应该正确验证有效令牌")]
        public async Task ValidateToken_WithValidToken_ShouldReturnSuccess()
        {
            // Arrange
            var playerService = CreatePlayerService();
            var loginResponse = await LoginTestPlayer(playerService);
            
            var validateRequest = new ValidateTokenRequest
            {
                AccessToken = loginResponse.AccessToken!,
                ExpectedPlayerId = _testPlayerId
            };

            // Act
            var validateResponse = await playerService.ValidateTokenAsync(validateRequest);

            // Assert
            Assert.True(validateResponse.IsValid);
            Assert.Equal(_testPlayerId, validateResponse.PlayerId);
            Assert.NotNull(validateResponse.ExpiryTime);
            Assert.True(validateResponse.ExpiryTime > DateTime.UtcNow);
            Assert.NotEmpty(validateResponse.Claims);

            _output.WriteLine($"✅ 令牌验证成功");
            _output.WriteLine($"   玩家ID: {validateResponse.PlayerId}");
            _output.WriteLine($"   过期时间: {validateResponse.ExpiryTime}");
            _output.WriteLine($"   声明数量: {validateResponse.Claims.Count}");
        }

        [Fact(DisplayName = "令牌刷新API应该返回新的令牌对")]
        public async Task RefreshToken_WithValidRefreshToken_ShouldReturnNewTokens()
        {
            // Arrange
            var playerService = CreatePlayerService();
            var loginResponse = await LoginTestPlayer(playerService);
            
            // 等待1秒确保新令牌时间戳不同
            await Task.Delay(1000);
            
            var refreshRequest = new RefreshTokenRequest
            {
                RefreshToken = loginResponse.RefreshToken!,
                PlayerId = _testPlayerId
            };

            // Act
            var refreshResponse = await playerService.RefreshTokenAsync(refreshRequest);

            // Assert
            Assert.True(refreshResponse.Success);
            Assert.NotNull(refreshResponse.AccessToken);
            Assert.NotNull(refreshResponse.RefreshToken);
            Assert.NotEqual(loginResponse.AccessToken, refreshResponse.AccessToken);
            Assert.NotEqual(loginResponse.RefreshToken, refreshResponse.RefreshToken);

            // 验证新令牌有效
            var validateRequest = new ValidateTokenRequest
            {
                AccessToken = refreshResponse.AccessToken,
                ExpectedPlayerId = _testPlayerId
            };
            var validateResponse = await playerService.ValidateTokenAsync(validateRequest);
            Assert.True(validateResponse.IsValid);

            _output.WriteLine($"✅ 令牌刷新成功");
            _output.WriteLine($"   新访问令牌有效");
            _output.WriteLine($"   新刷新令牌有效");
        }

        [Fact(DisplayName = "无效刷新令牌应该被拒绝")]
        public async Task RefreshToken_WithInvalidToken_ShouldReturnFailure()
        {
            // Arrange
            var playerService = CreatePlayerService();
            var refreshRequest = new RefreshTokenRequest
            {
                RefreshToken = "invalid.refresh.token",
                PlayerId = _testPlayerId
            };

            // Act
            var refreshResponse = await playerService.RefreshTokenAsync(refreshRequest);

            // Assert
            Assert.False(refreshResponse.Success);
            Assert.Contains("无效", refreshResponse.Message);

            _output.WriteLine($"✅ 无效刷新令牌被正确拒绝: {refreshResponse.Message}");
        }

        [Fact(DisplayName = "获取当前用户信息需要有效认证")]
        public async Task GetCurrentUser_WithoutAuthentication_ShouldFail()
        {
            // 这个测试需要模拟认证上下文，当前简化处理
            // 在实际的端到端测试中，需要设置HTTP上下文和认证信息
            
            var playerService = CreatePlayerService();
            var request = new GetCurrentUserRequest();

            // Act & Assert
            // 注意：由于我们在测试环境中，HTTP上下文可能不完整
            // 这里主要测试方法能够正常调用而不抛出异常
            var response = await playerService.GetCurrentUserAsync(request);
            
            // 在测试环境中，用户未认证是预期的
            Assert.False(response.Success);
            Assert.Contains("未认证", response.Message);

            _output.WriteLine($"✅ 未认证用户被正确拒绝: {response.Message}");
        }

        [Fact(DisplayName = "令牌撤销API应该工作正常")]
        public async Task RevokeToken_WithValidToken_ShouldSucceed()
        {
            // Arrange
            var playerService = CreatePlayerService();
            var loginResponse = await LoginTestPlayer(playerService);
            
            var revokeRequest = new RevokeTokenRequest
            {
                Token = loginResponse.AccessToken!,
                PlayerId = _testPlayerId,
                RevokeType = TokenRevokeType.AccessToken
            };

            // Act
            var revokeResponse = await playerService.RevokeTokenAsync(revokeRequest);

            // Assert
            Assert.True(revokeResponse.Success);
            Assert.Contains("成功", revokeResponse.Message);

            _output.WriteLine($"✅ 令牌撤销成功: {revokeResponse.Message}");
        }

        [Fact(DisplayName = "登录失败不应该返回JWT令牌")]
        public async Task PlayerLogin_WithInvalidCredentials_ShouldNotReturnTokens()
        {
            // Arrange
            var playerService = CreatePlayerService();
            var invalidLoginRequest = new PlayerLoginRequest
            {
                PlayerId = "", // 无效的玩家ID
                DisplayName = "测试玩家",
                ClientVersion = "1.0.0-test",
                Platform = "IntegrationTest",
                DeviceId = "test_device"
            };

            // Act
            var loginResponse = await playerService.LoginAsync(invalidLoginRequest);

            // Assert
            Assert.False(loginResponse.Success);
            Assert.Null(loginResponse.AccessToken);
            Assert.Null(loginResponse.RefreshToken);

            _output.WriteLine($"✅ 无效登录被正确拒绝: {loginResponse.Message}");
        }

        [Fact(DisplayName = "JWT令牌应该包含正确的玩家信息")]
        public async Task JwtToken_ShouldContainCorrectPlayerInformation()
        {
            // Arrange
            var playerService = CreatePlayerService();
            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = _testPlayerId,
                DisplayName = "集成测试玩家",
                ClientVersion = "1.0.0-test",
                Platform = "IntegrationTest",
                DeviceId = "test_device_claims"
            };

            // Act
            var loginResponse = await playerService.LoginAsync(loginRequest);
            var validateRequest = new ValidateTokenRequest
            {
                AccessToken = loginResponse.AccessToken!,
                ExpectedPlayerId = _testPlayerId
            };
            var validateResponse = await playerService.ValidateTokenAsync(validateRequest);

            // Assert
            Assert.True(validateResponse.IsValid);
            Assert.Equal(_testPlayerId, validateResponse.PlayerId);
            
            // 验证声明中包含正确信息
            Assert.True(validateResponse.Claims.ContainsKey("display_name"));
            Assert.Equal("集成测试玩家", validateResponse.Claims["display_name"]);
            Assert.True(validateResponse.Claims.ContainsKey("platform"));
            Assert.Equal("IntegrationTest", validateResponse.Claims["platform"]);
            Assert.True(validateResponse.Claims.ContainsKey("device_id"));
            Assert.Equal("test_device_claims", validateResponse.Claims["device_id"]);

            _output.WriteLine($"✅ JWT令牌包含正确的玩家信息");
            _output.WriteLine($"   显示名称: {validateResponse.Claims["display_name"]}");
            _output.WriteLine($"   平台: {validateResponse.Claims["platform"]}");
            _output.WriteLine($"   设备ID: {validateResponse.Claims["device_id"]}");
        }

        /// <summary>
        /// 辅助方法：创建PlayerService实例
        /// </summary>
        private IPlayerService CreatePlayerService()
        {
            // 从Orleans集群获取IGrainFactory
            var grainFactory = _cluster.GrainFactory;
            
            // 创建必要的依赖
            var logger = _cluster.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PlayerService>>();
            
            // 创建JWT设置
            var jwtSettings = JwtSettings.CreateDevelopmentSettings();
            var jwtOptions = Microsoft.Extensions.Options.Options.Create(jwtSettings);
            var jwtLogger = _cluster.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JwtService>>();
            var jwtService = new JwtService(jwtOptions, jwtLogger);

            // 创建PlayerService实例
            return new PlayerService(grainFactory, logger, jwtService);
        }

        /// <summary>
        /// 辅助方法：登录测试玩家
        /// </summary>
        private async Task<PlayerLoginResponse> LoginTestPlayer(IPlayerService playerService)
        {
            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = _testPlayerId,
                DisplayName = "集成测试玩家",
                ClientVersion = "1.0.0-test",
                Platform = "IntegrationTest",
                DeviceId = "test_device_integration"
            };

            var response = await playerService.LoginAsync(loginRequest);
            Assert.True(response.Success, $"登录失败: {response.Message}");
            return response;
        }

        public void Dispose()
        {
            _output.WriteLine("JWT认证集成测试清理完成");
        }
    }
}
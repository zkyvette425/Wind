using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Wind.Shared.Auth;

namespace Wind.Server.Services
{
    /// <summary>
    /// JWT令牌生成和验证服务
    /// 提供访问令牌和刷新令牌的完整生命周期管理
    /// </summary>
    public class JwtService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<JwtService> _logger;
        private readonly TokenValidationParameters _tokenValidationParameters;
        private readonly JwtSecurityTokenHandler _tokenHandler;

        public JwtService(IOptions<JwtSettings> jwtSettings, ILogger<JwtService> logger)
        {
            _jwtSettings = jwtSettings.Value;
            _logger = logger;
            _tokenHandler = new JwtSecurityTokenHandler();

            // 验证JWT设置
            var (isValid, errors) = _jwtSettings.Validate();
            if (!isValid)
            {
                var errorMessage = $"JWT配置验证失败: {string.Join(", ", errors)}";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // 初始化令牌验证参数
            _tokenValidationParameters = CreateTokenValidationParameters();
            _logger.LogInformation("JWT服务已初始化，Issuer: {Issuer}, Audience: {Audience}", 
                _jwtSettings.Issuer, _jwtSettings.Audience);
        }

        /// <summary>
        /// 生成访问令牌和刷新令牌对
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="additionalClaims">额外的声明</param>
        /// <returns>令牌对信息</returns>
        public TokenResult GenerateTokens(string playerId, Dictionary<string, string>? additionalClaims = null)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("玩家ID不能为空", nameof(playerId));

            try
            {
                var now = DateTime.UtcNow;
                var accessTokenExpiry = now.Add(_jwtSettings.AccessTokenExpiry);
                var refreshTokenExpiry = now.Add(_jwtSettings.RefreshTokenExpiry);

                // 生成访问令牌声明
                var claims = CreateClaims(playerId, additionalClaims);
                
                // 生成访问令牌
                var accessToken = GenerateToken(claims, accessTokenExpiry, TokenType.Access);
                
                // 生成刷新令牌 (简化版声明)
                var refreshClaims = CreateRefreshClaims(playerId);
                var refreshToken = GenerateToken(refreshClaims, refreshTokenExpiry, TokenType.Refresh);

                _logger.LogInformation("为玩家 {PlayerId} 成功生成令牌对，访问令牌过期时间: {AccessExpiry}, 刷新令牌过期时间: {RefreshExpiry}", 
                    playerId, accessTokenExpiry, refreshTokenExpiry);

                return new TokenResult
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    AccessTokenExpiry = accessTokenExpiry,
                    RefreshTokenExpiry = refreshTokenExpiry,
                    TokenType = "Bearer"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "为玩家 {PlayerId} 生成令牌失败", playerId);
                throw new InvalidOperationException($"令牌生成失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 验证访问令牌
        /// </summary>
        /// <param name="token">待验证的令牌</param>
        /// <returns>验证结果</returns>
        public TokenValidationResult ValidateAccessToken(string token)
        {
            return ValidateToken(token, TokenType.Access);
        }

        /// <summary>
        /// 验证刷新令牌
        /// </summary>
        /// <param name="token">待验证的刷新令牌</param>
        /// <returns>验证结果</returns>
        public TokenValidationResult ValidateRefreshToken(string token)
        {
            return ValidateToken(token, TokenType.Refresh);
        }

        /// <summary>
        /// 使用刷新令牌生成新的访问令牌
        /// </summary>
        /// <param name="refreshToken">有效的刷新令牌</param>
        /// <returns>新的令牌对</returns>
        public TokenResult? RefreshAccessToken(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;

            try
            {
                var validationResult = ValidateRefreshToken(refreshToken);
                if (!validationResult.IsValid || validationResult.Principal == null)
                {
                    _logger.LogWarning("刷新令牌验证失败");
                    return null;
                }

                var playerId = validationResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    _logger.LogWarning("刷新令牌中缺少有效的玩家ID");
                    return null;
                }

                // 从现有刷新令牌中提取额外声明
                var additionalClaims = ExtractAdditionalClaims(validationResult.Principal);

                // 生成新的令牌对
                var newTokens = GenerateTokens(playerId, additionalClaims);
                
                _logger.LogInformation("成功为玩家 {PlayerId} 刷新访问令牌", playerId);
                return newTokens;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新访问令牌失败");
                return null;
            }
        }

        /// <summary>
        /// 从令牌中提取玩家ID
        /// </summary>
        /// <param name="token">JWT令牌</param>
        /// <returns>玩家ID，如果提取失败返回null</returns>
        public string? ExtractPlayerIdFromToken(string token)
        {
            try
            {
                var validationResult = ValidateAccessToken(token);
                return validationResult.IsValid ? 
                    validationResult.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value : 
                    null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 创建令牌验证参数
        /// </summary>
        private TokenValidationParameters CreateTokenValidationParameters()
        {
            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);
            return new TokenValidationParameters
            {
                ValidateIssuer = _jwtSettings.ValidateIssuer,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = _jwtSettings.ValidateAudience,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = _jwtSettings.ValidateLifetime,
                ValidateIssuerSigningKey = _jwtSettings.ValidateIssuerSigningKey,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = _jwtSettings.ClockSkew
            };
        }

        /// <summary>
        /// 创建访问令牌的声明集合
        /// </summary>
        private List<Claim> CreateClaims(string playerId, Dictionary<string, string>? additionalClaims)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, playerId),
                new(ClaimTypes.Name, playerId),
                new(JwtRegisteredClaimNames.Sub, playerId),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new("token_type", TokenType.Access.ToString().ToLower())
            };

            // 添加额外声明
            if (additionalClaims != null)
            {
                foreach (var claim in additionalClaims)
                {
                    claims.Add(new Claim(claim.Key, claim.Value));
                }
            }

            return claims;
        }

        /// <summary>
        /// 创建刷新令牌的声明集合（简化版）
        /// </summary>
        private List<Claim> CreateRefreshClaims(string playerId)
        {
            return new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, playerId),
                new(JwtRegisteredClaimNames.Sub, playerId),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new("token_type", TokenType.Refresh.ToString().ToLower())
            };
        }

        /// <summary>
        /// 生成JWT令牌
        /// </summary>
        private string GenerateToken(List<Claim> claims, DateTime expiry, TokenType tokenType)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiry,
                SigningCredentials = credentials,
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            return _tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// 验证令牌
        /// </summary>
        private TokenValidationResult ValidateToken(string token, TokenType expectedType)
        {
            if (string.IsNullOrWhiteSpace(token))
                return new TokenValidationResult { IsValid = false, Error = "令牌不能为空" };

            try
            {
                var principal = _tokenHandler.ValidateToken(token, _tokenValidationParameters, out var validatedToken);
                
                // 验证令牌类型
                var tokenTypeClaim = principal.FindFirst("token_type")?.Value;
                if (tokenTypeClaim != expectedType.ToString().ToLower())
                {
                    return new TokenValidationResult 
                    { 
                        IsValid = false, 
                        Error = $"令牌类型不匹配，期望: {expectedType}, 实际: {tokenTypeClaim}" 
                    };
                }

                return new TokenValidationResult 
                { 
                    IsValid = true, 
                    Principal = principal,
                    SecurityToken = validatedToken
                };
            }
            catch (SecurityTokenExpiredException)
            {
                return new TokenValidationResult { IsValid = false, Error = "令牌已过期" };
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                return new TokenValidationResult { IsValid = false, Error = "令牌签名无效" };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "令牌验证失败");
                return new TokenValidationResult { IsValid = false, Error = $"令牌验证失败: {ex.Message}" };
            }
        }

        /// <summary>
        /// 从令牌主体中提取额外声明
        /// </summary>
        private Dictionary<string, string> ExtractAdditionalClaims(ClaimsPrincipal principal)
        {
            var additionalClaims = new Dictionary<string, string>();
            
            // 跳过标准声明
            var standardClaims = new HashSet<string> 
            {
                ClaimTypes.NameIdentifier,
                ClaimTypes.Name,
                JwtRegisteredClaimNames.Sub,
                JwtRegisteredClaimNames.Jti,
                JwtRegisteredClaimNames.Iat,
                JwtRegisteredClaimNames.Exp,
                JwtRegisteredClaimNames.Iss,
                JwtRegisteredClaimNames.Aud,
                "token_type"
            };

            foreach (var claim in principal.Claims)
            {
                if (!standardClaims.Contains(claim.Type))
                {
                    additionalClaims[claim.Type] = claim.Value;
                }
            }

            return additionalClaims;
        }
    }

    /// <summary>
    /// 令牌类型枚举
    /// </summary>
    public enum TokenType
    {
        Access,
        Refresh
    }

    /// <summary>
    /// 令牌生成结果
    /// </summary>
    public class TokenResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime AccessTokenExpiry { get; set; }
        public DateTime RefreshTokenExpiry { get; set; }
        public string TokenType { get; set; } = "Bearer";
    }

    /// <summary>
    /// 令牌验证结果
    /// </summary>
    public class TokenValidationResult
    {
        public bool IsValid { get; set; }
        public ClaimsPrincipal? Principal { get; set; }
        public SecurityToken? SecurityToken { get; set; }
        public string? Error { get; set; }
    }
}
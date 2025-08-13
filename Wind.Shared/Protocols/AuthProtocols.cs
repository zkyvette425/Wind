using System.ComponentModel.DataAnnotations;
using MessagePack;
using MPKey = MessagePack.KeyAttribute;

namespace Wind.Shared.Protocols
{
    /// <summary>
    /// 令牌刷新请求
    /// </summary>
    [MessagePackObject]
    public class RefreshTokenRequest
    {
        [MPKey(0)]
        [Required]
        public string RefreshToken { get; set; } = string.Empty;

        [MPKey(1)]
        public string PlayerId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 令牌刷新响应
    /// </summary>
    [MessagePackObject]
    public class RefreshTokenResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public string? AccessToken { get; set; }

        [MPKey(3)]
        public string? RefreshToken { get; set; }

        [MPKey(4)]
        public DateTime? AccessTokenExpiry { get; set; }

        [MPKey(5)]
        public DateTime? RefreshTokenExpiry { get; set; }

        [MPKey(6)]
        public string TokenType { get; set; } = "Bearer";
    }

    /// <summary>
    /// 令牌验证请求
    /// </summary>
    [MessagePackObject]
    public class ValidateTokenRequest
    {
        [MPKey(0)]
        [Required]
        public string AccessToken { get; set; } = string.Empty;

        [MPKey(1)]
        public string? ExpectedPlayerId { get; set; }
    }

    /// <summary>
    /// 令牌验证响应
    /// </summary>
    [MessagePackObject]
    public class ValidateTokenResponse
    {
        [MPKey(0)]
        public bool IsValid { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public string? PlayerId { get; set; }

        [MPKey(3)]
        public DateTime? ExpiryTime { get; set; }

        [MPKey(4)]
        public Dictionary<string, string> Claims { get; set; } = new();
    }

    /// <summary>
    /// 令牌撤销请求
    /// </summary>
    [MessagePackObject]
    public class RevokeTokenRequest
    {
        [MPKey(0)]
        [Required]
        public string Token { get; set; } = string.Empty;

        [MPKey(1)]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(2)]
        public TokenRevokeType RevokeType { get; set; } = TokenRevokeType.AccessToken;
    }

    /// <summary>
    /// 令牌撤销响应
    /// </summary>
    [MessagePackObject]
    public class RevokeTokenResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 获取当前用户信息请求 (需要认证)
    /// </summary>
    [MessagePackObject]
    public class GetCurrentUserRequest
    {
        // 这个请求不需要参数，直接从JWT令牌中获取用户信息
    }

    /// <summary>
    /// 获取当前用户信息响应
    /// </summary>
    [MessagePackObject]
    public class GetCurrentUserResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public string? PlayerId { get; set; }

        [MPKey(3)]
        public string? DisplayName { get; set; }

        [MPKey(4)]
        public Dictionary<string, string> Claims { get; set; } = new();
    }

    /// <summary>
    /// 修改密码请求
    /// </summary>
    [MessagePackObject]
    public class ChangePasswordRequest
    {
        [MPKey(0)]
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        [MPKey(1)]
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [MPKey(2)]
        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// 修改密码响应
    /// </summary>
    [MessagePackObject]
    public class ChangePasswordResponse
    {
        [MPKey(0)]
        public bool Success { get; set; }

        [MPKey(1)]
        public string Message { get; set; } = string.Empty;

        [MPKey(2)]
        public bool RequireReLogin { get; set; }
    }

    /// <summary>
    /// 令牌撤销类型
    /// </summary>
    public enum TokenRevokeType
    {
        AccessToken = 0,
        RefreshToken = 1,
        AllTokens = 2
    }

    /// <summary>
    /// JWT声明常量
    /// </summary>
    public static class JwtClaimTypes
    {
        public const string PlayerId = "player_id";
        public const string DisplayName = "display_name";
        public const string SessionId = "session_id";
        public const string Platform = "platform";
        public const string DeviceId = "device_id";
        public const string LoginTime = "login_time";
        public const string TokenType = "token_type";
        public const string RefreshTokenId = "refresh_token_id";
    }

    /// <summary>
    /// 认证错误代码
    /// </summary>
    public static class AuthErrorCodes
    {
        public const string InvalidToken = "INVALID_TOKEN";
        public const string ExpiredToken = "EXPIRED_TOKEN";
        public const string MalformedToken = "MALFORMED_TOKEN";
        public const string InsufficientPermissions = "INSUFFICIENT_PERMISSIONS";
        public const string InvalidCredentials = "INVALID_CREDENTIALS";
        public const string AccountLocked = "ACCOUNT_LOCKED";
        public const string TokenRevoked = "TOKEN_REVOKED";
        public const string UnknownError = "UNKNOWN_ERROR";
    }
}
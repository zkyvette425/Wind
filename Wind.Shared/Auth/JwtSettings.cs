using System.ComponentModel.DataAnnotations;

namespace Wind.Shared.Auth
{
    /// <summary>
    /// JWT认证配置设置
    /// 包含令牌生成和验证的关键参数
    /// </summary>
    public class JwtSettings
    {
        /// <summary>
        /// JWT配置节名称
        /// </summary>
        public const string SectionName = "JwtSettings";

        /// <summary>
        /// JWT签名密钥 (必须至少256位/32字节)
        /// 生产环境中应从安全的配置源获取
        /// </summary>
        [Required]
        [MinLength(32, ErrorMessage = "JWT密钥长度必须至少32个字符")]
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// JWT发行者 (iss声明)
        /// 标识颁发令牌的服务
        /// </summary>
        [Required]
        public string Issuer { get; set; } = "Wind.GameServer";

        /// <summary>
        /// JWT受众 (aud声明)  
        /// 标识令牌的目标接收者
        /// </summary>
        [Required]
        public string Audience { get; set; } = "Wind.GameClient";

        /// <summary>
        /// 访问令牌过期时间（分钟）
        /// 默认15分钟，平衡安全性和用户体验
        /// </summary>
        [Range(1, 1440, ErrorMessage = "访问令牌过期时间必须在1-1440分钟之间")]
        public int AccessTokenExpiryMinutes { get; set; } = 15;

        /// <summary>
        /// 刷新令牌过期时间（天）
        /// 默认7天，允许长期保持登录状态
        /// </summary>
        [Range(1, 30, ErrorMessage = "刷新令牌过期时间必须在1-30天之间")]
        public int RefreshTokenExpiryDays { get; set; } = 7;

        /// <summary>
        /// 是否验证发行者
        /// 生产环境建议设为true
        /// </summary>
        public bool ValidateIssuer { get; set; } = true;

        /// <summary>
        /// 是否验证受众
        /// 生产环境建议设为true
        /// </summary>
        public bool ValidateAudience { get; set; } = true;

        /// <summary>
        /// 是否验证生命周期
        /// 建议始终设为true
        /// </summary>
        public bool ValidateLifetime { get; set; } = true;

        /// <summary>
        /// 是否验证签名密钥
        /// 必须设为true以确保令牌安全性
        /// </summary>
        public bool ValidateIssuerSigningKey { get; set; } = true;

        /// <summary>
        /// 时钟偏移容忍度（分钟）
        /// 允许客户端和服务器时间轻微差异
        /// </summary>
        [Range(0, 10, ErrorMessage = "时钟偏移容忍度必须在0-10分钟之间")]
        public int ClockSkewMinutes { get; set; } = 2;

        /// <summary>
        /// 获取访问令牌过期时间跨度
        /// </summary>
        public TimeSpan AccessTokenExpiry => TimeSpan.FromMinutes(AccessTokenExpiryMinutes);

        /// <summary>
        /// 获取刷新令牌过期时间跨度
        /// </summary>
        public TimeSpan RefreshTokenExpiry => TimeSpan.FromDays(RefreshTokenExpiryDays);

        /// <summary>
        /// 获取时钟偏移容忍度时间跨度
        /// </summary>
        public TimeSpan ClockSkew => TimeSpan.FromMinutes(ClockSkewMinutes);

        /// <summary>
        /// 验证JWT设置的有效性
        /// </summary>
        /// <returns>验证结果和错误信息</returns>
        public (bool IsValid, List<string> Errors) Validate()
        {
            var errors = new List<string>();
            var validationContext = new ValidationContext(this);
            var validationResults = new List<ValidationResult>();

            if (!Validator.TryValidateObject(this, validationContext, validationResults, true))
            {
                errors.AddRange(validationResults.Select(vr => vr.ErrorMessage ?? "未知验证错误"));
            }

            // 额外的业务逻辑验证
            if (string.IsNullOrWhiteSpace(SecretKey))
            {
                errors.Add("JWT密钥不能为空");
            }
            else if (SecretKey.Length < 32)
            {
                errors.Add("JWT密钥长度不足，必须至少32个字符以确保安全性");
            }

            if (AccessTokenExpiryMinutes > RefreshTokenExpiryDays * 1440)
            {
                errors.Add("访问令牌过期时间不能超过刷新令牌过期时间");
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// 生成默认的开发环境JWT设置
        /// 仅用于开发和测试，生产环境必须使用安全的密钥
        /// </summary>
        /// <returns>开发环境JWT设置</returns>
        public static JwtSettings CreateDevelopmentSettings()
        {
            return new JwtSettings
            {
                SecretKey = "Wind-Game-Server-Development-JWT-Secret-Key-2025-Very-Secure-Random-String",
                Issuer = "Wind.GameServer.Development",
                Audience = "Wind.GameClient.Development",
                AccessTokenExpiryMinutes = 30, // 开发环境延长到30分钟
                RefreshTokenExpiryDays = 1, // 开发环境缩短到1天
                ValidateIssuer = false, // 开发环境关闭严格验证
                ValidateAudience = false,
                ClockSkewMinutes = 5 // 开发环境允许更大时钟偏移
            };
        }
    }
}
using MagicOnion;
using MagicOnion.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using Wind.Server.Services;

namespace Wind.Server.Filters
{
    /// <summary>
    /// MagicOnion限流过滤器基类
    /// 专门为MagicOnion服务设计的限流拦截器
    /// </summary>
    public abstract class RateLimitFilterBase : MagicOnionFilterAttribute
    {
        protected readonly string? _policyName;
        protected readonly int? _maxRequests;
        protected readonly int? _windowSeconds;

        protected RateLimitFilterBase(string? policyName = null, int? maxRequests = null, int? windowSeconds = null)
        {
            _policyName = policyName;
            _maxRequests = maxRequests;
            _windowSeconds = windowSeconds;
        }

        public override async ValueTask Invoke(ServiceContext context, Func<ServiceContext, ValueTask> next)
        {
            var serviceProvider = context.ServiceProvider;
            var rateLimitingService = serviceProvider.GetRequiredService<RateLimitingService>();
            var logger = serviceProvider.GetRequiredService<ILogger<RateLimitFilterBase>>();
            var options = serviceProvider.GetRequiredService<IOptions<RateLimitOptions>>().Value;

            // 检查是否启用限流
            if (!options.EnableRateLimit)
            {
                await next(context);
                return;
            }

            try
            {
                // 获取客户端标识符
                var clientIdentifier = GetClientIdentifier(context);
                
                // 获取方法名作为端点
                var endpoint = GetMethodName(context);
                
                // 获取适用的限流策略
                var policy = GetEffectivePolicy(clientIdentifier, endpoint, rateLimitingService);
                
                // 执行限流检查
                var result = rateLimitingService.CheckRateLimit(clientIdentifier, endpoint, policy);

                if (!result.IsAllowed)
                {
                    // 请求被限流，抛出异常
                    await HandleRateLimitExceeded(context, result, logger, options);
                    return;
                }

                // 记录限流信息到响应头（如果可能）
                RecordRateLimitInfo(context, result);

                // 继续执行请求
                await next(context);
            }
            catch (RateLimitExceededException)
            {
                // 重新抛出限流异常
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "限流过滤器处理请求时发生错误: {Method}", context.MethodInfo?.Name);
                
                // 发生错误时允许请求继续，避免系统完全不可用
                await next(context);
            }
        }

        /// <summary>
        /// 获取客户端标识符
        /// </summary>
        private string GetClientIdentifier(ServiceContext context)
        {
            try
            {
                // 尝试从gRPC元数据获取用户标识
                var headers = context.CallContext.RequestHeaders;
                var authHeader = headers?.FirstOrDefault(h => h.Key == "authorization");
                
                if (authHeader != null && !string.IsNullOrEmpty(authHeader.Value))
                {
                    // 简化处理：使用authorization header的hash作为用户标识
                    var authHash = authHeader.Value.GetHashCode().ToString();
                    return $"auth:{authHash}";
                }

                // 回退到peer信息
                var peer = context.CallContext.Peer ?? "unknown";
                return $"peer:{peer}";
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>
        /// 获取方法名
        /// </summary>
        private string GetMethodName(ServiceContext context)
        {
            return context.MethodInfo?.Name ?? "unknown";
        }

        /// <summary>
        /// 获取有效的限流策略
        /// </summary>
        private RateLimitPolicy GetEffectivePolicy(string clientIdentifier, string endpoint, RateLimitingService rateLimitingService)
        {
            // 如果过滤器指定了自定义策略参数，创建自定义策略
            if (_maxRequests.HasValue || _windowSeconds.HasValue)
            {
                var basePolicy = rateLimitingService.GetPolicyForClient(clientIdentifier, endpoint);
                return new RateLimitPolicy
                {
                    Name = _policyName ?? $"Custom_{endpoint}",
                    MaxRequests = _maxRequests ?? basePolicy.MaxRequests,
                    WindowSize = _windowSeconds.HasValue ? 
                        TimeSpan.FromSeconds(_windowSeconds.Value) : basePolicy.WindowSize,
                    GlobalMaxRequests = basePolicy.GlobalMaxRequests
                };
            }

            // 使用服务配置的策略
            return rateLimitingService.GetPolicyForClient(clientIdentifier, endpoint);
        }

        /// <summary>
        /// 处理限流超限情况
        /// </summary>
        private Task HandleRateLimitExceeded(ServiceContext context, RateLimitCheckResult result, ILogger logger, RateLimitOptions options)
        {
            if (options.EnableLogging)
            {
                logger.LogWarning("MagicOnion限流阻止请求: Client={Client}, Method={Method}, " +
                                 "Type={Type}, Current={Current}, Max={Max}",
                    result.ClientIdentifier, context.MethodInfo?.Name, result.LimitType,
                    result.CurrentRequests, result.MaxRequests);
            }

            // 抛出自定义限流异常
            throw new RateLimitExceededException(
                $"请求频率过高，请稍后重试。{result.LimitType}限制: {result.MaxRequests}请求/窗口",
                result);
        }

        /// <summary>
        /// 记录限流信息到响应（如果可能）
        /// </summary>
        private void RecordRateLimitInfo(ServiceContext context, RateLimitCheckResult result)
        {
            try
            {
                // 将限流信息添加到调用上下文，可供服务方法访问
                context.Items["RateLimit.Remaining"] = result.RemainingRequests;
                context.Items["RateLimit.Reset"] = result.WindowResetTime;
                context.Items["RateLimit.Limit"] = result.MaxRequests;
            }
            catch
            {
                // 记录错误但不影响请求处理
            }
        }
    }


    /// <summary>
    /// 限流超限异常
    /// </summary>
    public class RateLimitExceededException : Exception
    {
        public RateLimitCheckResult Result { get; }

        public RateLimitExceededException(string message, RateLimitCheckResult result) 
            : base(message)
        {
            Result = result;
        }

        public RateLimitExceededException(string message, RateLimitCheckResult result, Exception innerException) 
            : base(message, innerException)
        {
            Result = result;
        }
    }

    /// <summary>
    /// 限流特性类，提供便捷的使用方式
    /// </summary>
    
    /// <summary>
    /// 登录API限流 - 较严格的限制
    /// </summary>
    public class LoginRateLimitAttribute : RateLimitFilterBase
    {
        public LoginRateLimitAttribute() : base("Login", 10, 60) { }
    }

    /// <summary>
    /// 注册API限流 - 最严格的限制
    /// </summary>
    public class RegisterRateLimitAttribute : RateLimitFilterBase
    {
        public RegisterRateLimitAttribute() : base("Register", 3, 300) { }
    }

    /// <summary>
    /// 一般API限流 - 标准限制
    /// </summary>
    public class StandardRateLimitAttribute : RateLimitFilterBase
    {
        public StandardRateLimitAttribute() : base("Standard", 100, 60) { }
    }

    /// <summary>
    /// 高频API限流 - 宽松限制
    /// </summary>
    public class HighFrequencyRateLimitAttribute : RateLimitFilterBase
    {
        public HighFrequencyRateLimitAttribute() : base("HighFrequency", 500, 60) { }
    }
}
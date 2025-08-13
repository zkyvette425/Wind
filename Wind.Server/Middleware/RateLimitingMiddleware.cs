using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using Wind.Server.Services;

namespace Wind.Server.Middleware
{
    /// <summary>
    /// API限流中间件
    /// 在HTTP请求处理管道中拦截请求并应用限流策略
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RateLimitingService _rateLimitingService;
        private readonly RateLimitOptions _options;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        public RateLimitingMiddleware(
            RequestDelegate next,
            RateLimitingService rateLimitingService,
            IOptions<RateLimitOptions> options,
            ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _rateLimitingService = rateLimitingService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 检查是否启用限流
            if (!_options.EnableRateLimit)
            {
                await _next(context);
                return;
            }

            // 跳过非API请求（如静态文件、健康检查等）
            if (ShouldSkipRateLimit(context))
            {
                await _next(context);
                return;
            }

            try
            {
                // 获取客户端标识符
                var clientIdentifier = GetClientIdentifier(context);
                
                // 获取API端点
                var endpoint = GetEndpoint(context);
                
                // 获取适用的限流策略
                var policy = _rateLimitingService.GetPolicyForClient(clientIdentifier, endpoint);
                
                // 执行限流检查
                var result = _rateLimitingService.CheckRateLimit(clientIdentifier, endpoint, policy);

                if (!result.IsAllowed)
                {
                    // 请求被限流，返回429状态码
                    await HandleRateLimitExceeded(context, result);
                    return;
                }

                // 添加限流响应头
                AddRateLimitHeaders(context, result);

                // 继续处理请求
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "限流中间件处理请求时发生错误: {Path}", context.Request.Path);
                
                // 发生错误时允许请求继续，避免系统完全不可用
                await _next(context);
            }
        }

        /// <summary>
        /// 判断是否应跳过限流检查
        /// </summary>
        private bool ShouldSkipRateLimit(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant();
            
            // 跳过的路径模式
            var skipPatterns = new[]
            {
                "/health",
                "/metrics", 
                "/favicon.ico",
                "/.well-known",
                "/swagger",
                "/api/system"
            };

            return skipPatterns.Any(pattern => path?.StartsWith(pattern) == true);
        }

        /// <summary>
        /// 获取客户端标识符（优先使用用户ID，其次使用IP）
        /// </summary>
        private string GetClientIdentifier(HttpContext context)
        {
            // 优先使用已认证用户的ID
            var userId = context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            // 使用JWT中的用户ID
            var jwtUserId = context.User?.FindFirst("sub")?.Value ?? 
                           context.User?.FindFirst("playerId")?.Value;
            if (!string.IsNullOrEmpty(jwtUserId))
            {
                return $"user:{jwtUserId}";
            }

            // 回退到IP地址
            var remoteIp = GetClientIpAddress(context);
            return $"ip:{remoteIp}";
        }

        /// <summary>
        /// 获取客户端真实IP地址
        /// </summary>
        private string GetClientIpAddress(HttpContext context)
        {
            // 检查代理头
            var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                // X-Forwarded-For 可能包含多个IP，取第一个
                return xForwardedFor.Split(',')[0].Trim();
            }

            var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp))
            {
                return xRealIp;
            }

            // 使用连接的远程IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        /// <summary>
        /// 获取API端点标识
        /// </summary>
        private string GetEndpoint(HttpContext context)
        {
            // 对于MagicOnion，可能需要从路径中提取方法名
            var path = context.Request.Path.Value;
            
            if (string.IsNullOrEmpty(path))
                return "unknown";

            // 提取MagicOnion服务方法名
            // 路径格式通常是: /IPlayerService/LoginAsync
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                return segments[1]; // 返回方法名
            }

            return path;
        }

        /// <summary>
        /// 处理限流超限情况
        /// </summary>
        private async Task HandleRateLimitExceeded(HttpContext context, RateLimitCheckResult result)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";

            // 添加标准限流响应头
            context.Response.Headers.Add("X-RateLimit-Limit", result.MaxRequests.ToString());
            context.Response.Headers.Add("X-RateLimit-Remaining", "0");
            context.Response.Headers.Add("X-RateLimit-Reset", 
                ((DateTimeOffset)result.WindowResetTime).ToUnixTimeSeconds().ToString());
            
            if (result.RetryAfter > TimeSpan.Zero)
            {
                context.Response.Headers.Add("Retry-After", 
                    ((int)result.RetryAfter.TotalSeconds).ToString());
            }

            // 创建错误响应
            var errorResponse = new
            {
                error = "rate_limit_exceeded",
                message = $"请求频率过高，请稍后重试。{result.LimitType}限制: {result.MaxRequests}请求/窗口",
                details = new
                {
                    limit_type = result.LimitType,
                    max_requests = result.MaxRequests,
                    current_requests = result.CurrentRequests,
                    window_reset = result.WindowResetTime.ToString("O"),
                    retry_after_seconds = (int)result.RetryAfter.TotalSeconds
                }
            };

            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (_options.EnableLogging)
            {
                _logger.LogWarning("限流阻止请求: Client={Client}, Endpoint={Endpoint}, Type={Type}, " +
                                 "Current={Current}, Max={Max}, ResetTime={Reset}",
                    result.ClientIdentifier, result.Endpoint, result.LimitType,
                    result.CurrentRequests, result.MaxRequests, result.WindowResetTime);
            }

            await context.Response.WriteAsync(json);
        }

        /// <summary>
        /// 添加限流相关的响应头
        /// </summary>
        private void AddRateLimitHeaders(HttpContext context, RateLimitCheckResult result)
        {
            if (context.Response.HasStarted)
                return;

            try
            {
                context.Response.Headers.Add("X-RateLimit-Limit", result.MaxRequests.ToString());
                context.Response.Headers.Add("X-RateLimit-Remaining", result.RemainingRequests.ToString());
                context.Response.Headers.Add("X-RateLimit-Reset", 
                    ((DateTimeOffset)result.WindowResetTime).ToUnixTimeSeconds().ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "添加限流响应头时发生错误");
            }
        }
    }

    /// <summary>
    /// 限流中间件扩展方法
    /// </summary>
    public static class RateLimitingMiddlewareExtensions
    {
        /// <summary>
        /// 添加限流中间件到请求管道
        /// </summary>
        public static IApplicationBuilder UseRateLimit(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RateLimitingMiddleware>();
        }

        /// <summary>
        /// 注册限流服务
        /// </summary>
        public static IServiceCollection AddRateLimit(this IServiceCollection services, 
            Action<RateLimitOptions>? configureOptions = null)
        {
            // 配置选项
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            else
            {
                services.Configure<RateLimitOptions>(options => { }); // 使用默认配置
            }

            // 注册限流服务
            services.AddSingleton<RateLimitingService>();

            return services;
        }
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wind.Server.Configuration;
using Wind.Server.Services;
using Serilog;

namespace Wind.Server.Extensions;

/// <summary>
/// Redis缓存服务扩展
/// 提供Redis缓存策略和连接管理的依赖注入配置
/// </summary>
public static class RedisCacheExtensions
{
    /// <summary>
    /// 添加Redis缓存策略服务
    /// </summary>
    public static IServiceCollection AddRedisCacheStrategy(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置Redis选项
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        
        // 注册Redis连接管理器（单例）
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var redisOptions = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
            var logger = serviceProvider.GetRequiredService<Serilog.ILogger>();
            
            try
            {
                redisOptions.Validate();
                
                var configuration = ConfigurationOptions.Parse(redisOptions.ConnectionString);
                
                // 应用Redis连接配置
                configuration.ConnectTimeout = redisOptions.ConnectTimeout;
                configuration.SyncTimeout = redisOptions.SyncTimeout;
                configuration.AsyncTimeout = redisOptions.AsyncTimeout;
                configuration.Ssl = redisOptions.EnableSsl;
                configuration.Password = redisOptions.Password;
                configuration.User = redisOptions.Username;
                
                // 连接池和重试配置
                configuration.ConnectRetry = redisOptions.RetryCount;
                configuration.ReconnectRetryPolicy = new ExponentialRetry(redisOptions.RetryDelay);
                
                // 启用性能优化
                configuration.AbortOnConnectFail = false; // 避免启动时因Redis连接失败而崩溃
                configuration.ChannelPrefix = RedisChannel.Literal(redisOptions.KeyPrefix ?? "Wind:v1.2:");
                
                logger.Information("Redis连接配置: {ConnectionString}, SSL={SSL}, 重试={RetryCount}", 
                    MaskConnectionString(redisOptions.ConnectionString), redisOptions.EnableSsl, redisOptions.RetryCount);
                
                var multiplexer = ConnectionMultiplexer.Connect(configuration);
                
                // 注册连接事件监听
                multiplexer.ConnectionFailed += (sender, args) =>
                {
                    logger.Error("Redis连接失败: {EndPoint}, {FailureType}", args.EndPoint, args.FailureType);
                };
                
                multiplexer.ConnectionRestored += (sender, args) =>
                {
                    logger.Information("Redis连接恢复: {EndPoint}", args.EndPoint);
                };
                
                multiplexer.ErrorMessage += (sender, args) =>
                {
                    logger.Warning("Redis错误消息: {Message} from {EndPoint}", args.Message, args.EndPoint);
                };
                
                logger.Information("Redis连接建立成功: {EndPoints}", 
                    string.Join(", ", multiplexer.GetEndPoints().Select(ep => ep.ToString())));
                
                return multiplexer;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Redis连接初始化失败，将使用内存缓存替代");
                
                // 返回一个假的连接，用于测试和开发环境
                // 在生产环境中应该抛出异常
                throw new InvalidOperationException("Redis连接失败，请检查Redis服务状态和配置", ex);
            }
        });
        
        // 注册Redis缓存策略服务
        services.AddSingleton<RedisCacheStrategy>();
        
        // 注册Redis连接管理器（如果需要更高级的管理功能）
        services.AddSingleton<RedisConnectionManager>();
        
        return services;
    }
    
    /// <summary>
    /// 添加Redis缓存策略服务（简化版本，使用默认配置）
    /// </summary>
    public static IServiceCollection AddRedisCacheStrategy(this IServiceCollection services, string connectionString, string keyPrefix = "Wind:v1.2:")
    {
        services.Configure<RedisOptions>(options =>
        {
            options.ConnectionString = connectionString;
            options.KeyPrefix = keyPrefix;
            options.DefaultTtlSeconds = 3600; // 1小时默认TTL
            options.ConnectTimeout = 5000;
            options.SyncTimeout = 5000;
            options.AsyncTimeout = 5000;
            options.RetryCount = 3;
            options.RetryDelay = 1000;
            options.EnableHealthCheck = true;
        });
        
        // 创建简单的内存配置用于测试
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Redis:ConnectionString"] = connectionString,
                ["Redis:KeyPrefix"] = keyPrefix,
                ["Redis:DefaultTtlSeconds"] = "3600",
                ["Redis:ConnectTimeout"] = "5000",
                ["Redis:SyncTimeout"] = "5000",
                ["Redis:AsyncTimeout"] = "5000",
                ["Redis:RetryCount"] = "3",
                ["Redis:RetryDelay"] = "1000",
                ["Redis:EnableHealthCheck"] = "true"
            });
        
        return services.AddRedisCacheStrategy(configBuilder.Build());
    }
    
    /// <summary>
    /// 验证Redis连接是否可用
    /// </summary>
    public static async Task<bool> TestRedisConnectionAsync(this IServiceProvider serviceProvider)
    {
        try
        {
            var multiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
            var database = multiplexer.GetDatabase();
            
            // 执行简单的ping测试
            var pingTime = await database.PingAsync();
            
            var logger = serviceProvider.GetRequiredService<Serilog.ILogger>();
            logger.Information("Redis连接测试成功: Ping={PingMs}ms", pingTime.TotalMilliseconds);
            
            return true;
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetRequiredService<Serilog.ILogger>();
            logger.Error(ex, "Redis连接测试失败");
            return false;
        }
    }
    
    /// <summary>
    /// 获取Redis缓存健康状态
    /// </summary>
    public static async Task<(bool IsHealthy, string Status, Dictionary<string, object> Details)> GetRedisCacheHealthAsync(this IServiceProvider serviceProvider)
    {
        try
        {
            var multiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
            var cacheStrategy = serviceProvider.GetRequiredService<RedisCacheStrategy>();
            
            // 检查连接状态
            var isConnected = multiplexer.IsConnected;
            if (!isConnected)
            {
                return (false, "Disconnected", new Dictionary<string, object>
                {
                    ["connected"] = false,
                    ["endpoints"] = multiplexer.GetEndPoints().Select(ep => ep.ToString()).ToArray()
                });
            }
            
            // 获取缓存统计
            var stats = await cacheStrategy.GetCacheStatisticsAsync();
            
            var details = new Dictionary<string, object>
            {
                ["connected"] = true,
                ["endpoints"] = multiplexer.GetEndPoints().Select(ep => ep.ToString()).ToArray(),
                ["used_memory_mb"] = stats.UsedMemory / 1024.0 / 1024.0,
                ["total_keys"] = stats.TotalKeys,
                ["hit_rate_percent"] = stats.HitRate,
                ["expired_keys"] = stats.ExpiredKeys,
                ["evicted_keys"] = stats.EvictedKeys
            };
            
            // 判断健康状态
            var isHealthy = isConnected && stats.HitRate >= 70.0; // 命中率阈值70%
            var status = isHealthy ? "Healthy" : "Degraded";
            
            return (isHealthy, status, details);
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}", new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["exception_type"] = ex.GetType().Name
            });
        }
    }
    
    /// <summary>
    /// 屏蔽连接字符串中的敏感信息
    /// </summary>
    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return connectionString;
            
        // 屏蔽密码信息
        return System.Text.RegularExpressions.Regex.Replace(
            connectionString, 
            @"password=([^,;]+)", 
            "password=***", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
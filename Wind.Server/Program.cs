using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;
using Orleans.Serialization;
using Serilog;
using MagicOnion;
using MagicOnion.Server;
using MessagePack;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Wind.Shared.Auth;
using Wind.Server.Services;
using Wind.Server.Middleware;

// 从配置文件读取Serilog配置
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Information("启动Orleans Silo + MagicOnion宿主...");

    // 使用WebApplication支持MagicOnion的gRPC服务
    var builder = WebApplication.CreateBuilder(args);

    // 配置Kestrel支持HTTP/2 (MagicOnion需要)
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenLocalhost(5271, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        });
    });

    // 配置Orleans Silo
    builder.Host.UseOrleans(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            .ConfigureEndpoints(
                advertisedIP: IPAddress.Loopback,
                siloPort: 11111, 
                gatewayPort: 30000)
            // 配置内存持久化存储 (临时方案，后续升级到Redis)
            .AddMemoryGrainStorage("PlayerStorage")
            .AddMemoryGrainStorage("RoomStorage")
            .AddMemoryGrainStorage("MatchmakingStorage");
    });

    // 使用Serilog，确保捕获所有Information级别日志
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();
    builder.Logging.SetMinimumLevel(LogLevel.Information);

    // 配置JWT认证设置
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
    
    // 验证JWT设置
    var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
    if (jwtSettings == null)
    {
        Log.Fatal("JWT配置缺失，请检查appsettings.json中的JwtSettings节");
        return 1;
    }

    var (isValidJwt, jwtErrors) = jwtSettings.Validate();
    if (!isValidJwt)
    {
        Log.Fatal("JWT配置验证失败: {Errors}", string.Join(", ", jwtErrors));
        return 1;
    }

    // 注册JWT服务
    builder.Services.AddSingleton<JwtService>();
    
    // 注册限流服务
    builder.Services.AddRateLimit(options =>
    {
        // 配置默认限流策略
        options.DefaultPolicy = new RateLimitPolicy
        {
            Name = "Default",
            WindowSize = TimeSpan.FromMinutes(1),
            MaxRequests = 60, // 每分钟60请求
            GlobalMaxRequests = 10000 // 全局每分钟10000请求
        };

        // 配置端点特定策略
        options.EndpointPolicies = new Dictionary<string, RateLimitPolicy>
        {
            ["LoginAsync"] = new RateLimitPolicy
            {
                Name = "Login",
                WindowSize = TimeSpan.FromMinutes(1),
                MaxRequests = 10, // 登录更严格
                GlobalMaxRequests = 1000
            },
            ["RegisterAsync"] = new RateLimitPolicy
            {
                Name = "Register", 
                WindowSize = TimeSpan.FromMinutes(5),
                MaxRequests = 3, // 注册最严格
                GlobalMaxRequests = 100
            },
            ["GetPlayerInfoAsync"] = new RateLimitPolicy
            {
                Name = "PlayerInfo",
                WindowSize = TimeSpan.FromMinutes(1),
                MaxRequests = 120, // 查询类API较宽松
                GlobalMaxRequests = 5000
            }
        };

        // 白名单客户端（如果有）
        options.WhitelistedClients = new List<string>();

        // 启用限流和日志
        options.EnableRateLimit = true;
        options.EnableLogging = true;
    });
    
    // 配置JWT Bearer认证
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = jwtSettings.ValidateIssuer,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = jwtSettings.ValidateAudience,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = jwtSettings.ValidateLifetime,
                ValidateIssuerSigningKey = jwtSettings.ValidateIssuerSigningKey,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = jwtSettings.ClockSkew
            };

            // 配置JWT事件处理
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Log.Warning("JWT认证失败: {Error}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var playerId = context.Principal?.Identity?.Name;
                    Log.Debug("JWT令牌验证成功，玩家: {PlayerId}", playerId);
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Log.Debug("JWT认证挑战: {Error}, {ErrorDescription}", 
                        context.Error, context.ErrorDescription);
                    return Task.CompletedTask;
                }
            };
        });

    // 添加授权策略
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireAuthenticatedUser", policy =>
            policy.RequireAuthenticatedUser());
        
        options.AddPolicy("PlayerOnly", policy =>
            policy.RequireAuthenticatedUser()
                  .RequireClaim(ClaimTypes.NameIdentifier));
    });

    // 注册JWT认证Filter (暂时禁用，待MagicOnion API兼容性修复)
    // builder.Services.AddSingleton<Wind.Server.Filters.JwtAuthorizationFilter>();
    
    // 添加MagicOnion服务 (基于Context7文档)
    builder.Services.AddMagicOnion();
    // builder.Services.AddMagicOnion(options =>
    // {
    //     // 添加JWT认证过滤器到所有服务 (暂时禁用)
    //     options.GlobalFilters.Add<Wind.Server.Filters.JwtAuthorizationFilter>();
    // });
    
    // 配置Orleans MessagePack序列化器 (正确位置)
    builder.Services.AddSerializer(serializerBuilder => serializerBuilder.AddMessagePackSerializer());

    // 添加健康检查
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    Log.Information("Orleans Silo配置完成，端口: Silo=11111, Gateway=30000");
    Log.Information("Orleans存储配置: PlayerStorage, RoomStorage, MatchmakingStorage (Memory)");
    Log.Information("MagicOnion服务已添加到DI容器，将自动发现PlayerService等服务");
    Log.Information("健康检查服务已配置");

    var app = builder.Build();

    // 启用限流中间件 (在认证之前)
    app.UseRateLimit();
    
    // 启用认证和授权中间件 (必须在MagicOnion之前)
    app.UseAuthentication();
    app.UseAuthorization();

    // 映射MagicOnion服务端点 (基于Context7文档)
    app.MapMagicOnionService();
    
    // 映射健康检查端点
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });
    
    // 添加根路径信息端点
    app.MapGet("/", () => new
    {
        Service = "Wind游戏服务器",
        Version = "v1.3",
        Technology = "Orleans + MagicOnion",
        Timestamp = DateTime.UtcNow,
        Endpoints = new
        {
            Health = "/health",
            Ready = "/health/ready", 
            Live = "/health/live",
            MagicOnion = "gRPC on port 5271"
        }
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Orleans Silo启动失败");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
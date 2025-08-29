using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;
using Orleans.Serialization;
using Orleans.Configuration;
using Orleans.Persistence;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;
using MagicOnion;
using MagicOnion.Server;
using MessagePack;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IO.Compression;
using Grpc.AspNetCore.Server;
using Grpc.Net.Compression;
using Wind.Shared.Auth;
using Wind.Server.Services;
using Wind.Server.Middleware;
using Wind.Server.Configuration;
using Wind.Server.Extensions;

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

    // 配置Kestrel支持gRPC和HTTP (MagicOnion + REST API)
    builder.WebHost.ConfigureKestrel(options =>
    {
        // 配置HTTP/2性能优化 (基于MagicOnion最佳实践)
        var http2 = options.Limits.Http2;
        http2.InitialConnectionWindowSize = 1024 * 1024 * 2; // 2 MB 连接窗口
        http2.InitialStreamWindowSize = 1024 * 1024; // 1 MB 流窗口
        http2.MaxStreamsPerConnection = 1000; // 每连接最大并发流
        
        // gRPC端口 - 仅HTTP/2 (生产环境推荐使用HTTPS)
        options.ListenLocalhost(5271, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        });
        
        // HTTP/1.1端口 - 用于健康检查、管理API等
        options.ListenLocalhost(5270, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
        });
        
        // 混合协议端口 - 开发和调试使用
        options.ListenLocalhost(5272, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
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
                gatewayPort: 30000);
            
        // 配置存储：使用Redis持久化存储 (Orleans 9.2.1兼容配置)
        Log.Information("配置Redis存储模式");
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379,password=windgame123";
        
        // 先添加基本的内存存储作为默认存储（避免启动错误）
        siloBuilder.AddMemoryGrainStorage("Default");
        
        // 配置Orleans Redis存储（基于技术研究记录案例3的正确解决方案）
        Log.Information("在SiloBuilder中配置Redis存储，连接字符串: {ConnectionString}", redisConnectionString.Replace("password=windgame123", "password=***"));
        try
        {
            siloBuilder
                .AddRedisGrainStorage("PlayerStorage", options => {
                    var playerConfigOptions = ConfigurationOptions.Parse(redisConnectionString);
                    playerConfigOptions.DefaultDatabase = 0;
                    playerConfigOptions.AbortOnConnectFail = false;
                    options.ConfigurationOptions = playerConfigOptions;
                    Log.Information("PlayerStorage Redis配置完成: DB=0");
                })
                .AddRedisGrainStorage("RoomStorage", options => {
                    var roomConfigOptions = ConfigurationOptions.Parse(redisConnectionString);
                    roomConfigOptions.DefaultDatabase = 1;
                    roomConfigOptions.AbortOnConnectFail = false;
                    options.ConfigurationOptions = roomConfigOptions;
                    Log.Information("RoomStorage Redis配置完成: DB=1");
                })
                .AddRedisGrainStorage("MatchmakingStorage", options => {
                    var matchmakingConfigOptions = ConfigurationOptions.Parse(redisConnectionString);
                    matchmakingConfigOptions.DefaultDatabase = 2;
                    matchmakingConfigOptions.AbortOnConnectFail = false;
                    options.ConfigurationOptions = matchmakingConfigOptions;
                    Log.Information("MatchmakingStorage Redis配置完成: DB=2");
                });
            Log.Information("✅ Orleans Redis存储配置成功 - 使用ConfigurationOptions方式");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Orleans Redis存储配置失败，将使用内存存储");
            // 如果Redis配置失败，回退到内存存储
            siloBuilder
                .AddMemoryGrainStorage("PlayerStorage")
                .AddMemoryGrainStorage("RoomStorage") 
                .AddMemoryGrainStorage("MatchmakingStorage");
        }
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
    
    // 配置Redis缓存策略 (替换原有的Redis配置)
    Log.Information("配置Redis缓存策略服务");
    try
    {
        builder.Services.AddRedisCacheStrategy(builder.Configuration);
        Log.Information("Redis缓存策略服务注册成功");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Redis缓存策略配置失败，将跳过Redis功能");
    }
    
    // 配置分布式锁服务
    Log.Information("配置分布式锁服务");
    try
    {
        builder.Services.AddDistributedLock(options =>
        {
            options.DefaultExpiryMinutes = 5;
            options.DefaultTimeoutSeconds = 30;
            options.RetryIntervalMs = 100;
            options.KeyPrefix = "Wind:Lock:";
            options.EnableAutoRenewal = true;
            options.AutoRenewalRatio = 0.7;
            options.EnableStatistics = true;
            options.MaxRetries = 100;
        });
        Log.Information("分布式锁服务注册成功");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "分布式锁配置失败，将跳过分布式锁功能");
    }
    
    
    // Orleans Redis存储配置已移动到SiloBuilder中（见下方UseOrleans配置）
    Log.Information("Orleans Redis Grain存储将在SiloBuilder中配置");
    
    // 配置MongoDB连接
    builder.Services.Configure<MongoDbOptions>(builder.Configuration.GetSection(MongoDbOptions.SectionName));
    
    // 验证MongoDB设置
    var mongoDbOptions = builder.Configuration.GetSection(MongoDbOptions.SectionName).Get<MongoDbOptions>();
    if (mongoDbOptions != null)
    {
        try
        {
            mongoDbOptions.Validate();
            Log.Information("MongoDB配置验证通过: {DatabaseName}", mongoDbOptions.DatabaseName);
        }
        catch (Exception ex)
        {
            Log.Warning("MongoDB配置验证失败: {Error}，将跳过MongoDB集成", ex.Message);
            mongoDbOptions = null;
        }
    }
    else
    {
        Log.Warning("MongoDB配置缺失，将跳过MongoDB集成");
    }
    
    // 注册MongoDB连接管理器和持久化服务
    if (mongoDbOptions != null)
    {
        builder.Services.AddSingleton<MongoDbConnectionManager>();
        builder.Services.AddSingleton<MongoIndexManager>();
        
        // 注册持久化服务
        builder.Services.AddSingleton<IPlayerPersistenceService, PlayerPersistenceService>();
        builder.Services.AddSingleton<IRoomPersistenceService, RoomPersistenceService>();
        builder.Services.AddSingleton<IGameRecordPersistenceService, GameRecordPersistenceService>();
        
        // 注册分布式事务服务
        builder.Services.AddSingleton<DistributedTransactionService>();
        
        // 注册冲突检测服务
        builder.Services.AddSingleton<ConflictDetectionService>();
        
        Log.Information("MongoDB连接管理器、持久化服务、分布式事务服务和冲突检测服务已注册");
    }
    
    // 验证数据同步设置 (简化，基于新的Redis缓存策略)
    var dataSyncOptions = builder.Configuration.GetSection(DataSyncOptions.SectionName).Get<DataSyncOptions>();
    if (dataSyncOptions != null && mongoDbOptions != null)
    {
        try
        {
            dataSyncOptions.Validate();
            builder.Services.Configure<DataSyncOptions>(builder.Configuration.GetSection(DataSyncOptions.SectionName));
            builder.Services.AddSingleton<IDataSyncService, DataSyncService>();
            builder.Services.AddSingleton<DataSyncManager>();
            Log.Information("数据同步服务已注册: 默认策略={DefaultStrategy}", dataSyncOptions.SyncStrategy.DefaultStrategy);
        }
        catch (Exception ex)
        {
            Log.Warning("数据同步配置验证失败: {Error}，将跳过数据同步功能", ex.Message);
        }
    }
    else
    {
        Log.Warning("数据同步功能需要Redis缓存策略和MongoDB同时可用，已跳过注册");
    }
    
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
    
    // 添加gRPC核心服务 (MagicOnion的基础)
    builder.Services.AddGrpc(options =>
    {
        // gRPC全局配置
        options.MaxReceiveMessageSize = 1024 * 1024 * 4; // 4MB 最大接收消息
        options.MaxSendMessageSize = 1024 * 1024 * 4; // 4MB 最大发送消息
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.CompressionProviders.Add(new GzipCompressionProvider(System.IO.Compression.CompressionLevel.Optimal));
        options.ResponseCompressionLevel = System.IO.Compression.CompressionLevel.Optimal;
        options.ResponseCompressionAlgorithm = "gzip";
    });
    
    // 添加MagicOnion服务 (基于Context7文档)
    builder.Services.AddMagicOnion(options =>
    {
        // 添加JWT认证过滤器到所有服务 (暂时禁用，待兼容性修复后启用)
        // options.GlobalFilters.Add<Wind.Server.Filters.JwtAuthorizationFilter>();
        
        Log.Information("MagicOnion服务配置完成");
    });
    
    // 配置MessagePack全局序列化选项 (MagicOnion + Orleans 2025最佳实践)
    var resolver = MessagePack.Resolvers.CompositeResolver.Create(
        // MagicOnion生成的Resolver (优先级最高，客户端启用)
        // MagicOnionGeneratedClientInitializer.Resolver, // 客户端时启用
        
        // Orleans和属性相关Resolver
        MessagePack.Resolvers.AttributeFormatterResolver.Instance,
        MessagePack.Resolvers.BuiltinResolver.Instance,
        
        // 高性能Resolver (服务端推荐配置)
        MessagePack.Resolvers.PrimitiveObjectResolver.Instance,
        
        // 标准Resolver (最后fallback)
        MessagePack.Resolvers.StandardResolver.Instance
    );
    
    MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard
        .WithResolver(resolver)
        .WithSecurity(MessagePackSecurity.UntrustedData) // CVE-2020-5234 安全补丁
        .WithCompression(MessagePackCompression.Lz4BlockArray); // 启用压缩
    
    Log.Information("MessagePack序列化器全局配置完成: 支持MagicOnion + Orleans，启用安全模式和压缩");
    
    // 配置Orleans MessagePack序列化器 (基于Microsoft.Orleans.Serialization.MessagePack 9.2.1)
    builder.Services.AddSerializer(serializerBuilder => 
    {
        serializerBuilder.AddMessagePackSerializer();
        Log.Information("Orleans MessagePack序列化器已注册");
    });

    // 配置连接池管理服务
    builder.Services.Configure<ConnectionPoolOptions>(options =>
    {
        options.MaxPoolSize = 10000;
        options.ConnectionTimeoutSeconds = 300;
        options.IdleTimeoutSeconds = 120;
        options.CleanupIntervalSeconds = 60;
        options.EnableConnectionMetrics = true;
        options.EnableHealthCheck = true;
    });
    builder.Services.AddSingleton<ConnectionPoolManager>();
    Log.Information("连接池管理服务注册完成");

    // 配置负载均衡服务
    builder.Services.Configure<LoadBalancingOptions>(options =>
    {
        options.DefaultStrategy = LoadBalancingStrategy.RoundRobin;
        options.DefaultWeight = 100;
        options.NodeTimeoutSeconds = 30;
        options.HealthCheckIntervalSeconds = 10;
        options.EnableHealthCheck = true;
        options.EnableMetrics = true;
        options.MaxRetries = 3;
        options.RetryDelayMilliseconds = 1000;
    });
    builder.Services.AddSingleton<LoadBalancingService>();
    
    // 注册消息路由服务 - v1.3网络通信层
    builder.Services.AddSingleton<Wind.Shared.Services.IMessageRouter, MessageRouterService>();
    Log.Information("消息路由服务已注册 - 智能路由和广播系统就绪");
    
    // 配置连接预热服务
    builder.Services.Configure<ConnectionWarmupOptions>(options =>
    {
        options.WarmupConnectionCount = 10;
        options.WarmupTimeoutMs = 5000;
        options.MaxRetryCount = 3;
        options.RetryDelayMs = 1000;
        options.ServerAddress = "http://localhost:5271";
        options.EnableWarmup = true;
        options.StartDelayMs = 2000;
    });
    builder.Services.AddHostedService<ConnectionWarmupService>();
    
    // 配置请求批处理服务
    builder.Services.Configure<RequestBatchingOptions>(options =>
    {
        options.MaxBatchSize = 50;
        options.MaxWaitTimeMs = 10;
        options.EnableBatching = true;
        options.MaxQueueSize = 1000;
        options.WorkerThreadCount = Environment.ProcessorCount;
        options.StatsUpdateIntervalMs = 5000;
    });
    builder.Services.AddHostedService<RequestBatchingService>();
    
    Log.Information("负载均衡服务注册完成");
    Log.Information("连接预热服务已注册 - 启动时自动预热{Count}个gRPC连接", 10);
    Log.Information("请求批处理服务已注册 - 批大小: {BatchSize}, 等待时间: {WaitTime}ms", 50, 10);

    // 添加健康检查
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    Log.Information("Orleans Silo配置完成，端口: Silo=11111, Gateway=30000");
    Log.Information("Orleans存储配置: PlayerStorage(DB:0), RoomStorage(DB:1), MatchmakingStorage(DB:2) -> Redis");
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
    
    Log.Information("MagicOnion服务端点已映射 - 支持Unary API和Streaming Hub");
    
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
    
    // 添加Redis缓存健康检查端点
    app.MapGet("/health/redis", async (IServiceProvider serviceProvider) =>
    {
        var (isHealthy, status, details) = await serviceProvider.GetRedisCacheHealthAsync();
        return Results.Ok(new
        {
            status = status,
            healthy = isHealthy,
            details = details,
            timestamp = DateTime.UtcNow
        });
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
            Redis = "/health/redis",
            ConnectionWarmup = "/warmup/status",
            RequestBatching = "/batching/status",
            MagicOnion = "gRPC on port 5271"
        }
    });
    
    // 添加连接预热状态监控端点
    app.MapGet("/warmup/status", (IServiceProvider serviceProvider) =>
    {
        try
        {
            var warmupService = serviceProvider.GetService<ConnectionWarmupService>();
            if (warmupService == null)
            {
                return Results.Ok(new
                {
                    Status = "Service Not Available",
                    Message = "连接预热服务未注册"
                });
            }

            var stats = warmupService.GetStats();
            return Results.Ok(new
            {
                Status = "OK",
                Timestamp = DateTime.UtcNow,
                WarmupEnabled = stats.IsWarmupEnabled,
                TotalConnections = stats.TotalWarmupConnections,
                TargetConnections = stats.TargetWarmupConnections,
                SuccessRate = $"{stats.WarmupSuccessRate:F1}%"
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"获取预热状态失败: {ex.Message}");
        }
    });

    // 添加请求批处理状态监控端点
    app.MapGet("/batching/status", (IServiceProvider serviceProvider) =>
    {
        try
        {
            var batchingService = serviceProvider.GetService<RequestBatchingService>();
            if (batchingService == null)
            {
                return Results.Ok(new
                {
                    Status = "Service Not Available",
                    Message = "请求批处理服务未注册"
                });
            }

            var stats = batchingService.GetStatistics();
            return Results.Ok(new
            {
                Status = "OK",
                Timestamp = DateTime.UtcNow,
                TotalRequestsProcessed = stats.TotalRequestsProcessed,
                TotalBatchesProcessed = stats.TotalBatchesProcessed,
                CurrentQueueSize = stats.CurrentQueueSize,
                AverageBatchSize = Math.Round(stats.AverageBatchSize, 2),
                AverageWaitTime = Math.Round(stats.AverageWaitTime, 2),
                ThroughputImprovement = $"{stats.ThroughputImprovement:F1}%",
                LastStatsUpdate = stats.LastStatsUpdate
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"获取批处理状态失败: {ex.Message}");
        }
    });

    // 启动前测试Redis连接
    Log.Information("测试Redis连接状态...");
    var redisConnectionOk = await app.Services.TestRedisConnectionAsync();
    if (redisConnectionOk)
    {
        Log.Information("✅ Redis连接测试成功，缓存策略已就绪");
    }
    else
    {
        Log.Warning("⚠️ Redis连接测试失败，但服务仍将启动（降级运行模式）");
    }

    // 初始化MongoDB索引
    var mongoIndexManager = app.Services.GetService<MongoIndexManager>();
    if (mongoIndexManager != null)
    {
        try
        {
            Log.Information("正在创建MongoDB索引...");
            await mongoIndexManager.CreateAllIndexesAsync();
            Log.Information("✅ MongoDB索引创建完成");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ MongoDB索引创建失败，但服务仍将启动");
        }
    }

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
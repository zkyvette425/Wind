using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;
using Orleans.Serialization;
using Serilog;
using MagicOnion;
using MagicOnion.Server;
using MessagePack;

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

    // 添加MagicOnion服务 (基于Context7文档)
    builder.Services.AddMagicOnion();
    
    // 配置Orleans MessagePack序列化器 (正确位置)
    builder.Services.AddSerializer(serializerBuilder => serializerBuilder.AddMessagePackSerializer());

    // 添加健康检查
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    Log.Information("Orleans Silo配置完成，端口: Silo=11111, Gateway=30000");
    Log.Information("Orleans存储配置: PlayerStorage, RoomStorage, MatchmakingStorage (Memory)");
    Log.Information("MagicOnion服务已添加到DI容器");
    Log.Information("健康检查服务已配置");

    var app = builder.Build();

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
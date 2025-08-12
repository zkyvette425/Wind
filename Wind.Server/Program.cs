using System.Net;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;
using Serilog;
using MagicOnion;
using MagicOnion.Server;

// 配置Serilog日志
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
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
                gatewayPort: 30000);
    });

    // 使用Serilog，确保捕获所有Information级别日志
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();
    builder.Logging.SetMinimumLevel(LogLevel.Information);

    // 添加MagicOnion服务 (基于Context7文档)
    builder.Services.AddMagicOnion();

    Log.Information("Orleans Silo配置完成，端口: Silo=11111, Gateway=30000");
    Log.Information("MagicOnion服务已添加到DI容器");

    var app = builder.Build();

    // 映射MagicOnion服务端点 (基于Context7文档)
    app.MapMagicOnionService();
    
    // 添加根路径信息端点
    app.MapGet("/", () => "Wind游戏服务器 - Orleans + MagicOnion");

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
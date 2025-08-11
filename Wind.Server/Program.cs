using System.Net;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;
using Serilog;

// 配置Serilog日志
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("启动Orleans Silo宿主...");

    // 使用现代化的Orleans宿主模式
    var builder = Host.CreateApplicationBuilder(args)
        .UseOrleans(siloBuilder =>
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

    Log.Information("Orleans Silo配置完成，端口: Silo=11111, Gateway=30000");

    // TODO: 在v1.2阶段将添加MagicOnion服务配置

    var host = builder.Build();
    await host.RunAsync();
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
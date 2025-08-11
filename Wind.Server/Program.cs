using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

// 配置Serilog日志
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// 使用Serilog
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// TODO: 在v1.0阶段将添加Orleans Silo配置
// TODO: 在v1.0阶段将添加MagicOnion服务配置

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// TODO: 配置Orleans和MagicOnion路由

app.Run();

Log.CloseAndFlush();
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.Json;
using Wind.Application;
using Wind.Infrastructure;
using Wind.Server.Hubs;
using Wind.Core.Interfaces;
using Wind.Core.Services;
using Wind.Core.Network;

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

builder.Services.AddControllers();

// 添加应用层服务
builder.Services.AddApplication();

// 添加基础设施层服务
builder.Services.AddInfrastructure("Data Source=game.db");

// 添加SignalR服务，用于实时游戏通信
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    });

// 添加核心服务依赖注入
builder.Services.AddSingleton<IMessageRouter, MessageRouter>();
builder.Services.AddSingleton<IProtocolParser, JsonProtocolParser>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// 配置SignalR hub路由
app.MapHub<GameHub>("/gameHub");

app.Run();

using MagicOnion;
using MagicOnion.Server;
using Wind.Shared.Services;
using Wind.GrainInterfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Orleans;

namespace Wind.Grains.Services;

/// <summary>
/// 测试RPC服务实现 - 演示MagicOnion与Orleans Grain的协作
/// 基于Context7文档的ServiceBase<T>继承模式
/// </summary>
public class TestService : ServiceBase<ITestService>, ITestService
{
    private readonly ILogger<TestService> _logger;

    public TestService(ILogger<TestService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 简单加法运算实现
    /// </summary>
    public async UnaryResult<int> AddAsync(int x, int y)
    {
        _logger.LogInformation("MagicOnion RPC调用 - AddAsync: {X} + {Y}", x, y);
        
        var result = x + y;
        
        // 演示与Orleans Grain的协作 - 可以在这里调用HelloGrain
        var grainFactory = Context.ServiceProvider.GetRequiredService<IGrainFactory>();
        var helloGrain = grainFactory.GetGrain<IHelloGrain>("test-grain");
        var greetingMessage = await helloGrain.SayHelloAsync("MagicOnion");
        
        _logger.LogInformation("从Orleans Grain获得问候: {Greeting}", greetingMessage);
        
        return result;
    }

    /// <summary>
    /// 字符串回显实现
    /// </summary>
    public async UnaryResult<string> EchoAsync(string message)
    {
        _logger.LogInformation("MagicOnion RPC调用 - EchoAsync: {Message}", message);
        
        // 演示异步处理
        await Task.Delay(10);
        
        return $"Echo from Wind Server: {message}";
    }

    /// <summary>
    /// 获取服务器信息实现
    /// </summary>
    public async UnaryResult<string> GetServerInfoAsync()
    {
        _logger.LogInformation("MagicOnion RPC调用 - GetServerInfoAsync");
        
        var serverInfo = $"Wind游戏服务器 - Orleans + MagicOnion, 时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        
        return await Task.FromResult(serverInfo);
    }
}
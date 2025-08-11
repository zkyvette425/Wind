using Orleans;
using Wind.GrainInterfaces;
using Microsoft.Extensions.Logging;

namespace Wind.Grains;

/// <summary>
/// HelloGrain实现，用于验证Orleans基础环境是否正常工作
/// </summary>
public class HelloGrain : Grain, IHelloGrain
{
    private readonly ILogger<HelloGrain> _logger;

    public HelloGrain(ILogger<HelloGrain> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 实现问候方法，记录日志并返回问候信息
    /// </summary>
    /// <param name="name">要问候的姓名</param>
    /// <returns>Orleans问候消息</returns>
    public Task<string> SayHelloAsync(string name)
    {
        _logger.LogInformation("HelloGrain收到问候请求，姓名: {Name}", name);
        
        var response = $"Hello {name} from Orleans! Grain ID: {this.GetPrimaryKeyString()}";
        
        _logger.LogInformation("HelloGrain响应: {Response}", response);
        
        return Task.FromResult(response);
    }
}
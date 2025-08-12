using Grpc.Net.Client;
using MagicOnion.Client;
using Wind.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Wind.GrainInterfaces;

namespace Wind.Client.Services;

/// <summary>
/// Wind游戏客户端 - 集成MagicOnion RPC和Orleans Grain调用
/// 演示客户端如何同时使用两种通信方式
/// </summary>
public class WindGameClient : IDisposable
{
    private readonly ILogger<WindGameClient> _logger;
    private GrpcChannel? _grpcChannel;
    private IClusterClient? _orleansClient;
    private IHost? _orleansHost;
    private ITestService? _testService;
    private bool _disposed = false;

    /// <summary>
    /// 服务器地址配置
    /// </summary>
    public class ServerConfig
    {
        public string GrpcAddress { get; set; } = "http://localhost:5271";
        public string OrleansGatewayAddress { get; set; } = "127.0.0.1";
        public int OrleansGatewayPort { get; set; } = 30000;
    }

    public WindGameClient(ILogger<WindGameClient> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 连接到Wind游戏服务器 (MagicOnion + Orleans)
    /// </summary>
    public async Task<bool> ConnectAsync(ServerConfig config)
    {
        return await ConnectWithRetryAsync(config, maxRetries: 3, delayBetweenRetries: TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// 带重试机制的连接方法
    /// </summary>
    private async Task<bool> ConnectWithRetryAsync(ServerConfig config, int maxRetries, TimeSpan delayBetweenRetries)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("正在连接到Wind游戏服务器... (尝试 {Attempt}/{MaxRetries})", attempt, maxRetries);

                // 1. 建立gRPC连接用于MagicOnion RPC调用
                _grpcChannel = GrpcChannel.ForAddress(config.GrpcAddress);
                _testService = MagicOnionClient.Create<ITestService>(_grpcChannel);
                
                // 测试gRPC连接
                await TestConnectionAsync();
                _logger.LogInformation("MagicOnion gRPC连接已建立: {Address}", config.GrpcAddress);

                // 2. 建立Orleans客户端连接用于直接Grain调用  
                var hostBuilder = Host.CreateDefaultBuilder()
                    .UseOrleansClient(clientBuilder =>
                    {
                        clientBuilder.UseLocalhostClustering(gatewayPort: config.OrleansGatewayPort);
                    })
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Warning);
                    });
                
                _orleansHost = hostBuilder.Build();
                await _orleansHost.StartAsync();
                _orleansClient = _orleansHost.Services.GetRequiredService<IClusterClient>();
                
                _logger.LogInformation("Orleans客户端连接已建立: {GatewayAddress}:{GatewayPort}", 
                    config.OrleansGatewayAddress, config.OrleansGatewayPort);

                _logger.LogInformation("Wind游戏客户端连接成功！");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "连接尝试 {Attempt}/{MaxRetries} 失败", attempt, maxRetries);
                
                // 清理已建立的连接
                await CleanupPartialConnectionsAsync();

                if (attempt == maxRetries)
                {
                    _logger.LogError("所有连接尝试失败，放弃连接");
                    return false;
                }
                
                _logger.LogInformation("等待 {Delay} 后重试...", delayBetweenRetries);
                await Task.Delay(delayBetweenRetries);
            }
        }
        
        return false;
    }

    /// <summary>
    /// 测试连接是否正常
    /// </summary>
    private async Task TestConnectionAsync()
    {
        if (_testService == null)
            throw new InvalidOperationException("gRPC服务未初始化");

        // 发送一个简单的测试请求验证连接
        await _testService.GetServerInfoAsync();
    }

    /// <summary>
    /// 清理部分建立的连接
    /// </summary>
    private async Task CleanupPartialConnectionsAsync()
    {
        try
        {
            if (_orleansHost != null)
            {
                await _orleansHost.StopAsync();
                _orleansHost.Dispose();
                _orleansHost = null;
                _orleansClient = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "清理Orleans客户端连接时发生错误");
        }

        try
        {
            if (_grpcChannel != null)
            {
                _grpcChannel.Dispose();
                _grpcChannel = null;
                _testService = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "清理gRPC连接时发生错误");
        }
    }

    /// <summary>
    /// 测试MagicOnion RPC调用
    /// </summary>
    public async Task<int> TestAddAsync(int x, int y)
    {
        if (_testService == null)
            throw new InvalidOperationException("客户端未连接到服务器");

        _logger.LogInformation("调用MagicOnion RPC: AddAsync({X}, {Y})", x, y);
        var result = await _testService.AddAsync(x, y);
        _logger.LogInformation("MagicOnion RPC结果: {Result}", result);
        return result;
    }

    /// <summary>
    /// 测试MagicOnion字符串回显
    /// </summary>
    public async Task<string> TestEchoAsync(string message)
    {
        if (_testService == null)
            throw new InvalidOperationException("客户端未连接到服务器");

        _logger.LogInformation("调用MagicOnion RPC: EchoAsync({Message})", message);
        var result = await _testService.EchoAsync(message);
        _logger.LogInformation("MagicOnion RPC结果: {Result}", result);
        return result;
    }

    /// <summary>
    /// 获取服务器信息
    /// </summary>
    public async Task<string> GetServerInfoAsync()
    {
        if (_testService == null)
            throw new InvalidOperationException("客户端未连接到服务器");

        _logger.LogInformation("调用MagicOnion RPC: GetServerInfoAsync()");
        var result = await _testService.GetServerInfoAsync();
        _logger.LogInformation("服务器信息: {Info}", result);
        return result;
    }

    /// <summary>
    /// 测试Orleans Grain直接调用
    /// </summary>
    public async Task<string> TestOrleansGrainAsync(string name)
    {
        if (_orleansClient == null)
            throw new InvalidOperationException("Orleans客户端未连接到服务器");

        _logger.LogInformation("直接调用Orleans Grain: HelloGrain({Name})", name);
        
        try
        {
            // 直接调用Orleans Grain
            var helloGrain = _orleansClient.GetGrain<IHelloGrain>(name);
            var greeting = await helloGrain.SayHelloAsync(name);
            
            _logger.LogInformation("Orleans Grain调用结果: {Greeting}", greeting);
            return greeting;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orleans Grain调用失败");
            throw;
        }
    }

    /// <summary>
    /// 测试Orleans和MagicOnion混合调用
    /// </summary>
    public async Task<(string OrleansResult, int MagicOnionResult)> TestHybridCallAsync(string name, int x, int y)
    {
        _logger.LogInformation("测试混合调用模式: Orleans + MagicOnion");
        
        // 并行调用Orleans Grain和MagicOnion RPC
        var orleansTask = TestOrleansGrainAsync(name);
        var magicOnionTask = TestAddAsync(x, y);
        
        await Task.WhenAll(orleansTask, magicOnionTask);
        
        var results = (orleansTask.Result, magicOnionTask.Result);
        _logger.LogInformation("混合调用完成: Orleans=\"{OrleansResult}\", MagicOnion={MagicOnionResult}", 
            results.Item1, results.Item2);
            
        return results;
    }

    /// <summary>
    /// 断开连接并释放资源
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            _logger.LogInformation("正在断开Wind游戏客户端连接...");

            // 断开Orleans客户端连接
            if (_orleansHost != null)
            {
                await _orleansHost.StopAsync();
                _orleansHost.Dispose();
                _orleansHost = null;
                _orleansClient = null;
                _logger.LogInformation("Orleans客户端连接已断开");
            }

            if (_grpcChannel != null)
            {
                _grpcChannel.Dispose();
                _grpcChannel = null;
                _logger.LogInformation("MagicOnion gRPC连接已断开");
            }

            _logger.LogInformation("Wind游戏客户端已断开连接");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "断开连接时发生错误");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisconnectAsync().GetAwaiter().GetResult();
            _disposed = true;
        }
    }
}
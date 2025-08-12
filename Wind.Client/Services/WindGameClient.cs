using Grpc.Net.Client;
using MagicOnion.Client;
using Wind.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Configuration;
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
    // Orleans客户端暂时注释掉，专注MagicOnion测试
    // private IClusterClient? _orleansSaidClient;
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
        try
        {
            _logger.LogInformation("正在连接到Wind游戏服务器...");

            // 1. 建立gRPC连接用于MagicOnion RPC调用
            _grpcChannel = GrpcChannel.ForAddress(config.GrpcAddress);
            _testService = MagicOnionClient.Create<ITestService>(_grpcChannel);
            
            _logger.LogInformation("MagicOnion gRPC连接已建立: {Address}", config.GrpcAddress);

            // 2. Orleans客户端连接暂时跳过，专注MagicOnion测试
            // 在v1.2阶段先验证MagicOnion功能，Orleans直接调用在后续版本完善
            _logger.LogInformation("MagicOnion集成测试模式 - 跳过Orleans直接客户端连接");

            _logger.LogInformation("Wind游戏客户端连接成功！");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接到Wind游戏服务器失败");
            return false;
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
    /// 测试Orleans Grain直接调用 (v1.2暂时跳过，通过MagicOnion间接调用)
    /// </summary>
    public async Task<string> TestOrleansGrainAsync(string name)
    {
        // v1.2阶段暂时通过MagicOnion RPC间接调用Orleans Grain
        // 这样可以测试MagicOnion服务内部调用Grain的功能
        _logger.LogInformation("通过MagicOnion RPC间接调用Orleans Grain (AddAsync会调用HelloGrain)");
        
        // 调用AddAsync，它内部会调用Orleans HelloGrain
        var result = await TestAddAsync(1, 1);
        return $"通过MagicOnion间接调用Orleans Grain成功，计算结果: {result}";
    }

    /// <summary>
    /// 断开连接并释放资源
    /// </summary>
    public void Disconnect()
    {
        try
        {
            _logger.LogInformation("正在断开Wind游戏客户端连接...");

            // Orleans客户端在v1.2阶段暂时跳过
            // if (_orleansSaidClient != null) { ... }

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
            Disconnect();
            _disposed = true;
        }
    }
}
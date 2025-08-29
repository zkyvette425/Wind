using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Grpc.Net.Client;
using MagicOnion.Client;
using Wind.Shared.Services;

namespace Wind.Server.Services;

/// <summary>
/// 连接预热服务配置
/// </summary>
public class ConnectionWarmupOptions
{
    /// <summary>
    /// 预热连接数量
    /// </summary>
    public int WarmupConnectionCount { get; set; } = 10;

    /// <summary>
    /// 预热超时时间（毫秒）
    /// </summary>
    public int WarmupTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 预热重试次数
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// 预热重试间隔（毫秒）
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// 服务器地址
    /// </summary>
    public string ServerAddress { get; set; } = "http://localhost:5271";

    /// <summary>
    /// 是否启用预热
    /// </summary>
    public bool EnableWarmup { get; set; } = true;

    /// <summary>
    /// 预热延迟启动时间（毫秒）
    /// </summary>
    public int StartDelayMs { get; set; } = 2000;
}

/// <summary>
/// gRPC连接预热服务
/// 在服务启动时预建立连接，减少首次调用延迟
/// </summary>
public class ConnectionWarmupService : IHostedService
{
    private readonly ILogger<ConnectionWarmupService> _logger;
    private readonly ConnectionWarmupOptions _options;
    private readonly List<GrpcChannel> _warmupChannels = new();
    private readonly List<IGameService> _warmupClients = new();
    private CancellationTokenSource _cancellationTokenSource = new();

    public ConnectionWarmupService(
        ILogger<ConnectionWarmupService> logger,
        IOptions<ConnectionWarmupOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 服务启动时执行预热
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableWarmup)
        {
            _logger.LogInformation("连接预热已禁用，跳过预热过程");
            return;
        }

        _logger.LogInformation("开始gRPC连接预热，目标连接数: {Count}", _options.WarmupConnectionCount);
        
        // 延迟启动，等待服务完全启动
        await Task.Delay(_options.StartDelayMs, cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await WarmupConnectionsAsync(cancellationToken);
            
            stopwatch.Stop();
            _logger.LogInformation("gRPC连接预热完成，耗时: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "gRPC连接预热失败，耗时: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// 服务停止时清理预热连接
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("清理预热连接");
        
        _cancellationTokenSource.Cancel();

        // 清理预热的gRPC通道
        var disposeTasks = _warmupChannels.Select(async channel =>
        {
            try
            {
                await channel.ShutdownAsync();
                channel.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理预热连接时发生错误");
            }
        });

        await Task.WhenAll(disposeTasks);
        
        _warmupChannels.Clear();
        _warmupClients.Clear();
        
        _logger.LogInformation("预热连接清理完成");
    }

    /// <summary>
    /// 执行连接预热
    /// </summary>
    private async Task WarmupConnectionsAsync(CancellationToken cancellationToken)
    {
        var warmupTasks = new List<Task>();

        for (int i = 0; i < _options.WarmupConnectionCount; i++)
        {
            warmupTasks.Add(WarmupSingleConnectionAsync(i, cancellationToken));
        }

        await Task.WhenAll(warmupTasks);
    }

    /// <summary>
    /// 预热单个连接
    /// </summary>
    private async Task WarmupSingleConnectionAsync(int connectionIndex, CancellationToken cancellationToken)
    {
        int retryCount = 0;
        
        while (retryCount <= _options.MaxRetryCount)
        {
            try
            {
                await WarmupConnectionWithTimeoutAsync(connectionIndex, cancellationToken);
                return; // 成功则退出重试循环
            }
            catch (Exception ex) when (retryCount < _options.MaxRetryCount)
            {
                retryCount++;
                _logger.LogWarning(ex, "连接{Index}预热失败，第{Retry}次重试", connectionIndex, retryCount);
                
                if (retryCount <= _options.MaxRetryCount)
                {
                    await Task.Delay(_options.RetryDelayMs, cancellationToken);
                }
            }
        }

        _logger.LogError("连接{Index}预热最终失败，已达最大重试次数", connectionIndex);
    }

    /// <summary>
    /// 带超时的连接预热
    /// </summary>
    private async Task WarmupConnectionWithTimeoutAsync(int connectionIndex, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.WarmupTimeoutMs);

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // 创建gRPC通道
            var channel = GrpcChannel.ForAddress(_options.ServerAddress, new GrpcChannelOptions
            {
                HttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // 开发环境忽略SSL证书验证
                },
                MaxReceiveMessageSize = 4 * 1024 * 1024, // 4MB
                MaxSendMessageSize = 4 * 1024 * 1024, // 4MB
                MaxRetryAttempts = 3
            });

            // 创建MagicOnion客户端
            var client = MagicOnionClient.Create<IGameService>(channel);

            // 执行预热调用（心跳检查）
            await client.HealthCheckAsync();

            stopwatch.Stop();

            // 保存预热的连接以备后续使用
            lock (_warmupChannels)
            {
                _warmupChannels.Add(channel);
                _warmupClients.Add(client);
            }

            _logger.LogDebug("连接{Index}预热成功，耗时: {ElapsedMs}ms", 
                connectionIndex, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"连接{connectionIndex}预热超时 ({_options.WarmupTimeoutMs}ms)");
        }
    }

    /// <summary>
    /// 获取预热连接统计信息
    /// </summary>
    public ConnectionWarmupStats GetStats()
    {
        lock (_warmupChannels)
        {
            return new ConnectionWarmupStats
            {
                TotalWarmupConnections = _warmupChannels.Count,
                TargetWarmupConnections = _options.WarmupConnectionCount,
                IsWarmupEnabled = _options.EnableWarmup,
                WarmupSuccessRate = _options.WarmupConnectionCount > 0 
                    ? (double)_warmupChannels.Count / _options.WarmupConnectionCount * 100 
                    : 0
            };
        }
    }
}

/// <summary>
/// 连接预热统计信息
/// </summary>
public class ConnectionWarmupStats
{
    /// <summary>
    /// 实际预热连接数
    /// </summary>
    public int TotalWarmupConnections { get; set; }

    /// <summary>
    /// 目标预热连接数
    /// </summary>
    public int TargetWarmupConnections { get; set; }

    /// <summary>
    /// 是否启用预热
    /// </summary>
    public bool IsWarmupEnabled { get; set; }

    /// <summary>
    /// 预热成功率
    /// </summary>
    public double WarmupSuccessRate { get; set; }
}
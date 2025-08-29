using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using MagicOnion;
using Wind.Shared.Services;

namespace Wind.Server.Services;

/// <summary>
/// 请求批处理服务配置
/// </summary>
public class RequestBatchingOptions
{
    /// <summary>
    /// 批处理最大请求数量
    /// </summary>
    public int MaxBatchSize { get; set; } = 50;

    /// <summary>
    /// 批处理最大等待时间（毫秒）
    /// </summary>
    public int MaxWaitTimeMs { get; set; } = 10;

    /// <summary>
    /// 是否启用批处理
    /// </summary>
    public bool EnableBatching { get; set; } = true;

    /// <summary>
    /// 批处理队列最大容量
    /// </summary>
    public int MaxQueueSize { get; set; } = 1000;

    /// <summary>
    /// 批处理工作线程数量
    /// </summary>
    public int WorkerThreadCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// 统计信息更新间隔（毫秒）
    /// </summary>
    public int StatsUpdateIntervalMs { get; set; } = 5000;
}

/// <summary>
/// 批处理请求项
/// </summary>
public class BatchRequestItem<TRequest, TResponse> : IBatchRequestItem
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public TRequest Request { get; set; } = default!;
    public TaskCompletionSource<TResponse> CompletionSource { get; set; } = new();
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public CancellationToken CancellationToken { get; set; }
    public string RequestType { get; set; } = string.Empty;

    public void SetResult(object? result)
    {
        if (result is TResponse response)
        {
            CompletionSource.SetResult(response);
        }
        else
        {
            CompletionSource.SetException(new InvalidOperationException("结果类型不匹配"));
        }
    }

    public void SetException(Exception exception)
    {
        CompletionSource.SetException(exception);
    }

    public void SetCanceled()
    {
        CompletionSource.SetCanceled();
    }
}

/// <summary>
/// 批处理统计信息
/// </summary>
public class BatchingStatistics
{
    public int TotalRequestsProcessed { get; set; }
    public int TotalBatchesProcessed { get; set; }
    public int CurrentQueueSize { get; set; }
    public double AverageBatchSize { get; set; }
    public double AverageWaitTime { get; set; }
    public double ThroughputImprovement { get; set; }
    public DateTime LastStatsUpdate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 请求批处理服务
/// 将小请求合并成批处理，减少网络往返次数，提升吞吐量
/// </summary>
public class RequestBatchingService : BackgroundService
{
    private readonly ILogger<RequestBatchingService> _logger;
    private readonly RequestBatchingOptions _options;
    private readonly ConcurrentQueue<IBatchRequestItem> _requestQueue = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Timer _statsTimer;
    private readonly BatchingStatistics _statistics = new();
    private readonly object _statsLock = new object();

    public RequestBatchingService(
        ILogger<RequestBatchingService> logger,
        IOptions<RequestBatchingOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        _statsTimer = new Timer(UpdateStatistics, null, 
            TimeSpan.FromMilliseconds(_options.StatsUpdateIntervalMs),
            TimeSpan.FromMilliseconds(_options.StatsUpdateIntervalMs));
    }

    /// <summary>
    /// 提交批处理请求
    /// </summary>
    public async Task<TResponse> SubmitBatchRequestAsync<TRequest, TResponse>(
        TRequest request,
        Func<IEnumerable<TRequest>, Task<IEnumerable<TResponse>>> batchProcessor,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableBatching)
        {
            // 如果未启用批处理，直接处理单个请求
            var singleResult = await batchProcessor(new[] { request });
            return singleResult.First();
        }

        var requestItem = new BatchRequestItem<TRequest, TResponse>
        {
            Request = request,
            CancellationToken = cancellationToken,
            RequestType = typeof(TRequest).Name
        };

        // 检查队列容量
        if (_requestQueue.Count >= _options.MaxQueueSize)
        {
            _logger.LogWarning("批处理队列已满，拒绝新请求。队列大小: {QueueSize}", _requestQueue.Count);
            throw new InvalidOperationException("批处理队列已满，请稍后重试");
        }

        _requestQueue.Enqueue(requestItem);
        _logger.LogDebug("请求已加入批处理队列: {RequestId}, 队列大小: {QueueSize}", 
            requestItem.RequestId, _requestQueue.Count);

        try
        {
            return await requestItem.CompletionSource.Task;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("批处理请求被取消: {RequestId}", requestItem.RequestId);
            throw;
        }
    }

    /// <summary>
    /// 后台批处理工作循环
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableBatching)
        {
            _logger.LogInformation("请求批处理服务已禁用");
            return;
        }

        _logger.LogInformation("启动请求批处理服务，批处理大小: {BatchSize}, 等待时间: {WaitTime}ms, 工作线程: {ThreadCount}",
            _options.MaxBatchSize, _options.MaxWaitTimeMs, _options.WorkerThreadCount);

        // 启动多个工作线程
        var workerTasks = new Task[_options.WorkerThreadCount];
        for (int i = 0; i < _options.WorkerThreadCount; i++)
        {
            int workerId = i;
            workerTasks[i] = ProcessBatchWorkerAsync(workerId, stoppingToken);
        }

        try
        {
            await Task.WhenAll(workerTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批处理工作线程异常");
            throw;
        }
    }

    /// <summary>
    /// 单个批处理工作线程
    /// </summary>
    private async Task ProcessBatchWorkerAsync(int workerId, CancellationToken stoppingToken)
    {
        _logger.LogDebug("批处理工作线程{WorkerId}已启动", workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchCycleAsync(workerId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批处理工作线程{WorkerId}发生错误", workerId);
                await Task.Delay(1000, stoppingToken); // 错误恢复延迟
            }
        }

        _logger.LogDebug("批处理工作线程{WorkerId}已停止", workerId);
    }

    /// <summary>
    /// 单次批处理周期
    /// </summary>
    private async Task ProcessBatchCycleAsync(int workerId, CancellationToken stoppingToken)
    {
        var batch = new List<IBatchRequestItem>();
        var stopwatch = Stopwatch.StartNew();

        // 收集批处理请求
        var waitEndTime = DateTime.UtcNow.AddMilliseconds(_options.MaxWaitTimeMs);
        
        while (DateTime.UtcNow < waitEndTime && 
               batch.Count < _options.MaxBatchSize && 
               !stoppingToken.IsCancellationRequested)
        {
            if (_requestQueue.TryDequeue(out var item))
            {
                if (!item.CancellationToken.IsCancellationRequested)
                {
                    batch.Add(item);
                }
                else
                {
                    item.SetCanceled();
                }
            }
            else if (batch.Count == 0)
            {
                // 如果没有请求，短暂等待
                await Task.Delay(1, stoppingToken);
            }
            else
            {
                // 有请求但未达到批处理大小，等待更多请求
                await Task.Delay(1, stoppingToken);
            }
        }

        if (batch.Count == 0)
        {
            return; // 没有请求需要处理
        }

        stopwatch.Stop();
        var waitTime = stopwatch.ElapsedMilliseconds;

        _logger.LogDebug("工作线程{WorkerId}收集到{Count}个请求，等待时间: {WaitTime}ms", 
            workerId, batch.Count, waitTime);

        // 按请求类型分组处理
        var groupedBatch = batch.GroupBy(item => item.RequestType).ToList();
        
        foreach (var group in groupedBatch)
        {
            await ProcessBatchGroupAsync(workerId, group.ToList(), waitTime);
        }

        // 更新统计信息
        lock (_statsLock)
        {
            _statistics.TotalRequestsProcessed += batch.Count;
            _statistics.TotalBatchesProcessed++;
            _statistics.AverageBatchSize = (double)_statistics.TotalRequestsProcessed / _statistics.TotalBatchesProcessed;
            _statistics.AverageWaitTime = (_statistics.AverageWaitTime + waitTime) / 2;
        }
    }

    /// <summary>
    /// 处理同类型请求批次
    /// </summary>
    private async Task ProcessBatchGroupAsync(int workerId, List<IBatchRequestItem> batch, double waitTime)
    {
        if (batch.Count == 0) return;

        var requestType = batch.First().RequestType;
        _logger.LogDebug("工作线程{WorkerId}处理{RequestType}批次，请求数: {Count}", 
            workerId, requestType, batch.Count);

        try
        {
            // 这里需要根据具体的请求类型调用相应的批处理器
            // 实际实现中会通过策略模式或工厂模式来处理不同类型的请求
            await ProcessSpecificBatchTypeAsync(batch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批处理组处理失败: {RequestType}, 请求数: {Count}", requestType, batch.Count);
            
            // 标记所有请求失败
            foreach (var item in batch)
            {
                item.SetException(ex);
            }
        }
    }

    /// <summary>
    /// 处理特定类型的批次（占位符实现）
    /// </summary>
    private async Task ProcessSpecificBatchTypeAsync(List<IBatchRequestItem> batch)
    {
        // 这是一个占位符实现
        // 实际应用中，这里会调用具体的批处理逻辑
        await Task.Delay(1); // 模拟处理时间

        // 为每个请求设置结果（这里只是示例）
        foreach (var item in batch)
        {
            item.SetResult(null); // 实际应设置正确的结果
        }
    }

    /// <summary>
    /// 更新统计信息
    /// </summary>
    private void UpdateStatistics(object? state)
    {
        lock (_statsLock)
        {
            _statistics.CurrentQueueSize = _requestQueue.Count;
            _statistics.LastStatsUpdate = DateTime.UtcNow;
            
            // 计算吞吐量提升（批处理 vs 单个处理）
            if (_statistics.TotalBatchesProcessed > 0)
            {
                var theoreticalIndividualRequests = _statistics.TotalRequestsProcessed;
                var actualBatches = _statistics.TotalBatchesProcessed;
                _statistics.ThroughputImprovement = ((double)theoreticalIndividualRequests / actualBatches - 1) * 100;
            }
        }

        if (_statistics.TotalRequestsProcessed > 0)
        {
            _logger.LogDebug("批处理统计 - 处理请求: {Requests}, 批次: {Batches}, 平均批大小: {AvgBatch:F2}, 吞吐量提升: {Improvement:F1}%",
                _statistics.TotalRequestsProcessed, _statistics.TotalBatchesProcessed, 
                _statistics.AverageBatchSize, _statistics.ThroughputImprovement);
        }
    }

    /// <summary>
    /// 获取批处理统计信息
    /// </summary>
    public BatchingStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new BatchingStatistics
            {
                TotalRequestsProcessed = _statistics.TotalRequestsProcessed,
                TotalBatchesProcessed = _statistics.TotalBatchesProcessed,
                CurrentQueueSize = _statistics.CurrentQueueSize,
                AverageBatchSize = _statistics.AverageBatchSize,
                AverageWaitTime = _statistics.AverageWaitTime,
                ThroughputImprovement = _statistics.ThroughputImprovement,
                LastStatsUpdate = _statistics.LastStatsUpdate
            };
        }
    }

    /// <summary>
    /// 资源清理
    /// </summary>
    public override void Dispose()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource已经被释放，忽略异常
        }
        
        _statsTimer?.Dispose();
        _cancellationTokenSource?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// 批处理请求项接口
/// </summary>
public interface IBatchRequestItem
{
    string RequestId { get; }
    string RequestType { get; }
    CancellationToken CancellationToken { get; }
    void SetResult(object? result);
    void SetException(Exception exception);
    void SetCanceled();
}


using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Wind.Server.Services;

/// <summary>
/// 自适应超时服务配置
/// </summary>
public class AdaptiveTimeoutOptions
{
    /// <summary>
    /// 基础超时时间（毫秒）
    /// </summary>
    public int BaseTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 最小超时时间（毫秒）
    /// </summary>
    public int MinTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// 最大超时时间（毫秒）
    /// </summary>
    public int MaxTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// 响应时间历史记录数量
    /// </summary>
    public int HistorySize { get; set; } = 100;

    /// <summary>
    /// 超时调整系数
    /// </summary>
    public double AdjustmentFactor { get; set; } = 1.5;

    /// <summary>
    /// 性能评估间隔（毫秒）
    /// </summary>
    public int EvaluationIntervalMs { get; set; } = 10000;

    /// <summary>
    /// 是否启用自适应超时
    /// </summary>
    public bool EnableAdaptiveTimeout { get; set; } = true;

    /// <summary>
    /// 网络质量评估窗口大小
    /// </summary>
    public int NetworkQualityWindowSize { get; set; } = 50;

    /// <summary>
    /// 超时阈值调整敏感度
    /// </summary>
    public double TimeoutSensitivity { get; set; } = 0.8;
}

/// <summary>
/// 操作类型枚举
/// </summary>
public enum OperationType
{
    /// <summary>
    /// 游戏服务调用
    /// </summary>
    GameService,
    
    /// <summary>
    /// 房间操作
    /// </summary>
    RoomOperation,
    
    /// <summary>
    /// 匹配系统
    /// </summary>
    Matchmaking,
    
    /// <summary>
    /// 玩家操作
    /// </summary>
    PlayerOperation,
    
    /// <summary>
    /// 数据库操作
    /// </summary>
    DatabaseOperation,
    
    /// <summary>
    /// 缓存操作
    /// </summary>
    CacheOperation
}

/// <summary>
/// 网络质量指标
/// </summary>
public class NetworkQuality
{
    /// <summary>
    /// 平均响应时间（毫秒）
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// 响应时间标准差
    /// </summary>
    public double ResponseTimeStdDev { get; set; }

    /// <summary>
    /// 超时率
    /// </summary>
    public double TimeoutRate { get; set; }

    /// <summary>
    /// 错误率
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// 网络质量分数 (0-100)
    /// </summary>
    public double QualityScore { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 操作性能记录
/// </summary>
public class OperationMetrics
{
    /// <summary>
    /// 操作类型
    /// </summary>
    public OperationType OperationType { get; set; }

    /// <summary>
    /// 响应时间历史记录
    /// </summary>
    public Queue<double> ResponseTimes { get; set; } = new();

    /// <summary>
    /// 超时次数
    /// </summary>
    public int TimeoutCount { get; set; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 错误次数
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// 当前推荐超时时间（毫秒）
    /// </summary>
    public int RecommendedTimeoutMs { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 自适应超时统计信息
/// </summary>
public class AdaptiveTimeoutStatistics
{
    /// <summary>
    /// 总操作次数
    /// </summary>
    public int TotalOperations { get; set; }

    /// <summary>
    /// 超时优化次数
    /// </summary>
    public int TimeoutOptimizations { get; set; }

    /// <summary>
    /// 平均性能提升百分比
    /// </summary>
    public double PerformanceImprovement { get; set; }

    /// <summary>
    /// 当前网络质量
    /// </summary>
    public NetworkQuality CurrentNetworkQuality { get; set; } = new();

    /// <summary>
    /// 各操作类型的推荐超时时间
    /// </summary>
    public Dictionary<OperationType, int> RecommendedTimeouts { get; set; } = new();

    /// <summary>
    /// 最后统计更新时间
    /// </summary>
    public DateTime LastStatsUpdate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 自适应超时服务
/// 根据网络状况和历史性能数据动态调整操作超时时间
/// </summary>
public class AdaptiveTimeoutService : BackgroundService
{
    private readonly ILogger<AdaptiveTimeoutService> _logger;
    private readonly AdaptiveTimeoutOptions _options;
    private readonly ConcurrentDictionary<OperationType, OperationMetrics> _operationMetrics = new();
    private readonly Timer _evaluationTimer;
    private readonly AdaptiveTimeoutStatistics _statistics = new();
    private readonly object _statsLock = new object();

    public AdaptiveTimeoutService(
        ILogger<AdaptiveTimeoutService> logger,
        IOptions<AdaptiveTimeoutOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // 初始化各操作类型的默认指标
        foreach (OperationType operationType in Enum.GetValues<OperationType>())
        {
            _operationMetrics[operationType] = new OperationMetrics
            {
                OperationType = operationType,
                RecommendedTimeoutMs = _options.BaseTimeoutMs
            };
        }

        _evaluationTimer = new Timer(EvaluateAndAdjustTimeouts, null,
            TimeSpan.FromMilliseconds(_options.EvaluationIntervalMs),
            TimeSpan.FromMilliseconds(_options.EvaluationIntervalMs));
    }

    /// <summary>
    /// 记录操作结果
    /// </summary>
    public void RecordOperation(OperationType operationType, double responseTimeMs, bool isSuccess, bool isTimeout)
    {
        if (!_options.EnableAdaptiveTimeout) return;

        if (!_operationMetrics.TryGetValue(operationType, out var metrics))
        {
            metrics = new OperationMetrics
            {
                OperationType = operationType,
                RecommendedTimeoutMs = _options.BaseTimeoutMs
            };
            _operationMetrics[operationType] = metrics;
        }

        lock (metrics)
        {
            // 记录响应时间
            if (isSuccess && !isTimeout)
            {
                metrics.ResponseTimes.Enqueue(responseTimeMs);
                if (metrics.ResponseTimes.Count > _options.HistorySize)
                {
                    metrics.ResponseTimes.Dequeue();
                }
            }

            // 更新计数器
            if (isSuccess)
                metrics.SuccessCount++;
            else if (isTimeout)
                metrics.TimeoutCount++;
            else
                metrics.ErrorCount++;

            metrics.LastUpdate = DateTime.UtcNow;
        }

        // 更新统计信息
        lock (_statsLock)
        {
            _statistics.TotalOperations++;
        }

        _logger.LogDebug("记录操作结果: {OperationType}, 响应时间: {ResponseTime}ms, 成功: {Success}, 超时: {Timeout}",
            operationType, responseTimeMs, isSuccess, isTimeout);
    }

    /// <summary>
    /// 获取推荐的超时时间
    /// </summary>
    public int GetRecommendedTimeout(OperationType operationType)
    {
        if (!_options.EnableAdaptiveTimeout)
        {
            return _options.BaseTimeoutMs;
        }

        if (_operationMetrics.TryGetValue(operationType, out var metrics))
        {
            return Math.Max(_options.MinTimeoutMs, 
                   Math.Min(_options.MaxTimeoutMs, metrics.RecommendedTimeoutMs));
        }

        return _options.BaseTimeoutMs;
    }

    /// <summary>
    /// 获取超时配置（支持CancellationToken创建）
    /// </summary>
    public CancellationTokenSource CreateTimeoutToken(OperationType operationType)
    {
        var timeoutMs = GetRecommendedTimeout(operationType);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
        
        _logger.LogDebug("为{OperationType}创建超时令牌，超时时间: {Timeout}ms", operationType, timeoutMs);
        return cts;
    }

    /// <summary>
    /// 后台服务执行
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableAdaptiveTimeout)
        {
            _logger.LogInformation("自适应超时服务已禁用");
            return;
        }

        _logger.LogInformation("启动自适应超时服务，评估间隔: {Interval}ms", _options.EvaluationIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.EvaluationIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("自适应超时服务已停止");
    }

    /// <summary>
    /// 评估并调整超时时间
    /// </summary>
    private void EvaluateAndAdjustTimeouts(object? state)
    {
        try
        {
            var networkQuality = CalculateNetworkQuality();
            var optimizationCount = 0;

            foreach (var (operationType, metrics) in _operationMetrics)
            {
                lock (metrics)
                {
                    if (metrics.ResponseTimes.Count < 10) continue; // 需要足够的数据

                    var oldTimeout = metrics.RecommendedTimeoutMs;
                    var newTimeout = CalculateOptimalTimeout(metrics, networkQuality);

                    if (Math.Abs(newTimeout - oldTimeout) > oldTimeout * 0.1) // 变化超过10%才调整
                    {
                        metrics.RecommendedTimeoutMs = newTimeout;
                        optimizationCount++;

                        _logger.LogInformation("调整{OperationType}超时时间: {OldTimeout}ms -> {NewTimeout}ms",
                            operationType, oldTimeout, newTimeout);
                    }
                }
            }

            // 更新统计信息
            lock (_statsLock)
            {
                _statistics.TimeoutOptimizations += optimizationCount;
                _statistics.CurrentNetworkQuality = networkQuality;
                _statistics.RecommendedTimeouts = _operationMetrics.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => kvp.Value.RecommendedTimeoutMs);
                _statistics.LastStatsUpdate = DateTime.UtcNow;

                // 计算性能提升
                if (_statistics.TotalOperations > 0)
                {
                    var baselineTimeouts = _operationMetrics.Values.Sum(m => _options.BaseTimeoutMs);
                    var optimizedTimeouts = _operationMetrics.Values.Sum(m => m.RecommendedTimeoutMs);
                    _statistics.PerformanceImprovement = ((double)(baselineTimeouts - optimizedTimeouts) / baselineTimeouts) * 100;
                }
            }

            if (optimizationCount > 0)
            {
                _logger.LogInformation("完成超时优化，调整了{Count}个操作类型的超时设置", optimizationCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "评估和调整超时时间时发生错误");
        }
    }

    /// <summary>
    /// 计算网络质量
    /// </summary>
    private NetworkQuality CalculateNetworkQuality()
    {
        var allResponseTimes = new List<double>();
        var totalOperations = 0;
        var totalTimeouts = 0;
        var totalErrors = 0;

        foreach (var metrics in _operationMetrics.Values)
        {
            lock (metrics)
            {
                allResponseTimes.AddRange(metrics.ResponseTimes);
                totalOperations += metrics.SuccessCount + metrics.TimeoutCount + metrics.ErrorCount;
                totalTimeouts += metrics.TimeoutCount;
                totalErrors += metrics.ErrorCount;
            }
        }

        if (allResponseTimes.Count == 0)
        {
            return new NetworkQuality
            {
                AverageResponseTime = _options.BaseTimeoutMs,
                ResponseTimeStdDev = 0,
                TimeoutRate = 0,
                ErrorRate = 0,
                QualityScore = 80 // 默认质量分数
            };
        }

        var avgResponseTime = allResponseTimes.Average();
        var stdDev = Math.Sqrt(allResponseTimes.Average(rt => Math.Pow(rt - avgResponseTime, 2)));
        var timeoutRate = totalOperations > 0 ? (double)totalTimeouts / totalOperations : 0;
        var errorRate = totalOperations > 0 ? (double)totalErrors / totalOperations : 0;

        // 计算质量分数 (0-100)
        var qualityScore = 100.0;
        qualityScore -= Math.Min(30, avgResponseTime / 1000 * 10); // 响应时间惩罚
        qualityScore -= Math.Min(25, stdDev / 1000 * 15); // 稳定性惩罚
        qualityScore -= timeoutRate * 30; // 超时率惩罚
        qualityScore -= errorRate * 20; // 错误率惩罚

        return new NetworkQuality
        {
            AverageResponseTime = avgResponseTime,
            ResponseTimeStdDev = stdDev,
            TimeoutRate = timeoutRate,
            ErrorRate = errorRate,
            QualityScore = Math.Max(0, qualityScore)
        };
    }

    /// <summary>
    /// 计算最优超时时间
    /// </summary>
    private int CalculateOptimalTimeout(OperationMetrics metrics, NetworkQuality networkQuality)
    {
        var responseTimes = metrics.ResponseTimes.ToArray();
        if (responseTimes.Length == 0) return _options.BaseTimeoutMs;

        // 基于P95响应时间计算
        Array.Sort(responseTimes);
        var p95Index = (int)(responseTimes.Length * 0.95);
        var p95ResponseTime = responseTimes[Math.Min(p95Index, responseTimes.Length - 1)];

        // 应用调整系数
        var baseTimeout = p95ResponseTime * _options.AdjustmentFactor;

        // 根据网络质量调整
        var qualityMultiplier = networkQuality.QualityScore < 50 ? 1.3 : 
                               networkQuality.QualityScore < 70 ? 1.1 : 1.0;
        
        var adjustedTimeout = baseTimeout * qualityMultiplier;

        // 应用边界限制
        return (int)Math.Max(_options.MinTimeoutMs, 
               Math.Min(_options.MaxTimeoutMs, adjustedTimeout));
    }

    /// <summary>
    /// 获取自适应超时统计信息
    /// </summary>
    public AdaptiveTimeoutStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new AdaptiveTimeoutStatistics
            {
                TotalOperations = _statistics.TotalOperations,
                TimeoutOptimizations = _statistics.TimeoutOptimizations,
                PerformanceImprovement = _statistics.PerformanceImprovement,
                CurrentNetworkQuality = new NetworkQuality
                {
                    AverageResponseTime = _statistics.CurrentNetworkQuality.AverageResponseTime,
                    ResponseTimeStdDev = _statistics.CurrentNetworkQuality.ResponseTimeStdDev,
                    TimeoutRate = _statistics.CurrentNetworkQuality.TimeoutRate,
                    ErrorRate = _statistics.CurrentNetworkQuality.ErrorRate,
                    QualityScore = _statistics.CurrentNetworkQuality.QualityScore,
                    LastUpdate = _statistics.CurrentNetworkQuality.LastUpdate
                },
                RecommendedTimeouts = new Dictionary<OperationType, int>(_statistics.RecommendedTimeouts),
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
            _evaluationTimer?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Timer已经被释放，忽略异常
        }
        
        base.Dispose();
    }
}
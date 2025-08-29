using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Wind.Server.Services;
using Xunit;

namespace Wind.Tests.Services
{
    /// <summary>
    /// AdaptiveTimeoutService测试套件
    /// 验证自适应超时机制的正确性和性能
    /// </summary>
    public class AdaptiveTimeoutServiceTests : IDisposable
    {
        private readonly Mock<ILogger<AdaptiveTimeoutService>> _mockLogger;
        private readonly AdaptiveTimeoutService _timeoutService;
        private readonly AdaptiveTimeoutOptions _options;

        public AdaptiveTimeoutServiceTests()
        {
            _mockLogger = new Mock<ILogger<AdaptiveTimeoutService>>();
            
            _options = new AdaptiveTimeoutOptions
            {
                BaseTimeoutMs = 5000,
                MinTimeoutMs = 1000,
                MaxTimeoutMs = 30000,
                HistorySize = 10, // 较小的值便于测试
                AdjustmentFactor = 1.5,
                EvaluationIntervalMs = 1000, // 较快的评估间隔
                EnableAdaptiveTimeout = true,
                NetworkQualityWindowSize = 5,
                TimeoutSensitivity = 0.8
            };

            var optionsWrapper = Options.Create(_options);
            _timeoutService = new AdaptiveTimeoutService(_mockLogger.Object, optionsWrapper);
        }

        [Fact]
        public void GetStatistics_ShouldReturnInitialStatistics()
        {
            // Act
            var stats = _timeoutService.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalOperations);
            Assert.Equal(0, stats.TimeoutOptimizations);
            Assert.Equal(0, stats.PerformanceImprovement);
            Assert.NotNull(stats.CurrentNetworkQuality);
            Assert.NotNull(stats.RecommendedTimeouts);
        }

        [Fact]
        public void GetRecommendedTimeout_ShouldReturnBaseTimeoutForNewOperationType()
        {
            // Act
            var timeout = _timeoutService.GetRecommendedTimeout(OperationType.GameService);

            // Assert
            Assert.Equal(_options.BaseTimeoutMs, timeout);
        }

        [Fact]
        public void GetRecommendedTimeout_WhenDisabled_ShouldReturnBaseTimeout()
        {
            // Arrange
            var disabledOptions = new AdaptiveTimeoutOptions { EnableAdaptiveTimeout = false };
            var disabledService = new AdaptiveTimeoutService(_mockLogger.Object, Options.Create(disabledOptions));

            // Act
            var timeout = disabledService.GetRecommendedTimeout(OperationType.GameService);

            // Assert
            Assert.Equal(disabledOptions.BaseTimeoutMs, timeout);
            disabledService.Dispose();
        }

        [Fact]
        public void CreateTimeoutToken_ShouldReturnCancellationTokenSource()
        {
            // Act
            using var cts = _timeoutService.CreateTimeoutToken(OperationType.GameService);

            // Assert
            Assert.NotNull(cts);
            Assert.False(cts.Token.IsCancellationRequested);
        }

        [Fact]
        public void RecordOperation_ShouldUpdateStatistics()
        {
            // Arrange
            var operationType = OperationType.GameService;
            var responseTime = 1500.0;

            // Act
            _timeoutService.RecordOperation(operationType, responseTime, isSuccess: true, isTimeout: false);
            
            // Assert
            var stats = _timeoutService.GetStatistics();
            Assert.Equal(1, stats.TotalOperations);
        }

        [Fact]
        public void RecordOperation_WithTimeoutAndError_ShouldUpdateCounters()
        {
            // Arrange
            var operationType = OperationType.DatabaseOperation;

            // Act
            _timeoutService.RecordOperation(operationType, 5000, isSuccess: false, isTimeout: true);
            _timeoutService.RecordOperation(operationType, 0, isSuccess: false, isTimeout: false); // Error without timeout

            // Assert
            var stats = _timeoutService.GetStatistics();
            Assert.Equal(2, stats.TotalOperations);
        }

        [Fact]
        public void RecordOperation_WhenDisabled_ShouldNotAffectStatistics()
        {
            // Arrange
            var disabledOptions = new AdaptiveTimeoutOptions { EnableAdaptiveTimeout = false };
            var disabledService = new AdaptiveTimeoutService(_mockLogger.Object, Options.Create(disabledOptions));

            // Act
            disabledService.RecordOperation(OperationType.GameService, 1000, true, false);

            // Assert
            var stats = disabledService.GetStatistics();
            Assert.Equal(0, stats.TotalOperations);
            
            disabledService.Dispose();
        }

        [Fact]
        public void NetworkQuality_ShouldCalculateCorrectly()
        {
            // Arrange
            var operationType = OperationType.GameService;
            
            // Act - 记录多个成功操作
            for (int i = 0; i < 5; i++)
            {
                _timeoutService.RecordOperation(operationType, 1000 + i * 100, isSuccess: true, isTimeout: false);
            }

            // Assert
            var stats = _timeoutService.GetStatistics();
            Assert.True(stats.CurrentNetworkQuality.AverageResponseTime >= 0);
            Assert.True(stats.CurrentNetworkQuality.QualityScore >= 0);
            Assert.True(stats.CurrentNetworkQuality.QualityScore <= 100);
        }

        [Fact]
        public void AdaptiveTimeoutOptions_ShouldHaveCorrectDefaults()
        {
            // Arrange & Act
            var options = new AdaptiveTimeoutOptions();

            // Assert
            Assert.Equal(5000, options.BaseTimeoutMs);
            Assert.Equal(1000, options.MinTimeoutMs);
            Assert.Equal(30000, options.MaxTimeoutMs);
            Assert.Equal(100, options.HistorySize);
            Assert.Equal(1.5, options.AdjustmentFactor);
            Assert.Equal(10000, options.EvaluationIntervalMs);
            Assert.True(options.EnableAdaptiveTimeout);
            Assert.Equal(50, options.NetworkQualityWindowSize);
            Assert.Equal(0.8, options.TimeoutSensitivity);
        }

        [Fact]
        public void NetworkQuality_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var networkQuality = new NetworkQuality();

            // Assert
            Assert.Equal(0, networkQuality.AverageResponseTime);
            Assert.Equal(0, networkQuality.ResponseTimeStdDev);
            Assert.Equal(0, networkQuality.TimeoutRate);
            Assert.Equal(0, networkQuality.ErrorRate);
            Assert.Equal(0, networkQuality.QualityScore);
            Assert.True(networkQuality.LastUpdate <= DateTime.UtcNow);
        }

        [Fact]
        public void OperationMetrics_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var metrics = new OperationMetrics
            {
                OperationType = OperationType.RoomOperation
            };

            // Assert
            Assert.Equal(OperationType.RoomOperation, metrics.OperationType);
            Assert.NotNull(metrics.ResponseTimes);
            Assert.Empty(metrics.ResponseTimes);
            Assert.Equal(0, metrics.TimeoutCount);
            Assert.Equal(0, metrics.SuccessCount);
            Assert.Equal(0, metrics.ErrorCount);
            Assert.Equal(0, metrics.RecommendedTimeoutMs);
            Assert.True(metrics.LastUpdate <= DateTime.UtcNow);
        }

        [Fact]
        public void OperationType_ShouldHaveAllExpectedValues()
        {
            // Act & Assert - 确保所有操作类型都存在
            Assert.True(Enum.IsDefined(typeof(OperationType), OperationType.GameService));
            Assert.True(Enum.IsDefined(typeof(OperationType), OperationType.RoomOperation));
            Assert.True(Enum.IsDefined(typeof(OperationType), OperationType.Matchmaking));
            Assert.True(Enum.IsDefined(typeof(OperationType), OperationType.PlayerOperation));
            Assert.True(Enum.IsDefined(typeof(OperationType), OperationType.DatabaseOperation));
            Assert.True(Enum.IsDefined(typeof(OperationType), OperationType.CacheOperation));
        }

        [Fact]
        public async Task StartAsync_WhenDisabled_ShouldCompleteQuickly()
        {
            // Arrange
            var disabledOptions = new AdaptiveTimeoutOptions { EnableAdaptiveTimeout = false };
            var disabledService = new AdaptiveTimeoutService(_mockLogger.Object, Options.Create(disabledOptions));

            // Act
            await disabledService.StartAsync(CancellationToken.None);

            // Assert - Should complete without hanging
            disabledService.Dispose();
        }

        [Fact]
        public async Task StopAsync_ShouldCompleteSuccessfully()
        {
            // Act & Assert - Should not throw
            await _timeoutService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public void Dispose_ShouldCompleteWithoutException()
        {
            // Act & Assert - Should not throw
            _timeoutService.Dispose();
        }

        public void Dispose()
        {
            _timeoutService?.Dispose();
        }
    }

    /// <summary>
    /// AdaptiveTimeoutService集成测试
    /// 验证自适应超时的实际运行效果
    /// </summary>
    public class AdaptiveTimeoutIntegrationTests : IDisposable
    {
        private readonly Mock<ILogger<AdaptiveTimeoutService>> _mockLogger;
        private readonly AdaptiveTimeoutService _timeoutService;
        private readonly AdaptiveTimeoutOptions _options;

        public AdaptiveTimeoutIntegrationTests()
        {
            _mockLogger = new Mock<ILogger<AdaptiveTimeoutService>>();
            
            _options = new AdaptiveTimeoutOptions
            {
                BaseTimeoutMs = 2000,
                MinTimeoutMs = 500,
                MaxTimeoutMs = 10000,
                HistorySize = 20,
                AdjustmentFactor = 1.3,
                EvaluationIntervalMs = 500, // 快速评估
                EnableAdaptiveTimeout = true,
                NetworkQualityWindowSize = 10,
                TimeoutSensitivity = 0.7
            };

            var optionsWrapper = Options.Create(_options);
            _timeoutService = new AdaptiveTimeoutService(_mockLogger.Object, optionsWrapper);
        }

        [Fact]
        public async Task AdaptiveTimeout_ShouldAdjustBasedOnPerformance()
        {
            // Arrange
            await _timeoutService.StartAsync(CancellationToken.None);
            var operationType = OperationType.GameService;

            // Act - 记录快速响应的操作
            for (int i = 0; i < 15; i++)
            {
                _timeoutService.RecordOperation(operationType, 300 + i * 10, isSuccess: true, isTimeout: false);
            }

            // 等待评估周期
            await Task.Delay(600);

            var initialTimeout = _timeoutService.GetRecommendedTimeout(operationType);
            
            // 继续记录更快的响应
            for (int i = 0; i < 10; i++)
            {
                _timeoutService.RecordOperation(operationType, 200 + i * 5, isSuccess: true, isTimeout: false);
            }

            await Task.Delay(600);
            var adjustedTimeout = _timeoutService.GetRecommendedTimeout(operationType);

            // Assert
            Assert.True(initialTimeout > 0);
            Assert.True(adjustedTimeout > 0);
            Assert.InRange(adjustedTimeout, _options.MinTimeoutMs, _options.MaxTimeoutMs);

            await _timeoutService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task TimeoutToken_ShouldCancelAfterTimeout()
        {
            // Arrange
            var fastTimeoutOptions = new AdaptiveTimeoutOptions
            {
                BaseTimeoutMs = 100, // 很短的超时时间
                EnableAdaptiveTimeout = true
            };
            
            var fastService = new AdaptiveTimeoutService(_mockLogger.Object, Options.Create(fastTimeoutOptions));

            // Act
            using var cts = fastService.CreateTimeoutToken(OperationType.GameService);
            var startTime = DateTime.UtcNow;

            try
            {
                await Task.Delay(500, cts.Token); // 延迟超过超时时间
                Assert.True(false, "应该已经超时");
            }
            catch (OperationCanceledException)
            {
                var elapsedTime = DateTime.UtcNow - startTime;
                Assert.True(elapsedTime.TotalMilliseconds < 300); // 应该在300ms内取消
            }

            fastService.Dispose();
        }

        [Fact]
        public async Task NetworkQualityMonitoring_ShouldReflectOperationResults()
        {
            // Arrange
            await _timeoutService.StartAsync(CancellationToken.None);
            var operationType = OperationType.DatabaseOperation;

            // Act - 记录混合的操作结果
            // 成功操作
            for (int i = 0; i < 8; i++)
            {
                _timeoutService.RecordOperation(operationType, 1000 + i * 100, isSuccess: true, isTimeout: false);
            }
            
            // 超时操作
            _timeoutService.RecordOperation(operationType, 5000, isSuccess: false, isTimeout: true);
            
            // 错误操作
            _timeoutService.RecordOperation(operationType, 0, isSuccess: false, isTimeout: false);

            // 等待评估
            await Task.Delay(600);

            // Assert
            var stats = _timeoutService.GetStatistics();
            Assert.Equal(10, stats.TotalOperations);
            Assert.True(stats.CurrentNetworkQuality.AverageResponseTime > 0);
            Assert.True(stats.CurrentNetworkQuality.TimeoutRate > 0);
            Assert.True(stats.CurrentNetworkQuality.ErrorRate > 0);
            Assert.True(stats.CurrentNetworkQuality.QualityScore < 100); // 应该因为超时和错误而降低

            await _timeoutService.StopAsync(CancellationToken.None);
        }

        public void Dispose()
        {
            _timeoutService?.Dispose();
        }
    }
}
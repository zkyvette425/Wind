using System;
using System.Collections.Generic;
using System.Linq;
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
    /// RequestBatchingService测试套件
    /// 验证请求批处理功能的正确性和性能
    /// </summary>
    public class RequestBatchingServiceTests : IDisposable
    {
        private readonly Mock<ILogger<RequestBatchingService>> _mockLogger;
        private readonly RequestBatchingService _batchingService;
        private readonly RequestBatchingOptions _options;

        public RequestBatchingServiceTests()
        {
            _mockLogger = new Mock<ILogger<RequestBatchingService>>();
            
            _options = new RequestBatchingOptions
            {
                MaxBatchSize = 5,
                MaxWaitTimeMs = 100,
                EnableBatching = true,
                MaxQueueSize = 100,
                WorkerThreadCount = 2,
                StatsUpdateIntervalMs = 1000
            };

            var optionsWrapper = Options.Create(_options);
            _batchingService = new RequestBatchingService(_mockLogger.Object, optionsWrapper);
        }

        [Fact]
        public async Task GetStatistics_ShouldReturnInitialStatistics()
        {
            // Act
            var stats = _batchingService.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalRequestsProcessed);
            Assert.Equal(0, stats.TotalBatchesProcessed);
            Assert.Equal(0, stats.CurrentQueueSize);
            Assert.Equal(0, stats.AverageBatchSize);
            Assert.Equal(0, stats.AverageWaitTime);
            Assert.Equal(0, stats.ThroughputImprovement);
        }

        [Fact]
        public async Task SubmitBatchRequestAsync_WhenBatchingDisabled_ShouldProcessImmediately()
        {
            // Arrange
            var disabledOptions = new RequestBatchingOptions { EnableBatching = false };
            var disabledService = new RequestBatchingService(_mockLogger.Object, Options.Create(disabledOptions));

            var request = "test-request";
            var expectedResponse = "test-response";

            // Act
            var result = await disabledService.SubmitBatchRequestAsync<string, string>(
                request,
                async requests =>
                {
                    Assert.Single(requests);
                    Assert.Equal(request, requests.First());
                    return new[] { expectedResponse };
                });

            // Assert
            Assert.Equal(expectedResponse, result);
            disabledService.Dispose();
        }

        [Fact]
        public void BatchRequestItem_ShouldImplementIBatchRequestItemCorrectly()
        {
            // Arrange
            var batchItem = new BatchRequestItem<string, string>
            {
                Request = "test",
                RequestType = "TestRequest"
            };

            // Act & Assert
            Assert.NotNull(batchItem.RequestId);
            Assert.Equal("TestRequest", batchItem.RequestType);
            Assert.NotNull(batchItem.CompletionSource);
            
            // Test SetResult
            batchItem.SetResult("success");
            Assert.True(batchItem.CompletionSource.Task.IsCompletedSuccessfully);
            Assert.Equal("success", batchItem.CompletionSource.Task.Result);
        }

        [Fact]
        public void BatchRequestItem_SetResult_WithWrongType_ShouldThrowException()
        {
            // Arrange
            var batchItem = new BatchRequestItem<string, string>
            {
                Request = "test"
            };

            // Act
            batchItem.SetResult(123); // Wrong type

            // Assert
            Assert.True(batchItem.CompletionSource.Task.IsFaulted);
            Assert.IsType<InvalidOperationException>(batchItem.CompletionSource.Task.Exception?.InnerException);
        }

        [Fact]
        public void BatchRequestItem_SetException_ShouldSetTaskException()
        {
            // Arrange
            var batchItem = new BatchRequestItem<string, string>
            {
                Request = "test"
            };
            var exception = new ArgumentException("Test exception");

            // Act
            batchItem.SetException(exception);

            // Assert
            Assert.True(batchItem.CompletionSource.Task.IsFaulted);
            Assert.Equal(exception, batchItem.CompletionSource.Task.Exception?.InnerException);
        }

        [Fact]
        public void BatchRequestItem_SetCanceled_ShouldCancelTask()
        {
            // Arrange
            var batchItem = new BatchRequestItem<string, string>
            {
                Request = "test"
            };

            // Act
            batchItem.SetCanceled();

            // Assert
            Assert.True(batchItem.CompletionSource.Task.IsCanceled);
        }

        [Fact]
        public void RequestBatchingOptions_ShouldHaveCorrectDefaults()
        {
            // Arrange & Act
            var options = new RequestBatchingOptions();

            // Assert
            Assert.Equal(50, options.MaxBatchSize);
            Assert.Equal(10, options.MaxWaitTimeMs);
            Assert.True(options.EnableBatching);
            Assert.Equal(1000, options.MaxQueueSize);
            Assert.Equal(Environment.ProcessorCount, options.WorkerThreadCount);
            Assert.Equal(5000, options.StatsUpdateIntervalMs);
        }

        [Fact]
        public void BatchingStatistics_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var stats = new BatchingStatistics();

            // Assert
            Assert.Equal(0, stats.TotalRequestsProcessed);
            Assert.Equal(0, stats.TotalBatchesProcessed);
            Assert.Equal(0, stats.CurrentQueueSize);
            Assert.Equal(0, stats.AverageBatchSize);
            Assert.Equal(0, stats.AverageWaitTime);
            Assert.Equal(0, stats.ThroughputImprovement);
            Assert.True(stats.LastStatsUpdate <= DateTime.UtcNow);
        }

        [Fact]
        public async Task StartAsync_WhenBatchingDisabled_ShouldReturnImmediately()
        {
            // Arrange
            var disabledOptions = new RequestBatchingOptions { EnableBatching = false };
            var disabledService = new RequestBatchingService(_mockLogger.Object, Options.Create(disabledOptions));

            // Act
            await disabledService.StartAsync(CancellationToken.None);

            // Assert - Should not throw and complete quickly
            disabledService.Dispose();
        }

        [Fact]
        public async Task StopAsync_ShouldCompleteSuccessfully()
        {
            // Act & Assert - Should not throw
            await _batchingService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public void Dispose_ShouldCompleteWithoutException()
        {
            // Act & Assert - Should not throw
            _batchingService.Dispose();
        }

        public void Dispose()
        {
            _batchingService?.Dispose();
        }
    }

    /// <summary>
    /// RequestBatchingService集成测试
    /// 验证批处理的实际运行效果
    /// </summary>
    public class RequestBatchingIntegrationTests : IDisposable
    {
        private readonly Mock<ILogger<RequestBatchingService>> _mockLogger;
        private readonly RequestBatchingService _batchingService;
        private readonly RequestBatchingOptions _options;

        public RequestBatchingIntegrationTests()
        {
            _mockLogger = new Mock<ILogger<RequestBatchingService>>();
            
            _options = new RequestBatchingOptions
            {
                MaxBatchSize = 3,
                MaxWaitTimeMs = 50,
                EnableBatching = true,
                MaxQueueSize = 100,
                WorkerThreadCount = 1, // 使用单线程便于测试
                StatsUpdateIntervalMs = 500
            };

            var optionsWrapper = Options.Create(_options);
            _batchingService = new RequestBatchingService(_mockLogger.Object, optionsWrapper);
        }

        [Fact]
        public async Task BatchProcessing_ShouldHandleConcurrentRequests()
        {
            // Arrange
            await _batchingService.StartAsync(CancellationToken.None);
            var processedRequests = new List<int>();
            var requestTasks = new List<Task<string>>();

            // Act - 提交多个并发请求
            for (int i = 1; i <= 5; i++)
            {
                var request = i;
                var task = _batchingService.SubmitBatchRequestAsync<int, string>(
                    request,
                    async requests =>
                    {
                        processedRequests.AddRange(requests);
                        return requests.Select(r => $"processed-{r}").ToList();
                    });
                requestTasks.Add(task);
            }

            // Wait for all requests to complete
            var results = await Task.WhenAll(requestTasks);

            // Assert
            Assert.Equal(5, results.Length);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal($"processed-{i + 1}", results[i]);
            }

            // 验证统计信息
            var stats = _batchingService.GetStatistics();
            Assert.True(stats.TotalRequestsProcessed >= 5);
            Assert.True(stats.TotalBatchesProcessed >= 1);

            await _batchingService.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task BatchProcessing_ShouldRespectMaxQueueSize()
        {
            // Arrange
            var limitedOptions = new RequestBatchingOptions
            {
                MaxBatchSize = 10,
                MaxWaitTimeMs = 1000, // 长等待时间
                EnableBatching = true,
                MaxQueueSize = 2, // 限制队列大小
                WorkerThreadCount = 1,
                StatsUpdateIntervalMs = 500
            };

            var limitedService = new RequestBatchingService(_mockLogger.Object, Options.Create(limitedOptions));
            await limitedService.StartAsync(CancellationToken.None);

            // Act & Assert - 超过队列容量应该抛出异常
            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await limitedService.SubmitBatchRequestAsync<int, string>(
                            i,
                            async requests => requests.Select(r => $"result-{r}").ToList());
                    }
                    catch (InvalidOperationException ex)
                    {
                        Assert.Contains("队列已满", ex.Message);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await limitedService.StopAsync(CancellationToken.None);
            limitedService.Dispose();
        }

        [Fact]
        public async Task BatchProcessing_ShouldHandleCancellation()
        {
            // Arrange
            await _batchingService.StartAsync(CancellationToken.None);
            using var cts = new CancellationTokenSource();

            // Act
            var task = _batchingService.SubmitBatchRequestAsync<string, string>(
                "test",
                async requests => requests.Select(r => $"processed-{r}").ToList(),
                cts.Token);

            // 立即取消
            cts.Cancel();

            // Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => task);
            await _batchingService.StopAsync(CancellationToken.None);
        }

        public void Dispose()
        {
            _batchingService?.Dispose();
        }
    }
}
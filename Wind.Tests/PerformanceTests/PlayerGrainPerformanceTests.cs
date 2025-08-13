using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.TestingHost;
using System.Diagnostics;
using Wind.GrainInterfaces;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Tests.TestFixtures;
using Xunit.Abstractions;

namespace Wind.Tests.PerformanceTests
{
    /// <summary>
    /// PlayerGrain 性能测试
    /// 测试系统在高并发场景下的性能表现
    /// </summary>
    public class PlayerGrainPerformanceTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly ILogger<PlayerGrainPerformanceTests> _logger;

        public PlayerGrainPerformanceTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _logger = _fixture.Cluster.ServiceProvider.GetService<ILogger<PlayerGrainPerformanceTests>>()!;
        }

        [Fact]
        public async Task PlayerGrain_Should_Handle_100_Concurrent_Operations()
        {
            // Arrange
            const int concurrentCount = 100;
            var playerIds = Enumerable.Range(0, concurrentCount)
                .Select(i => $"perf-test-player-{i:D3}")
                .ToArray();

            var stopwatch = Stopwatch.StartNew();

            // Act - 并发登录
            var loginTasks = playerIds.Select(async playerId =>
            {
                var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
                var loginRequest = new PlayerLoginRequest
                {
                    PlayerId = playerId,
                    DisplayName = $"性能测试玩家-{playerId}",
                    ClientVersion = "1.0.0",
                    Platform = "PerformanceTest",
                    DeviceId = $"perf-device-{playerId}"
                };

                var startTime = stopwatch.ElapsedMilliseconds;
                var response = await playerGrain.LoginAsync(loginRequest);
                var endTime = stopwatch.ElapsedMilliseconds;

                return new
                {
                    PlayerId = playerId,
                    Success = response.Success,
                    ResponseTime = endTime - startTime,
                    Response = response
                };
            });

            var loginResults = await Task.WhenAll(loginTasks);
            var totalLoginTime = stopwatch.ElapsedMilliseconds;

            // Assert - 验证登录性能
            Assert.All(loginResults, result => Assert.True(result.Success, $"玩家 {result.PlayerId} 登录失败"));
            
            var averageResponseTime = loginResults.Average(r => r.ResponseTime);
            var maxResponseTime = loginResults.Max(r => r.ResponseTime);
            var successRate = loginResults.Count(r => r.Success) * 100.0 / concurrentCount;

            _output.WriteLine($"✅ 并发登录测试完成:");
            _output.WriteLine($"   - 并发数量: {concurrentCount}");
            _output.WriteLine($"   - 总耗时: {totalLoginTime}ms");
            _output.WriteLine($"   - 平均响应时间: {averageResponseTime:F2}ms");
            _output.WriteLine($"   - 最大响应时间: {maxResponseTime}ms");
            _output.WriteLine($"   - 成功率: {successRate:F1}%");

            // 性能要求验证
            Assert.True(averageResponseTime < 100, $"平均响应时间 {averageResponseTime:F2}ms 超过100ms目标");
            Assert.True(maxResponseTime < 500, $"最大响应时间 {maxResponseTime}ms 超过500ms阈值");
            Assert.Equal(100.0, successRate);

            // Act - 并发操作测试
            stopwatch.Restart();
            var operationTasks = loginResults.Select(async loginResult =>
            {
                var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(loginResult.PlayerId);
                var operations = new List<Task<bool>>();

                // 心跳操作
                operations.Add(playerGrain.HeartbeatAsync());
                
                // 位置更新
                operations.Add(playerGrain.UpdatePositionAsync(new PlayerPosition 
                { 
                    X = Random.Shared.NextSingle() * 1000, 
                    Y = Random.Shared.NextSingle() * 1000, 
                    Z = 0 
                }));
                
                // 状态设置
                operations.Add(playerGrain.SetOnlineStatusAsync(PlayerOnlineStatus.Away));

                var startTime = stopwatch.ElapsedMilliseconds;
                var results = await Task.WhenAll(operations);
                var endTime = stopwatch.ElapsedMilliseconds;

                return new
                {
                    PlayerId = loginResult.PlayerId,
                    Success = results.All(r => r),
                    ResponseTime = endTime - startTime,
                    OperationCount = operations.Count
                };
            });

            var operationResults = await Task.WhenAll(operationTasks);
            var totalOperationTime = stopwatch.ElapsedMilliseconds;

            // Assert - 验证操作性能
            var operationSuccessRate = operationResults.Count(r => r.Success) * 100.0 / concurrentCount;
            var avgOperationTime = operationResults.Average(r => r.ResponseTime);
            var maxOperationTime = operationResults.Max(r => r.ResponseTime);
            var totalOperations = operationResults.Sum(r => r.OperationCount);

            _output.WriteLine($"✅ 并发操作测试完成:");
            _output.WriteLine($"   - 总操作数: {totalOperations}");
            _output.WriteLine($"   - 总耗时: {totalOperationTime}ms");
            _output.WriteLine($"   - 平均操作时间: {avgOperationTime:F2}ms");
            _output.WriteLine($"   - 最大操作时间: {maxOperationTime}ms");
            _output.WriteLine($"   - 操作成功率: {operationSuccessRate:F1}%");

            Assert.True(avgOperationTime < 150, $"平均操作时间 {avgOperationTime:F2}ms 超过150ms阈值");
            Assert.True(operationSuccessRate >= 95.0, $"操作成功率 {operationSuccessRate:F1}% 低于95%阈值");
        }

        [Fact]
        public async Task PlayerGrain_Should_Handle_1000_Sequential_Heartbeats()
        {
            // Arrange
            const int heartbeatCount = 1000;
            var playerId = "perf-heartbeat-test";
            var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);

            // 先登录
            await playerGrain.LoginAsync(new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = "心跳性能测试",
                ClientVersion = "1.0.0",
                Platform = "PerformanceTest"
            });

            // Act - 连续心跳测试
            var stopwatch = Stopwatch.StartNew();
            var successCount = 0;
            var responseTimes = new List<long>();

            for (int i = 0; i < heartbeatCount; i++)
            {
                var start = stopwatch.ElapsedMilliseconds;
                var success = await playerGrain.HeartbeatAsync();
                var end = stopwatch.ElapsedMilliseconds;
                
                if (success) successCount++;
                responseTimes.Add(end - start);
            }

            stopwatch.Stop();

            // Assert
            var totalTime = stopwatch.ElapsedMilliseconds;
            var averageTime = responseTimes.Average();
            var p95Time = responseTimes.OrderBy(x => x).Skip((int)(heartbeatCount * 0.95)).First();
            var throughput = heartbeatCount * 1000.0 / totalTime; // ops/second

            _output.WriteLine($"✅ 心跳性能测试完成:");
            _output.WriteLine($"   - 心跳次数: {heartbeatCount}");
            _output.WriteLine($"   - 成功次数: {successCount}");
            _output.WriteLine($"   - 总耗时: {totalTime}ms");
            _output.WriteLine($"   - 平均响应时间: {averageTime:F2}ms");
            _output.WriteLine($"   - P95响应时间: {p95Time}ms");
            _output.WriteLine($"   - 吞吐量: {throughput:F1} ops/sec");

            Assert.Equal(heartbeatCount, successCount);
            Assert.True(averageTime < 10, $"平均心跳时间 {averageTime:F2}ms 超过10ms目标");
            Assert.True(p95Time < 50, $"P95心跳时间 {p95Time}ms 超过50ms阈值");
            Assert.True(throughput > 100, $"心跳吞吐量 {throughput:F1} ops/sec 低于100 ops/sec目标");
        }

        [Fact]
        public async Task PlayerGrain_Should_Handle_Concurrent_Position_Updates()
        {
            // Arrange
            const int playerCount = 50;
            const int updatesPerPlayer = 20;
            var playerIds = Enumerable.Range(0, playerCount)
                .Select(i => $"pos-perf-{i:D2}")
                .ToArray();

            // 先登录所有玩家
            var loginTasks = playerIds.Select(async playerId =>
            {
                var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
                await playerGrain.LoginAsync(new PlayerLoginRequest
                {
                    PlayerId = playerId,
                    DisplayName = $"位置测试-{playerId}",
                    ClientVersion = "1.0.0",
                    Platform = "PerformanceTest"
                });
                return playerGrain;
            });

            var playerGrains = await Task.WhenAll(loginTasks);

            // Act - 并发位置更新
            var stopwatch = Stopwatch.StartNew();
            
            var updateTasks = playerGrains.SelectMany((grain, playerIndex) =>
                Enumerable.Range(0, updatesPerPlayer).Select(async updateIndex =>
                {
                    var position = new PlayerPosition
                    {
                        X = playerIndex * 100 + updateIndex,
                        Y = playerIndex * 100 + updateIndex,
                        Z = 0,
                        MapId = $"map-{playerIndex % 3}"
                    };

                    var start = stopwatch.ElapsedMilliseconds;
                    var success = await grain.UpdatePositionAsync(position);
                    var end = stopwatch.ElapsedMilliseconds;

                    return new
                    {
                        Success = success,
                        ResponseTime = end - start,
                        PlayerIndex = playerIndex,
                        UpdateIndex = updateIndex
                    };
                })
            );

            var updateResults = await Task.WhenAll(updateTasks);
            var totalTime = stopwatch.ElapsedMilliseconds;

            // Assert
            var totalUpdates = playerCount * updatesPerPlayer;
            var successCount = updateResults.Count(r => r.Success);
            var averageTime = updateResults.Average(r => r.ResponseTime);
            var maxTime = updateResults.Max(r => r.ResponseTime);
            var throughput = totalUpdates * 1000.0 / totalTime;

            _output.WriteLine($"✅ 位置更新性能测试完成:");
            _output.WriteLine($"   - 玩家数量: {playerCount}");
            _output.WriteLine($"   - 每玩家更新次数: {updatesPerPlayer}");
            _output.WriteLine($"   - 总更新次数: {totalUpdates}");
            _output.WriteLine($"   - 成功更新次数: {successCount}");
            _output.WriteLine($"   - 总耗时: {totalTime}ms");
            _output.WriteLine($"   - 平均响应时间: {averageTime:F2}ms");
            _output.WriteLine($"   - 最大响应时间: {maxTime}ms");
            _output.WriteLine($"   - 更新吞吐量: {throughput:F1} updates/sec");

            var successRate = successCount * 100.0 / totalUpdates;
            Assert.True(successRate >= 99.0, $"位置更新成功率 {successRate:F1}% 低于99%");
            Assert.True(averageTime < 20, $"平均位置更新时间 {averageTime:F2}ms 超过20ms目标");
            Assert.True(throughput > 500, $"位置更新吞吐量 {throughput:F1} updates/sec 低于500目标");
        }

        [Fact]
        public async Task PlayerGrain_Should_Handle_Memory_Pressure()
        {
            // Arrange
            const int playerCount = 200;
            var playerIds = Enumerable.Range(0, playerCount)
                .Select(i => $"memory-test-{i:D3}")
                .ToArray();

            // 获取初始内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            var stopwatch = Stopwatch.StartNew();

            // Act - 创建大量PlayerGrain并进行操作
            var tasks = playerIds.Select(async playerId =>
            {
                var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
                
                // 登录
                var loginResponse = await playerGrain.LoginAsync(new PlayerLoginRequest
                {
                    PlayerId = playerId,
                    DisplayName = $"内存测试-{playerId}",
                    ClientVersion = "1.0.0",
                    Platform = "MemoryTest"
                });

                // 多次操作
                for (int i = 0; i < 10; i++)
                {
                    await playerGrain.HeartbeatAsync();
                    await playerGrain.UpdatePositionAsync(new PlayerPosition { X = i, Y = i, Z = 0 });
                    await playerGrain.SetOnlineStatusAsync(i % 2 == 0 ? PlayerOnlineStatus.Online : PlayerOnlineStatus.Away);
                }

                // 获取最终状态
                var finalInfo = await playerGrain.GetPlayerInfoAsync();
                
                return loginResponse.Success && finalInfo != null;
            });

            var results = await Task.WhenAll(tasks);
            var totalTime = stopwatch.ElapsedMilliseconds;

            // 强制垃圾回收并获取峰值内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var peakMemory = GC.GetTotalMemory(false);
            var memoryIncrease = peakMemory - initialMemory;

            // Assert
            var successCount = results.Count(r => r);
            var memoryPerPlayer = memoryIncrease / playerCount;

            _output.WriteLine($"✅ 内存压力测试完成:");
            _output.WriteLine($"   - 玩家数量: {playerCount}");
            _output.WriteLine($"   - 成功处理: {successCount}");
            _output.WriteLine($"   - 总耗时: {totalTime}ms");
            _output.WriteLine($"   - 初始内存: {initialMemory / (1024 * 1024):F1} MB");
            _output.WriteLine($"   - 峰值内存: {peakMemory / (1024 * 1024):F1} MB");
            _output.WriteLine($"   - 内存增长: {memoryIncrease / (1024 * 1024):F1} MB");
            _output.WriteLine($"   - 每玩家内存: {memoryPerPlayer / 1024:F1} KB");

            Assert.Equal(playerCount, successCount);
            Assert.True(memoryPerPlayer < 50 * 1024, $"每玩家内存使用 {memoryPerPlayer / 1024:F1}KB 超过50KB目标"); // < 50KB per player
            Assert.True(memoryIncrease < 200 * 1024 * 1024, $"总内存增长 {memoryIncrease / (1024 * 1024):F1}MB 超过200MB限制"); // < 200MB total
        }

        [Fact]
        public async Task PlayerGrain_Should_Maintain_Performance_Under_Load()
        {
            // Arrange - 综合性能测试
            const int warmupPlayers = 50;
            const int testPlayers = 100;
            const int operationsPerPlayer = 5;

            // Warmup Phase - 预热系统
            _output.WriteLine("🔥 系统预热中...");
            var warmupTasks = Enumerable.Range(0, warmupPlayers).Select(async i =>
            {
                var playerId = $"warmup-{i}";
                var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
                await playerGrain.LoginAsync(new PlayerLoginRequest
                {
                    PlayerId = playerId,
                    DisplayName = $"预热-{i}",
                    ClientVersion = "1.0.0",
                    Platform = "Warmup"
                });
                await playerGrain.HeartbeatAsync();
            });
            await Task.WhenAll(warmupTasks);

            // Performance Test Phase
            _output.WriteLine("⚡ 开始性能测试...");
            var stopwatch = Stopwatch.StartNew();

            var performanceTasks = Enumerable.Range(0, testPlayers).Select(async playerIndex =>
            {
                var playerId = $"load-test-{playerIndex}";
                var playerGrain = _fixture.Cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
                var operationTimes = new List<long>();

                // 登录
                var loginStart = stopwatch.ElapsedMilliseconds;
                var loginResponse = await playerGrain.LoginAsync(new PlayerLoginRequest
                {
                    PlayerId = playerId,
                    DisplayName = $"负载测试-{playerIndex}",
                    ClientVersion = "1.0.0",
                    Platform = "LoadTest"
                });
                operationTimes.Add(stopwatch.ElapsedMilliseconds - loginStart);

                if (!loginResponse.Success)
                    return new { Success = false, AverageTime = 0.0, MaxTime = 0L };

                // 执行多种操作
                for (int i = 0; i < operationsPerPlayer; i++)
                {
                    var opStart = stopwatch.ElapsedMilliseconds;
                    
                    await Task.WhenAll(
                        playerGrain.HeartbeatAsync(),
                        playerGrain.UpdatePositionAsync(new PlayerPosition { X = i, Y = i, Z = 0 }),
                        playerGrain.SetOnlineStatusAsync(PlayerOnlineStatus.Online)
                    );
                    
                    operationTimes.Add(stopwatch.ElapsedMilliseconds - opStart);
                }

                // 最终状态检查
                var finalStart = stopwatch.ElapsedMilliseconds;
                var finalInfo = await playerGrain.GetPlayerInfoAsync();
                operationTimes.Add(stopwatch.ElapsedMilliseconds - finalStart);

                return new
                {
                    Success = finalInfo != null,
                    AverageTime = operationTimes.Average(),
                    MaxTime = operationTimes.Max()
                };
            });

            var results = await Task.WhenAll(performanceTasks);
            var totalTestTime = stopwatch.ElapsedMilliseconds;

            // Assert - 综合性能验证
            var successCount = results.Count(r => r.Success);
            var overallAverageTime = results.Where(r => r.Success).Average(r => r.AverageTime);
            var overallMaxTime = results.Where(r => r.Success).Max(r => r.MaxTime);
            var successRate = successCount * 100.0 / testPlayers;
            var totalOperations = testPlayers * (1 + operationsPerPlayer + 1); // login + operations + final check
            var throughput = totalOperations * 1000.0 / totalTestTime;

            _output.WriteLine($"✅ 综合负载测试完成:");
            _output.WriteLine($"   - 测试玩家数: {testPlayers}");
            _output.WriteLine($"   - 每玩家操作数: {operationsPerPlayer + 2}");
            _output.WriteLine($"   - 总操作数: {totalOperations}");
            _output.WriteLine($"   - 成功玩家数: {successCount}");
            _output.WriteLine($"   - 成功率: {successRate:F1}%");
            _output.WriteLine($"   - 总测试时间: {totalTestTime}ms");
            _output.WriteLine($"   - 平均操作时间: {overallAverageTime:F2}ms");
            _output.WriteLine($"   - 最大操作时间: {overallMaxTime}ms");
            _output.WriteLine($"   - 系统吞吐量: {throughput:F1} ops/sec");

            // 性能标准验证
            Assert.True(successRate >= 99.0, $"成功率 {successRate:F1}% 低于99%标准");
            Assert.True(overallAverageTime < 100, $"平均操作时间 {overallAverageTime:F2}ms 超过100ms目标");
            Assert.True(overallMaxTime < 1000, $"最大操作时间 {overallMaxTime}ms 超过1000ms阈值");
            Assert.True(throughput > 200, $"系统吞吐量 {throughput:F1} ops/sec 低于200 ops/sec目标");

            _output.WriteLine($"🎉 所有性能指标均满足要求！");
        }
    }
}
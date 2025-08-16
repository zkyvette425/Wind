using Microsoft.Extensions.Logging;
using Moq;
using Wind.GrainInterfaces;
using Wind.Grains;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Wind.Shared.Services;
using Wind.Shared.Extensions;
using Xunit;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace Wind.Tests.CacheTests;

/// <summary>
/// 分布式锁集成测试套件
/// 验证PlayerGrain和RoomGrain中分布式锁的并发安全性和性能
/// </summary>
public class DistributedLockIntegrationTests : IDisposable
{
    private readonly Mock<IDistributedLock> _mockDistributedLock;
    private readonly Mock<ILogger<PlayerGrain>> _mockPlayerLogger;
    private readonly Mock<ILogger<RoomGrain>> _mockRoomLogger;
    private readonly List<Exception> _concurrentExceptions;
    private readonly SemaphoreSlim _lockSemaphore;

    public DistributedLockIntegrationTests()
    {
        _mockDistributedLock = new Mock<IDistributedLock>();
        _mockPlayerLogger = new Mock<ILogger<PlayerGrain>>();
        _mockRoomLogger = new Mock<ILogger<RoomGrain>>();
        _concurrentExceptions = new List<Exception>();
        _lockSemaphore = new SemaphoreSlim(1, 1);
        
        SetupDistributedLockMock();
    }

    [Fact]
    public async Task PlayerGrain_Login_Should_Use_Distributed_Lock()
    {
        // Arrange
        var playerId = "test_player_001";
        var playerGrain = CreatePlayerGrain(playerId);
        var loginRequest = new PlayerLoginRequest
        {
            PlayerId = playerId,
            DisplayName = "TestPlayer",
            ClientVersion = "1.0.0",
            Platform = "PC",
            DeviceId = "device_001"
        };

        // Act
        var response = await playerGrain.LoginAsync(loginRequest);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("登录成功", response.Message);
        Assert.NotNull(response.SessionId);
        Assert.NotNull(response.AuthToken);
        
        // 验证分布式锁被正确调用（使用扩展方法）
        _mockDistributedLock.Verify(x => x.WithLockAsync(
            $"Player:{playerId}",
            It.IsAny<Func<Task<PlayerLoginResponse>>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task RoomGrain_CreateRoom_Should_Use_Distributed_Lock()
    {
        // Arrange
        var roomId = "test_room_001";
        var roomGrain = CreateRoomGrain(roomId);
        var createRequest = new CreateRoomRequest
        {
            CreatorId = "player_001",
            RoomName = "Test Room",
            RoomType = RoomType.Normal,
            MaxPlayerCount = 4,
            Settings = new RoomSettings
            {
                GameMode = "FreeForAll",
                MinPlayersToStart = 2,
                AutoStart = false
            }
        };

        // Act
        var response = await roomGrain.CreateRoomAsync(createRequest);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("房间创建成功", response.Message);
        Assert.Equal(roomId, response.RoomId);
        
        // 验证分布式锁被正确调用（使用扩展方法）
        _mockDistributedLock.Verify(x => x.WithLockAsync(
            $"Room:{roomId}",
            It.IsAny<Func<Task<CreateRoomResponse>>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task Concurrent_Player_Operations_Should_Be_Serialized()
    {
        // Arrange
        var playerId = "concurrent_player";
        var playerGrain = CreatePlayerGrain(playerId);
        var concurrentOperations = 10;
        var tasks = new List<Task>();
        var results = new ConcurrentBag<bool>();

        // 模拟锁争用 - 同时只允许一个操作
        var lockHeld = false;
        _mockDistributedLock
            .Setup(x => x.WithLockAsync(
                $"Player:{playerId}",
                It.IsAny<Func<Task<PlayerLoginResponse>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string lockKey, Func<Task<PlayerLoginResponse>> action, TimeSpan? expiry, TimeSpan? timeout, CancellationToken token) =>
            {
                await _lockSemaphore.WaitAsync();
                try
                {
                    if (lockHeld)
                    {
                        throw new InvalidOperationException("锁争用检测失败");
                    }
                    
                    lockHeld = true;
                    await Task.Delay(50); // 模拟操作耗时
                    var result = await action();
                    lockHeld = false;
                    
                    return result;
                }
                finally
                {
                    _lockSemaphore.Release();
                }
            });

        // Act - 并发执行多个登录操作
        for (int i = 0; i < concurrentOperations; i++)
        {
            var operationId = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var loginRequest = new PlayerLoginRequest
                    {
                        PlayerId = playerId,
                        DisplayName = $"Player_{operationId}",
                        ClientVersion = "1.0.0",
                        Platform = "PC",
                        DeviceId = $"device_{operationId}"
                    };

                    var response = await playerGrain.LoginAsync(loginRequest);
                    results.Add(response.Success);
                }
                catch (Exception ex)
                {
                    lock (_concurrentExceptions)
                    {
                        _concurrentExceptions.Add(ex);
                    }
                    results.Add(false);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(_concurrentExceptions);
        Assert.Equal(concurrentOperations, results.Count);
        Assert.All(results, result => Assert.True(result));
        
        // 验证锁被调用了预期次数
        _mockDistributedLock.Verify(x => x.WithLockAsync(
            $"Player:{playerId}",
            It.IsAny<Func<Task<PlayerLoginResponse>>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()
        ), Times.Exactly(concurrentOperations));
    }

    [Fact]
    public async Task Lock_Timeout_Should_Be_Handled_Gracefully()
    {
        // Arrange
        var playerId = "timeout_player";
        var playerGrain = CreatePlayerGrain(playerId);
        
        // 模拟锁超时
        _mockDistributedLock
            .Setup(x => x.WithLockAsync(
                $"Player:{playerId}",
                It.IsAny<Func<Task<PlayerLoginResponse>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("分布式锁获取超时"));

        var loginRequest = new PlayerLoginRequest
        {
            PlayerId = playerId,
            DisplayName = "TimeoutPlayer",
            ClientVersion = "1.0.0",
            Platform = "PC",
            DeviceId = "timeout_device"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => playerGrain.LoginAsync(loginRequest));
        
        Assert.Equal("分布式锁获取超时", exception.Message);
    }

    [Fact]
    public async Task Position_Update_Should_Use_TryWithLock()
    {
        // Arrange
        var playerId = "position_player";
        var playerGrain = CreatePlayerGrain(playerId);
        var position = new PlayerPosition
        {
            X = 100.5f,
            Y = 200.3f,
            Z = 50.1f,
            MapId = "map_001"
        };

        // 为位置更新设置TryWithLockAsync
        _mockDistributedLock
            .Setup(x => x.TryWithLockAsync(
                $"Player:{playerId}:Heartbeat",
                It.IsAny<Func<Task>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await playerGrain.UpdatePositionAsync(position);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task Heartbeat_Should_Use_TryWithLock()
    {
        // Arrange
        var playerId = "heartbeat_player";
        var playerGrain = CreatePlayerGrain(playerId);
        
        _mockDistributedLock
            .Setup(x => x.TryWithLockAsync(
                $"Player:{playerId}:Heartbeat",
                It.IsAny<Func<Task>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await playerGrain.HeartbeatAsync();

        // Assert
        Assert.True(result);
        
        // 验证心跳使用了TryWithLockAsync
        _mockDistributedLock.Verify(x => x.TryWithLockAsync(
            $"Player:{playerId}:Heartbeat",
            It.IsAny<Func<Task>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task Lock_Performance_Should_Meet_Requirements()
    {
        // Arrange
        var playerId = "perf_player";
        var playerGrain = CreatePlayerGrain(playerId);
        var operations = 100;
        var stopwatch = Stopwatch.StartNew();

        // Act - 执行多次锁操作
        for (int i = 0; i < operations; i++)
        {
            var loginRequest = new PlayerLoginRequest
            {
                PlayerId = playerId,
                DisplayName = $"PerfPlayer_{i}",
                ClientVersion = "1.0.0",
                Platform = "PC",
                DeviceId = $"perf_device_{i}"
            };

            await playerGrain.LoginAsync(loginRequest);
        }

        stopwatch.Stop();

        // Assert - 性能要求验证
        var averageTime = stopwatch.ElapsedMilliseconds / (double)operations;
        Assert.True(averageTime < 10.0, $"平均锁操作时间 {averageTime}ms 应该 < 10ms");
        
        var totalTime = stopwatch.ElapsedMilliseconds;
        Assert.True(totalTime < operations * 20, $"总操作时间 {totalTime}ms 过长");
        
        // 验证所有锁调用都成功完成
        _mockDistributedLock.Verify(x => x.WithLockAsync(
            $"Player:{playerId}",
            It.IsAny<Func<Task<PlayerLoginResponse>>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()
        ), Times.Exactly(operations));
    }

    [Fact]
    public async Task Mixed_Grain_Types_Should_Use_Different_Lock_Keys()
    {
        // Arrange
        var playerId = "mixed_player";
        var roomId = "mixed_room";
        var playerGrain = CreatePlayerGrain(playerId);
        var roomGrain = CreateRoomGrain(roomId);

        // Act
        var loginTask = playerGrain.LoginAsync(new PlayerLoginRequest
        {
            PlayerId = playerId,
            DisplayName = "MixedPlayer",
            ClientVersion = "1.0.0",
            Platform = "PC",
            DeviceId = "mixed_device"
        });

        var createRoomTask = roomGrain.CreateRoomAsync(new CreateRoomRequest
        {
            CreatorId = playerId,
            RoomName = "Mixed Room",
            RoomType = RoomType.Normal,
            MaxPlayerCount = 4
        });

        await Task.WhenAll(loginTask, createRoomTask);

        // Assert
        var loginResponse = await loginTask;
        var createResponse = await createRoomTask;
        
        Assert.True(loginResponse.Success);
        Assert.True(createResponse.Success);
        
        // 验证使用了不同的锁键
        _mockDistributedLock.Verify(x => x.WithLockAsync(
            $"Player:{playerId}",
            It.IsAny<Func<Task<PlayerLoginResponse>>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
        
        _mockDistributedLock.Verify(x => x.WithLockAsync(
            $"Room:{roomId}",
            It.IsAny<Func<Task<CreateRoomResponse>>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    private void SetupDistributedLockMock()
    {
        // 设置默认的WithLockAsync行为
        _mockDistributedLock
            .Setup(x => x.WithLockAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<PlayerLoginResponse>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string lockKey, Func<Task<PlayerLoginResponse>> action, TimeSpan? expiry, TimeSpan? timeout, CancellationToken token) =>
            {
                return await action();
            });

        _mockDistributedLock
            .Setup(x => x.WithLockAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<CreateRoomResponse>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string lockKey, Func<Task<CreateRoomResponse>> action, TimeSpan? expiry, TimeSpan? timeout, CancellationToken token) =>
            {
                return await action();
            });

        _mockDistributedLock
            .Setup(x => x.WithLockAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<JoinRoomResponse>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string lockKey, Func<Task<JoinRoomResponse>> action, TimeSpan? expiry, TimeSpan? timeout, CancellationToken token) =>
            {
                return await action();
            });

        _mockDistributedLock
            .Setup(x => x.WithLockAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<LeaveRoomResponse>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string lockKey, Func<Task<LeaveRoomResponse>> action, TimeSpan? expiry, TimeSpan? timeout, CancellationToken token) =>
            {
                return await action();
            });

        _mockDistributedLock
            .Setup(x => x.WithLockAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string lockKey, Func<Task<bool>> action, TimeSpan? expiry, TimeSpan? timeout, CancellationToken token) =>
            {
                return await action();
            });

        _mockDistributedLock
            .Setup(x => x.TryWithLockAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string lockKey, Func<Task> action, TimeSpan? expiry, TimeSpan? timeout, CancellationToken token) =>
            {
                await action();
                return true;
            });
    }

    private PlayerGrain CreatePlayerGrain(string playerId)
    {
        // 创建Mock缓存策略
        var mockCacheStrategy = new Mock<ICacheStrategy>();
        mockCacheStrategy.Setup(x => x.GetAsync<PlayerState>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((PlayerState?)null);
        mockCacheStrategy.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<PlayerState>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
        
        var grain = new PlayerGrain(_mockPlayerLogger.Object, _mockDistributedLock.Object, mockCacheStrategy.Object);
        
        // 手动调用OnActivateAsync来初始化状态
        grain.OnActivateAsync(CancellationToken.None).Wait();
        
        return grain;
    }

    private RoomGrain CreateRoomGrain(string roomId)
    {
        // 创建Mock缓存策略
        var mockCacheStrategy = new Mock<ICacheStrategy>();
        mockCacheStrategy.Setup(x => x.GetRoomCacheAsync<RoomState>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((RoomState?)null);
        mockCacheStrategy.Setup(x => x.SetRoomCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RoomState>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
        
        var grain = new RoomGrain(_mockRoomLogger.Object, _mockDistributedLock.Object, mockCacheStrategy.Object);
        
        // 手动调用OnActivateAsync来初始化状态
        grain.OnActivateAsync(CancellationToken.None).Wait();
        
        return grain;
    }

    public void Dispose()
    {
        _lockSemaphore?.Dispose();
    }
}
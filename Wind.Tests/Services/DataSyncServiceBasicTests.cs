using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wind.Server.Configuration;
using Wind.Server.Services;
using Wind.Shared.Models;
using Wind.Server.Models.Documents;
using Xunit;
using Xunit.Abstractions;
using Moq;
using StackExchange.Redis;
using MongoDB.Driver;

namespace Wind.Tests.Services;

/// <summary>
/// 数据同步服务基础功能测试
/// 测试数据同步逻辑而无需实际的Redis和MongoDB连接
/// </summary>
public class DataSyncServiceBasicTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<RedisConnectionManager> _mockRedisManager;
    private readonly Mock<MongoDbConnectionManager> _mockMongoManager;
    private readonly Mock<IPlayerPersistenceService> _mockPlayerPersistence;
    private readonly Mock<IRoomPersistenceService> _mockRoomPersistence;
    private readonly Mock<IGameRecordPersistenceService> _mockGameRecordPersistence;
    private readonly Mock<ILogger<DataSyncService>> _mockLogger;
    private readonly DataSyncOptions _options;

    public DataSyncServiceBasicTests(ITestOutputHelper output)
    {
        _output = output;
        
        // 创建Mock对象
        _mockRedisManager = new Mock<RedisConnectionManager>();
        _mockMongoManager = new Mock<MongoDbConnectionManager>();
        _mockPlayerPersistence = new Mock<IPlayerPersistenceService>();
        _mockRoomPersistence = new Mock<IRoomPersistenceService>();
        _mockGameRecordPersistence = new Mock<IGameRecordPersistenceService>();
        _mockLogger = new Mock<ILogger<DataSyncService>>();
        
        // 配置测试选项
        _options = new DataSyncOptions
        {
            FlushIntervalMs = 1000,
            FlushBatchSize = 10,
            MaxPendingWrites = 100,
            DefaultCacheExpirySeconds = 300,
            EnableStatistics = true,
            MongoCollections = new[] { "Players", "Rooms", "GameRecords", "Messages" },
            SyncStrategy = new SyncStrategyConfig
            {
                DefaultStrategy = SyncStrategyType.WriteThrough,
                TypeStrategyOverrides = new Dictionary<string, SyncStrategyType>
                {
                    ["PlayerState"] = SyncStrategyType.WriteThrough,
                    ["RoomState"] = SyncStrategyType.WriteThrough,
                    ["MessageInfo"] = SyncStrategyType.WriteBehind
                }
            }
        };
    }

    [Fact]
    public void DataSyncService_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var service = CreateDataSyncService();

        // Assert
        Assert.NotNull(service);
        _output.WriteLine("数据同步服务构造函数测试通过");
    }

    [Fact]
    public async Task GetSyncStats_ShouldReturnValidStatistics()
    {
        // Arrange
        var service = CreateDataSyncService();

        // Act
        var stats = await service.GetSyncStats();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.CacheHits >= 0, "缓存命中次数应该非负");
        Assert.True(stats.CacheMisses >= 0, "缓存未命中次数应该非负");
        Assert.True(stats.WriteThroughCount >= 0, "Write-Through次数应该非负");
        Assert.True(stats.WriteBehindCount >= 0, "Write-Behind次数应该非负");
        Assert.True(stats.PendingWriteBehindCount >= 0, "待处理Write-Behind数量应该非负");
        Assert.True(stats.SyncFailureCount >= 0, "同步失败次数应该非负");

        _output.WriteLine($"统计信息测试通过 - CacheHits:{stats.CacheHits}, CacheMisses:{stats.CacheMisses}");
    }

    [Fact]
    public async Task FlushPendingWrites_EmptyQueue_ShouldCompleteSuccessfully()
    {
        // Arrange
        var service = CreateDataSyncService();

        // Act & Assert - 应该不抛出异常
        await service.FlushPendingWrites();
        
        _output.WriteLine("空队列刷新测试通过");
    }

    [Fact]
    public void DataSyncOptions_Configuration_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(1000, _options.FlushIntervalMs);
        Assert.Equal(10, _options.FlushBatchSize);
        Assert.Equal(100, _options.MaxPendingWrites);
        Assert.Equal(300, _options.DefaultCacheExpirySeconds);
        Assert.True(_options.EnableStatistics);
        Assert.Equal(SyncStrategyType.WriteThrough, _options.SyncStrategy.DefaultStrategy);
        Assert.Contains("PlayerState", _options.SyncStrategy.TypeStrategyOverrides.Keys);
        Assert.Equal(SyncStrategyType.WriteThrough, _options.SyncStrategy.TypeStrategyOverrides["PlayerState"]);
        
        _output.WriteLine("数据同步选项配置测试通过");
    }

    [Fact]
    public void DataSyncStats_HitRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var stats = new DataSyncStats
        {
            CacheHits = 80,
            CacheMisses = 20
        };

        // Act
        var hitRate = stats.HitRate;

        // Assert
        Assert.Equal(0.8, hitRate, 3); // 80%命中率，精确到3位小数
        
        _output.WriteLine($"缓存命中率计算测试通过 - HitRate:{hitRate:P2}");
    }

    [Fact]
    public void DataSyncStats_HitRate_ZeroOperations_ShouldReturnZero()
    {
        // Arrange
        var stats = new DataSyncStats
        {
            CacheHits = 0,
            CacheMisses = 0
        };

        // Act
        var hitRate = stats.HitRate;

        // Assert
        Assert.Equal(0.0, hitRate);
        
        _output.WriteLine("零操作缓存命中率计算测试通过");
    }

    [Fact]
    public async Task DataSyncService_Dispose_ShouldCompleteGracefully()
    {
        // Arrange
        var service = CreateDataSyncService();

        // Act & Assert - 应该不抛出异常
        service.Dispose();
        
        _output.WriteLine("服务释放测试通过");
    }

    [Fact]
    public void DataSyncStrategyOptions_TypeOverrides_ShouldSupportMultipleTypes()
    {
        // Arrange
        var strategyOptions = new SyncStrategyConfig
        {
            DefaultStrategy = SyncStrategyType.WriteThrough,
            TypeStrategyOverrides = new Dictionary<string, SyncStrategyType>
            {
                ["PlayerState"] = SyncStrategyType.WriteThrough,
                ["RoomState"] = SyncStrategyType.WriteThrough, 
                ["MessageInfo"] = SyncStrategyType.WriteBehind,
                ["GameRecord"] = SyncStrategyType.CacheAside
            }
        };

        // Assert
        Assert.Equal(4, strategyOptions.TypeStrategyOverrides.Count);
        Assert.Equal(SyncStrategyType.WriteThrough, strategyOptions.TypeStrategyOverrides["PlayerState"]);
        Assert.Equal(SyncStrategyType.WriteBehind, strategyOptions.TypeStrategyOverrides["MessageInfo"]);
        Assert.Equal(SyncStrategyType.CacheAside, strategyOptions.TypeStrategyOverrides["GameRecord"]);
        
        _output.WriteLine("多类型同步策略配置测试通过");
    }

    [Fact]
    public void DataSyncOptions_MongoCollections_ShouldContainRequiredCollections()
    {
        // Assert
        Assert.Contains("Players", _options.MongoCollections);
        Assert.Contains("Rooms", _options.MongoCollections);
        Assert.Contains("GameRecords", _options.MongoCollections);
        Assert.Contains("Messages", _options.MongoCollections);
        
        _output.WriteLine("MongoDB集合配置测试通过");
    }

    [Fact]
    public void DataSyncOptions_Validation_ShouldEnforceReasonableValues()
    {
        // Arrange & Assert - 验证配置值的合理性
        Assert.True(_options.FlushIntervalMs > 0, "刷新间隔应该大于0");
        Assert.True(_options.FlushBatchSize > 0, "批处理大小应该大于0");
        Assert.True(_options.MaxPendingWrites > 0, "最大待处理写入数应该大于0");
        Assert.True(_options.DefaultCacheExpirySeconds > 0, "默认缓存过期时间应该大于0");
        Assert.True(_options.FlushBatchSize <= _options.MaxPendingWrites, 
            "批处理大小应该小于等于最大待处理写入数");
            
        _output.WriteLine("数据同步选项验证测试通过");
    }

    /// <summary>
    /// 创建数据同步服务实例（用于测试）
    /// </summary>
    private DataSyncService CreateDataSyncService()
    {
        var optionsWrapper = Options.Create(_options);
        
        return new DataSyncService(
            _mockRedisManager.Object,
            _mockMongoManager.Object,
            _mockPlayerPersistence.Object,
            _mockRoomPersistence.Object,
            _mockGameRecordPersistence.Object,
            optionsWrapper,
            _mockLogger.Object);
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wind.Server.Configuration;
using Wind.Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Wind.Tests.Services;

/// <summary>
/// 数据同步配置测试
/// 测试数据同步相关的配置和枚举
/// </summary>
public class DataSyncConfigurationTests
{
    private readonly ITestOutputHelper _output;

    public DataSyncConfigurationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SyncStrategyType_ShouldHaveExpectedValues()
    {
        // Assert - 验证枚举值
        Assert.True(Enum.IsDefined(typeof(SyncStrategyType), SyncStrategyType.WriteThrough));
        Assert.True(Enum.IsDefined(typeof(SyncStrategyType), SyncStrategyType.WriteBehind));
        Assert.True(Enum.IsDefined(typeof(SyncStrategyType), SyncStrategyType.CacheAside));
        
        _output.WriteLine("同步策略类型枚举测试通过");
    }

    [Fact]
    public void SyncStrategyConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange
        var config = new SyncStrategyConfig();

        // Assert
        Assert.Equal(SyncStrategyType.WriteThrough, config.DefaultStrategy);
        Assert.NotNull(config.TypeStrategyOverrides);
        Assert.True(config.TypeStrategyOverrides.Count >= 4); // 默认应该有4个类型覆盖
        Assert.Equal(SyncStrategyType.WriteThrough, config.TypeStrategyOverrides["PlayerState"]);
        Assert.Equal(SyncStrategyType.WriteThrough, config.TypeStrategyOverrides["RoomState"]);
        Assert.Equal(SyncStrategyType.WriteBehind, config.TypeStrategyOverrides["MessageInfo"]);
        Assert.Equal(SyncStrategyType.CacheAside, config.TypeStrategyOverrides["UserSession"]);
        
        _output.WriteLine("同步策略配置默认值测试通过");
    }

    [Fact]
    public void SyncStrategyConfig_GetStrategy_ShouldReturnCorrectStrategy()
    {
        // Arrange
        var config = new SyncStrategyConfig();

        // Act & Assert
        Assert.Equal(SyncStrategyType.WriteThrough, config.GetStrategy<PlayerStateStub>());
        Assert.Equal(SyncStrategyType.WriteThrough, config.GetStrategy<RoomStateStub>());
        Assert.Equal(SyncStrategyType.WriteBehind, config.GetStrategy<MessageInfoStub>());
        Assert.Equal(SyncStrategyType.CacheAside, config.GetStrategy<UserSessionStub>());
        Assert.Equal(SyncStrategyType.WriteThrough, config.GetStrategy<UnknownTypeStub>()); // 应该返回默认策略
        
        _output.WriteLine("同步策略获取测试通过");
    }

    [Fact]
    public void DataSyncOptions_DefaultValues_ShouldBeReasonable()
    {
        // Arrange
        var options = new DataSyncOptions();

        // Assert
        Assert.True(options.FlushIntervalMs > 0, "刷新间隔应该大于0");
        Assert.True(options.FlushBatchSize > 0, "批处理大小应该大于0");
        Assert.True(options.MaxPendingWrites > 0, "最大待处理写入数应该大于0");
        Assert.True(options.DefaultCacheExpirySeconds > 0, "默认缓存过期时间应该大于0");
        Assert.True(options.FlushBatchSize <= options.MaxPendingWrites, "批处理大小应该小于等于最大待处理写入数");
        Assert.NotNull(options.MongoCollections);
        Assert.NotNull(options.SyncStrategy);
        
        _output.WriteLine("数据同步选项默认值测试通过");
    }

    [Fact]
    public void DataSyncOptions_CollectionName_ShouldMapCorrectly()
    {
        // Arrange
        var options = new DataSyncOptions();
        options.MongoCollections = new[] { "Players", "Rooms", "GameRecords", "Messages" };

        // Act & Assert
        Assert.Equal("Players", options.GetCollectionName<PlayerStateStub>());
        Assert.Equal("Rooms", options.GetCollectionName<RoomStateStub>());
        Assert.Equal("GameRecords", options.GetCollectionName<GameRecordStub>());
        Assert.Equal("Messages", options.GetCollectionName<MessageInfoStub>());
        Assert.Equal("Others", options.GetCollectionName<UnknownTypeStub>());

        _output.WriteLine("MongoDB集合名称映射测试通过");
    }

    [Fact]
    public void DataSyncOptions_Validation_ShouldThrowOnInvalidValues()
    {
        // Arrange
        var options = new DataSyncOptions
        {
            FlushIntervalMs = 0, // 无效值
            FlushBatchSize = 0,  // 无效值
            MaxPendingWrites = 5, // 小于批处理大小
            DefaultCacheExpirySeconds = 0 // 无效值
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
        
        _output.WriteLine("数据同步选项验证测试通过");
    }

    [Fact]
    public void DataSyncStats_HitRate_Calculation_ShouldBeAccurate()
    {
        // Test Case 1: 正常命中率
        var stats1 = new DataSyncStats { CacheHits = 80, CacheMisses = 20 };
        Assert.Equal(0.8, stats1.HitRate, 3);

        // Test Case 2: 100%命中率
        var stats2 = new DataSyncStats { CacheHits = 100, CacheMisses = 0 };
        Assert.Equal(1.0, stats2.HitRate, 3);

        // Test Case 3: 0%命中率
        var stats3 = new DataSyncStats { CacheHits = 0, CacheMisses = 50 };
        Assert.Equal(0.0, stats3.HitRate, 3);

        // Test Case 4: 无操作
        var stats4 = new DataSyncStats { CacheHits = 0, CacheMisses = 0 };
        Assert.Equal(0.0, stats4.HitRate, 3);

        _output.WriteLine("数据同步统计命中率计算测试通过");
    }

    [Fact]
    public void SyncStrategyConfig_Validation_ShouldWork()
    {
        // Arrange
        var validConfig = new SyncStrategyConfig
        {
            DefaultStrategy = SyncStrategyType.WriteThrough,
            TypeStrategyOverrides = new Dictionary<string, SyncStrategyType>
            {
                ["TestType"] = SyncStrategyType.WriteBehind
            }
        };

        var invalidConfig = new SyncStrategyConfig
        {
            TypeStrategyOverrides = null! // 无效配置
        };

        // Act & Assert
        validConfig.Validate(); // 应该不抛出异常
        Assert.Throws<ArgumentNullException>(() => invalidConfig.Validate());
        
        _output.WriteLine("同步策略配置验证测试通过");
    }

    // 测试用的存根类
    private class PlayerStateStub { }
    private class RoomStateStub { }
    private class MessageInfoStub { }
    private class UserSessionStub { }
    private class GameRecordStub { }
    private class UnknownTypeStub { }
}
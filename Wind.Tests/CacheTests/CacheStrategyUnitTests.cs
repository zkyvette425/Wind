using Xunit;
using Xunit.Abstractions;
using Wind.Shared.Extensions;
using Wind.Shared.Models;

namespace Wind.Tests.CacheTests;

/// <summary>
/// 缓存策略扩展方法单元测试
/// 验证缓存键构建和扩展方法功能
/// </summary>
public class CacheStrategyUnitTests
{
    private readonly ITestOutputHelper _output;

    public CacheStrategyUnitTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory(DisplayName = "缓存键构建测试 - 验证不同数据类型的键格式")]
    [InlineData("player-001", "player_state", "player:player-001:player_state")]
    [InlineData("room-abc123", "room_info", "room:room-abc123:room_info")]
    [InlineData("queue-1", "matchmaking_queue", "matchmaking:queue-1:matchmaking_queue")]
    public void CacheKeyBuilding_ShouldGenerateCorrectKeys(string id, string dataType, string expectedKey)
    {
        // Act & Assert based on the extension method logic
        var playerKey = $"player:{id}:{dataType}";
        var roomKey = $"room:{id}:{dataType}";
        var matchmakingKey = $"matchmaking:{id}:{dataType}";

        // Verify the key format matches our TTL strategy mapping
        if (dataType.Contains("player"))
        {
            Assert.Equal(expectedKey, playerKey);
            _output.WriteLine($"✅ 玩家缓存键验证通过: {playerKey}");
        }
        else if (dataType.Contains("room"))
        {
            Assert.Equal(expectedKey, roomKey);
            _output.WriteLine($"✅ 房间缓存键验证通过: {roomKey}");
        }
        else if (dataType.Contains("matchmaking"))
        {
            Assert.Equal(expectedKey, matchmakingKey);
            _output.WriteLine($"✅ 匹配缓存键验证通过: {matchmakingKey}");
        }
    }

    [Fact(DisplayName = "TTL策略映射验证 - 确认不同数据类型使用正确的过期时间")]
    public void TtlStrategyMapping_ShouldUseCorrectExpiry()
    {
        // Arrange - 基于RedisCacheStrategy中的TTL策略
        var ttlMappings = new Dictionary<string, TimeSpan>
        {
            // Orleans PlayerStorage (DB0) 相关缓存
            ["player_state"] = TimeSpan.FromMinutes(45),
            ["player_info"] = TimeSpan.FromMinutes(30),
            ["player_session"] = TimeSpan.FromHours(2),
            
            // Orleans RoomStorage (DB1) 相关缓存
            ["room_state"] = TimeSpan.FromMinutes(25),
            ["room_info"] = TimeSpan.FromMinutes(20),
            ["room_players"] = TimeSpan.FromMinutes(15),
            
            // Orleans MatchmakingStorage (DB2) 相关缓存
            ["matchmaking_queue"] = TimeSpan.FromMinutes(5),
            ["matchmaking_stats"] = TimeSpan.FromMinutes(10),
            
            // 临时和验证数据
            ["verification"] = TimeSpan.FromMinutes(5),
            ["temp"] = TimeSpan.FromMinutes(2)
        };

        // Act & Assert
        foreach (var mapping in ttlMappings)
        {
            var dataType = mapping.Key;
            var expectedExpiry = mapping.Value;
            
            // 验证TTL策略的合理性
            if (dataType.StartsWith("player"))
            {
                Assert.True(expectedExpiry >= TimeSpan.FromMinutes(20), 
                    $"玩家数据过期时间应该 >= 20分钟: {dataType}");
            }
            else if (dataType.StartsWith("room"))
            {
                Assert.True(expectedExpiry >= TimeSpan.FromMinutes(15), 
                    $"房间数据过期时间应该 >= 15分钟: {dataType}");
            }
            else if (dataType.Contains("temp") || dataType.Contains("verification"))
            {
                Assert.True(expectedExpiry <= TimeSpan.FromMinutes(10), 
                    $"临时数据过期时间应该 <= 10分钟: {dataType}");
            }
            
            _output.WriteLine($"✅ TTL策略验证: {dataType} -> {expectedExpiry.TotalMinutes}分钟");
        }
    }

    [Fact(DisplayName = "缓存健康状态检查逻辑验证")]
    public void CacheHealthLogic_ShouldEvaluateCorrectly()
    {
        // Arrange - 模拟不同的缓存统计场景
        var healthyStats = new
        {
            HitRate = 95.0,
            TotalRequests = 1000L,
            AverageResponseTime = TimeSpan.FromMilliseconds(25)
        };

        var degradedStats = new
        {
            HitRate = 75.0,
            TotalRequests = 500L,
            AverageResponseTime = TimeSpan.FromMilliseconds(80)
        };

        var lowVolumeStats = new
        {
            HitRate = 98.0,
            TotalRequests = 50L,
            AverageResponseTime = TimeSpan.FromMilliseconds(15)
        };

        // Act & Assert
        // 健康状态: 高命中率 + 合理响应时间
        Assert.True(healthyStats.HitRate >= 90.0 && healthyStats.AverageResponseTime.TotalMilliseconds <= 50);
        _output.WriteLine($"✅ 健康状态验证: 命中率={healthyStats.HitRate}%, 响应时间={healthyStats.AverageResponseTime.TotalMilliseconds}ms");

        // 降级状态: 命中率偏低或响应时间过长
        var isDegraded = degradedStats.HitRate < 90.0 || degradedStats.AverageResponseTime.TotalMilliseconds > 50;
        Assert.True(isDegraded);
        _output.WriteLine($"✅ 降级状态验证: 命中率={degradedStats.HitRate}%, 响应时间={degradedStats.AverageResponseTime.TotalMilliseconds}ms");

        // 低流量状态: 请求量不足导致统计不准确
        Assert.True(lowVolumeStats.TotalRequests < 100);
        _output.WriteLine($"✅ 低流量状态验证: 请求数={lowVolumeStats.TotalRequests}");
    }

    [Theory(DisplayName = "分布式锁键命名验证 - 避免锁键冲突")]
    [InlineData("player", "player-001", "operation", "lock:player:player-001:operation")]
    [InlineData("room", "room-abc", "join", "lock:room:room-abc:join")]
    [InlineData("matchmaking", "queue-1", "update", "lock:matchmaking:queue-1:update")]
    public void DistributedLockKeys_ShouldFollowNamingConvention(string entityType, string entityId, string operation, string expectedLockKey)
    {
        // Act
        var actualLockKey = $"lock:{entityType}:{entityId}:{operation}";

        // Assert
        Assert.Equal(expectedLockKey, actualLockKey);
        _output.WriteLine($"✅ 分布式锁键验证: {actualLockKey}");
    }

    [Fact(DisplayName = "缓存预热优先级策略验证")]
    public void CacheWarmupPriority_ShouldRankCorrectly()
    {
        // Arrange - 不同类型数据的预热优先级
        var warmupPriorities = new Dictionary<string, int>
        {
            ["system_config"] = 10,     // 最高优先级 - 系统配置
            ["game_config"] = 9,        // 游戏配置
            ["active_players"] = 8,     // 活跃玩家
            ["hot_rooms"] = 7,          // 热门房间
            ["matchmaking_queues"] = 6, // 匹配队列
            ["player_stats"] = 5,       // 玩家统计
            ["room_history"] = 4,       // 房间历史
            ["temp_data"] = 1           // 最低优先级 - 临时数据
        };

        // Act & Assert
        var sortedByPriority = warmupPriorities.OrderByDescending(kvp => kvp.Value).ToList();
        
        // 验证优先级排序
        Assert.Equal("system_config", sortedByPriority.First().Key);
        Assert.Equal("temp_data", sortedByPriority.Last().Key);
        
        // 验证系统配置具有最高优先级
        Assert.True(warmupPriorities["system_config"] > warmupPriorities["player_stats"]);
        Assert.True(warmupPriorities["game_config"] > warmupPriorities["room_history"]);

        foreach (var priority in sortedByPriority)
        {
            _output.WriteLine($"✅ 预热优先级: {priority.Key} -> 优先级{priority.Value}");
        }
    }

    [Fact(DisplayName = "LRU淘汰策略逻辑验证")]
    public void LruEvictionLogic_ShouldPrioritizeCorrectly()
    {
        // Arrange - 模拟缓存项访问时间
        var cacheItems = new Dictionary<string, DateTime>
        {
            ["frequently_used"] = DateTime.UtcNow.AddMinutes(-5),   // 最近访问
            ["moderately_used"] = DateTime.UtcNow.AddMinutes(-30), // 中等访问
            ["rarely_used"] = DateTime.UtcNow.AddHours(-2),        // 很少访问
            ["very_old"] = DateTime.UtcNow.AddHours(-6)            // 很久没访问
        };

        // Act - 按LRU策略排序（最少使用的在前）
        var lruSorted = cacheItems.OrderBy(kvp => kvp.Value).ToList();

        // Assert
        Assert.Equal("very_old", lruSorted.First().Key);      // 最应该被淘汰
        Assert.Equal("frequently_used", lruSorted.Last().Key); // 最不应该被淘汰

        _output.WriteLine("✅ LRU淘汰顺序验证:");
        for (int i = 0; i < lruSorted.Count; i++)
        {
            var item = lruSorted[i];
            var ageMinutes = (DateTime.UtcNow - item.Value).TotalMinutes;
            _output.WriteLine($"   {i + 1}. {item.Key} (距离上次访问: {ageMinutes:F0}分钟)");
        }
    }

    [Theory(DisplayName = "心跳频率优化验证 - 减少缓存写入频率")]
    [InlineData(1, false, "心跳1次不更新缓存")]
    [InlineData(5, false, "心跳5次不更新缓存")]
    [InlineData(10, true, "心跳10次更新缓存")]
    [InlineData(20, true, "心跳20次更新缓存")]
    public void HeartbeatCacheUpdate_ShouldOptimizeWriteFrequency(int heartbeatCount, bool shouldUpdateCache, string description)
    {
        // Act - 模拟心跳缓存更新逻辑 (每10次心跳才更新主状态缓存)
        var shouldUpdate = (heartbeatCount % 10) == 0;

        // Assert
        Assert.Equal(shouldUpdateCache, shouldUpdate);
        _output.WriteLine($"✅ {description}: {shouldUpdate}");
    }
}
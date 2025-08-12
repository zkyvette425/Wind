using System.Diagnostics;
using System.Text.Json;
using MessagePack;
using Newtonsoft.Json;
using Wind.Shared.Models;
using Wind.Shared.Protocols;
using Xunit;
using Xunit.Abstractions;
using STJSerializer = System.Text.Json.JsonSerializer;

namespace Wind.Tests.Performance;

/// <summary>
/// 序列化性能基准测试框架
/// 用于对比不同序列化方案的性能表现
/// </summary>
public class SerializationBenchmarks
{
    private readonly ITestOutputHelper _output;
    private readonly PlayerState _testPlayerState;
    private readonly PlayerLoginRequest _testLoginRequest;
    private const int IterationCount = 10000;

    public SerializationBenchmarks(ITestOutputHelper output)
    {
        _output = output;
        
        // 创建测试数据
        _testPlayerState = new PlayerState
        {
            PlayerId = "TestPlayer123",
            DisplayName = "测试玩家",
            Level = 42,
            Experience = 123456,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
            OnlineStatus = PlayerOnlineStatus.Online,
            CurrentRoomId = "Room_001",
            Position = new PlayerPosition
            {
                X = 100.5f,
                Y = 200.3f,
                Z = 50.1f,
                Rotation = 45.0f,
                MapId = "TestMap",
                UpdatedAt = DateTime.UtcNow
            },
            Stats = new PlayerStats
            {
                GamesPlayed = 150,
                GamesWon = 120,
                GamesLost = 30,
                TotalPlayTime = 360000,
                HighestScore = 99999,
                CustomStats = new Dictionary<string, object>
                {
                    { "kills", 1500 },
                    { "deaths", 300 },
                    { "assists", 800 }
                }
            },
            Settings = new PlayerSettings
            {
                Language = "zh-CN",
                Timezone = "Asia/Shanghai",
                EnableNotifications = true,
                EnableSound = true,
                SoundVolume = 0.8f,
                GameSettings = new Dictionary<string, object>
                {
                    { "graphics_quality", "high" },
                    { "auto_aim", false }
                },
                UISettings = new Dictionary<string, string>
                {
                    { "theme", "dark" },
                    { "font_size", "medium" }
                }
            }
        };

        _testLoginRequest = new PlayerLoginRequest
        {
            PlayerId = "TestPlayer123",
            DisplayName = "测试玩家",
            ClientVersion = "1.0.0",
            Platform = "Windows",
            DeviceId = "TestDevice001"
        };
    }

    [Fact]
    public void MessagePack_Serialization_Performance_Test()
    {
        _output.WriteLine("=== MessagePack 序列化性能测试 ===");
        
        // 预热
        for (int i = 0; i < 1000; i++)
        {
            var bytes = MessagePackSerializer.Serialize(_testPlayerState);
            var deserialized = MessagePackSerializer.Deserialize<PlayerState>(bytes);
        }

        // 序列化测试
        var sw = Stopwatch.StartNew();
        byte[][] serializedData = new byte[IterationCount][];
        
        for (int i = 0; i < IterationCount; i++)
        {
            serializedData[i] = MessagePackSerializer.Serialize(_testPlayerState);
        }
        
        sw.Stop();
        var serializeTime = sw.ElapsedMilliseconds;
        var avgSize = serializedData[0].Length;

        // 反序列化测试
        sw.Restart();
        
        for (int i = 0; i < IterationCount; i++)
        {
            var deserialized = MessagePackSerializer.Deserialize<PlayerState>(serializedData[i]);
        }
        
        sw.Stop();
        var deserializeTime = sw.ElapsedMilliseconds;

        _output.WriteLine($"序列化时间: {serializeTime}ms ({IterationCount:N0} 次)");
        _output.WriteLine($"反序列化时间: {deserializeTime}ms ({IterationCount:N0} 次)");
        _output.WriteLine($"数据大小: {avgSize} bytes");
        _output.WriteLine($"总时间: {serializeTime + deserializeTime}ms");
        _output.WriteLine($"每次序列化: {(double)serializeTime / IterationCount:F3}ms");
        _output.WriteLine($"每次反序列化: {(double)deserializeTime / IterationCount:F3}ms");
    }

    [Fact]
    public void SystemTextJson_Serialization_Performance_Test()
    {
        _output.WriteLine("=== System.Text.Json 序列化性能测试 ===");
        
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // 预热
        for (int i = 0; i < 1000; i++)
        {
            var json = STJSerializer.Serialize(_testPlayerState, options);
            var deserialized = STJSerializer.Deserialize<PlayerState>(json, options);
        }

        // 序列化测试
        var sw = Stopwatch.StartNew();
        string[] serializedData = new string[IterationCount];
        
        for (int i = 0; i < IterationCount; i++)
        {
            serializedData[i] = STJSerializer.Serialize(_testPlayerState, options);
        }
        
        sw.Stop();
        var serializeTime = sw.ElapsedMilliseconds;
        var avgSize = System.Text.Encoding.UTF8.GetByteCount(serializedData[0]);

        // 反序列化测试
        sw.Restart();
        
        for (int i = 0; i < IterationCount; i++)
        {
            var deserialized = STJSerializer.Deserialize<PlayerState>(serializedData[i], options);
        }
        
        sw.Stop();
        var deserializeTime = sw.ElapsedMilliseconds;

        _output.WriteLine($"序列化时间: {serializeTime}ms ({IterationCount:N0} 次)");
        _output.WriteLine($"反序列化时间: {deserializeTime}ms ({IterationCount:N0} 次)");
        _output.WriteLine($"数据大小: {avgSize} bytes");
        _output.WriteLine($"总时间: {serializeTime + deserializeTime}ms");
        _output.WriteLine($"每次序列化: {(double)serializeTime / IterationCount:F3}ms");
        _output.WriteLine($"每次反序列化: {(double)deserializeTime / IterationCount:F3}ms");
    }

    [Fact]
    public void NewtonsoftJson_Serialization_Performance_Test()
    {
        _output.WriteLine("=== Newtonsoft.Json 序列化性能测试 ===");
        
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };

        // 预热
        for (int i = 0; i < 1000; i++)
        {
            var json = JsonConvert.SerializeObject(_testPlayerState, settings);
            var deserialized = JsonConvert.DeserializeObject<PlayerState>(json, settings);
        }

        // 序列化测试
        var sw = Stopwatch.StartNew();
        string[] serializedData = new string[IterationCount];
        
        for (int i = 0; i < IterationCount; i++)
        {
            serializedData[i] = JsonConvert.SerializeObject(_testPlayerState, settings);
        }
        
        sw.Stop();
        var serializeTime = sw.ElapsedMilliseconds;
        var avgSize = System.Text.Encoding.UTF8.GetByteCount(serializedData[0]);

        // 反序列化测试
        sw.Restart();
        
        for (int i = 0; i < IterationCount; i++)
        {
            var deserialized = JsonConvert.DeserializeObject<PlayerState>(serializedData[i], settings);
        }
        
        sw.Stop();
        var deserializeTime = sw.ElapsedMilliseconds;

        _output.WriteLine($"序列化时间: {serializeTime}ms ({IterationCount:N0} 次)");
        _output.WriteLine($"反序列化时间: {deserializeTime}ms ({IterationCount:N0} 次)");
        _output.WriteLine($"数据大小: {avgSize} bytes");
        _output.WriteLine($"总时间: {serializeTime + deserializeTime}ms");
        _output.WriteLine($"每次序列化: {(double)serializeTime / IterationCount:F3}ms");
        _output.WriteLine($"每次反序列化: {(double)deserializeTime / IterationCount:F3}ms");
    }

    [Fact]
    public void Serialization_Comparison_Summary()
    {
        _output.WriteLine("=== 序列化方案对比总结 ===");
        _output.WriteLine("运行所有基准测试以获取性能对比数据");
        _output.WriteLine("");
        _output.WriteLine("预期性能排序 (快到慢):");
        _output.WriteLine("1. MessagePack - 二进制，高性能");
        _output.WriteLine("2. System.Text.Json - 现代JSON，良好性能");
        _output.WriteLine("3. Newtonsoft.Json - 传统JSON，功能丰富");
        _output.WriteLine("");
        _output.WriteLine("注意：实际性能可能因数据结构和环境而异");
        _output.WriteLine("建议：在生产环境中运行完整的基准测试");
    }
}

/// <summary>
/// 序列化性能结果记录
/// </summary>
public class SerializationPerformanceResult
{
    public string SerializerName { get; set; } = string.Empty;
    public long SerializationTimeMs { get; set; }
    public long DeserializationTimeMs { get; set; }
    public int DataSizeBytes { get; set; }
    public int IterationCount { get; set; }
    
    public double AvgSerializationTimeMs => (double)SerializationTimeMs / IterationCount;
    public double AvgDeserializationTimeMs => (double)DeserializationTimeMs / IterationCount;
    public long TotalTimeMs => SerializationTimeMs + DeserializationTimeMs;
}
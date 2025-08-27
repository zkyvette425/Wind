using MongoDB.Driver;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Wind.Tests.IntegrationTests;

/// <summary>
/// 独立的连接测试 - 不依赖Orleans或ClusterFixture
/// </summary>
public class IsolatedConnectionTest
{
    private readonly ITestOutputHelper _output;

    public IsolatedConnectionTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Redis_DirectConnection_ShouldWork()
    {
        try
        {
            // Arrange
            var connectionString = "localhost:6380";
            var connection = ConnectionMultiplexer.Connect($"{connectionString},password=windgame123");
            var database = connection.GetDatabase();

            // Act
            await database.StringSetAsync("test:isolated", "success");
            var result = await database.StringGetAsync("test:isolated");

            // Assert
            Assert.True(result.HasValue);
            Assert.Equal("success", result!);

            // Cleanup
            await database.KeyDeleteAsync("test:isolated");
            connection.Dispose();

            _output.WriteLine("✅ Redis直接连接测试成功");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Redis连接失败: {ex.Message}");
            _output.WriteLine($"详细错误: {ex}");
            throw;
        }
    }

    [Fact]
    public async Task MongoDB_DirectConnection_ShouldWork()
    {
        try
        {
            // Arrange
            var connectionString = "mongodb://localhost:27018/windgame_test?replicaSet=rs0&directConnection=false&connectTimeoutMS=10000&serverSelectionTimeoutMS=10000&ssl=false";
            _output.WriteLine($"使用连接字符串: {connectionString}");

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase("windgame_test");

            // Act
            var pingCommand = new MongoDB.Bson.BsonDocument("ping", 1);
            var result = await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(pingCommand);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Contains("ok"));
            Assert.Equal(1.0, result["ok"].AsDouble);

            _output.WriteLine("✅ MongoDB直接连接测试成功");
            _output.WriteLine($"Ping结果: {result}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ MongoDB连接失败: {ex.Message}");
            _output.WriteLine($"详细错误: {ex}");
            throw;
        }
    }
}
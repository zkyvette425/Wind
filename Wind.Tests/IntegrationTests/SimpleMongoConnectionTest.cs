using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Wind.Tests.IntegrationTests;

public class SimpleMongoConnectionTest
{
    private readonly ITestOutputHelper _output;

    public SimpleMongoConnectionTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DirectMongoDB_ShouldConnectSuccessfully()
    {
        // Arrange
        var connectionString = "mongodb://windadmin:windgame123@localhost:27017/windgame_test?authSource=admin";
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase("windgame_test");
        
        // Act & Assert
        var result = await database.RunCommandAsync<object>("{ ping: 1 }");
        Assert.NotNull(result);
        
        _output.WriteLine("✅ MongoDB直接连接测试成功");
        _output.WriteLine($"连接字符串: {connectionString}");
        _output.WriteLine($"Ping结果: {result}");
    }
}
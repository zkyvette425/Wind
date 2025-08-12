using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Wind.Client.Services;

namespace Wind.Client;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Wind游戏客户端 v1.2 - MagicOnion + Orleans集成测试");
        Console.WriteLine("================================================");

        // 配置日志和依赖注入
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton<WindGameClient>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        var client = host.Services.GetRequiredService<WindGameClient>();

        try
        {
            // 连接到服务器
            var config = new WindGameClient.ServerConfig
            {
                GrpcAddress = "http://localhost:5271",
                OrleansGatewayAddress = "127.0.0.1",
                OrleansGatewayPort = 30000
            };

            Console.WriteLine("正在连接到服务器...");
            var connected = await client.ConnectAsync(config);
            
            if (!connected)
            {
                Console.WriteLine("❌ 连接服务器失败！请确保服务器正在运行。");
                return 1;
            }

            Console.WriteLine("✅ 连接服务器成功！");
            Console.WriteLine();

            // 运行测试
            await RunTestsAsync(client);

            Console.WriteLine();
            Console.WriteLine("测试完成！按任意键退出...");
            Console.ReadKey();

            client.Disconnect();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 客户端运行时错误: {ex.Message}");
            return 1;
        }
    }

    static async Task RunTestsAsync(WindGameClient client)
    {
        Console.WriteLine("🧪 开始运行集成测试...");
        Console.WriteLine();

        try
        {
            // 测试1: MagicOnion RPC - 加法运算
            Console.WriteLine("测试1: MagicOnion RPC调用 - AddAsync");
            var addResult = await client.TestAddAsync(100, 200);
            Console.WriteLine($"   结果: 100 + 200 = {addResult}");
            Console.WriteLine();

            // 测试2: MagicOnion RPC - 字符串回显
            Console.WriteLine("测试2: MagicOnion RPC调用 - EchoAsync");
            var echoResult = await client.TestEchoAsync("Hello MagicOnion!");
            Console.WriteLine($"   结果: {echoResult}");
            Console.WriteLine();

            // 测试3: MagicOnion RPC - 服务器信息
            Console.WriteLine("测试3: MagicOnion RPC调用 - GetServerInfoAsync");
            var serverInfo = await client.GetServerInfoAsync();
            Console.WriteLine($"   结果: {serverInfo}");
            Console.WriteLine();

            // 测试4: Orleans Grain直接调用
            Console.WriteLine("测试4: Orleans Grain直接调用 - HelloGrain");
            var grainResult = await client.TestOrleansGrainAsync("Client Direct Call");
            Console.WriteLine($"   结果: {grainResult}");
            Console.WriteLine();

            Console.WriteLine("✅ 所有测试执行完成！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 测试执行失败: {ex.Message}");
            Console.WriteLine($"   详细错误: {ex}");
        }
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Wind.GrainInterfaces;

namespace Wind.Client;

/// <summary>
/// Orleans测试客户端，用于验证HelloGrain是否正常工作
/// </summary>
public class OrleansTestClient
{
    public static async Task<int> Main(string[] args)
    {
        // 如果参数包含SimpleOrleansTest，运行简化测试
        if (args.Length > 0 && args[0] == "SimpleOrleansTest")
        {
            return await SimpleOrleansTest.RunTestAsync();
        }
        
        // 否则运行原始测试
        try
        {
            Console.WriteLine("启动Orleans测试客户端...");

            // 创建Orleans客户端
            var builder = Host.CreateApplicationBuilder(args);
            
            builder.UseOrleansClient(client =>
            {
                client.UseLocalhostClustering(gatewayPort: 30000);
            });

            // 配置日志
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            var host = builder.Build();
            await host.StartAsync();

            // 获取Orleans客户端
            var clusterClient = host.Services.GetRequiredService<IClusterClient>();

            Console.WriteLine("连接到Orleans集群...");

            // 测试HelloGrain
            var helloGrain = clusterClient.GetGrain<IHelloGrain>("test-user");
            
            Console.WriteLine("调用HelloGrain.SayHelloAsync...");
            var result = await helloGrain.SayHelloAsync("Orleans测试");
            
            Console.WriteLine($"收到响应: {result}");

            // 测试多个调用
            Console.WriteLine("\n进行多次调用测试...");
            for (int i = 1; i <= 3; i++)
            {
                var response = await helloGrain.SayHelloAsync($"测试{i}");
                Console.WriteLine($"测试{i}响应: {response}");
            }

            Console.WriteLine("\n✅ Orleans基础环境测试成功！");
            
            await host.StopAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 测试失败: {ex.Message}");
            return 1;
        }
    }
}
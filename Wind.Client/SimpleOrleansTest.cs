using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Wind.GrainInterfaces;
using System.Net;

namespace Wind.Client;

/// <summary>
/// 简单的Orleans测试，配置明确的连接地址
/// </summary>
public class SimpleOrleansTest
{
    public static async Task<int> RunTestAsync()
    {
        try
        {
            Console.WriteLine("🚀 启动Orleans客户端测试...");

            var builder = Host.CreateApplicationBuilder();
            
            builder.UseOrleansClient(client =>
            {
                // 使用localhost集群配置
                client.UseLocalhostClustering(gatewayPort: 30000);
                
                client.Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "default";
                    options.ServiceId = "default";
                });
            });

            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Warning); // 减少日志噪音

            var host = builder.Build();
            
            Console.WriteLine("🔗 连接到Orleans集群 (127.0.0.1:30000)...");
            await host.StartAsync();

            var client = host.Services.GetRequiredService<IClusterClient>();
            
            // 测试HelloGrain
            Console.WriteLine("🎯 获取HelloGrain实例...");
            var helloGrain = client.GetGrain<IHelloGrain>("test-grain-001");
            
            Console.WriteLine("📞 调用HelloGrain.SayHelloAsync...");
            var result = await helloGrain.SayHelloAsync("Orleans端到端测试");
            
            Console.WriteLine($"✅ 收到响应: {result}");

            // 进行多次测试
            Console.WriteLine("\n🔄 进行连续调用测试...");
            for (int i = 1; i <= 3; i++)
            {
                var response = await helloGrain.SayHelloAsync($"批量测试{i}");
                Console.WriteLine($"   测试{i}: {response}");
                await Task.Delay(100); // 稍微延迟
            }

            Console.WriteLine("\n🎉 Orleans基础环境测试完全成功！");
            Console.WriteLine("   ✅ Silo连接正常");
            Console.WriteLine("   ✅ Grain调用成功");
            Console.WriteLine("   ✅ 端到端通信验证完成");
            
            await host.StopAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 测试失败!");
            Console.WriteLine($"   错误: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   内部错误: {ex.InnerException.Message}");
            }
            return 1;
        }
    }
}
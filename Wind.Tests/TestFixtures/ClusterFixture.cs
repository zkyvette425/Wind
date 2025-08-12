using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Orleans;
using Orleans.TestingHost;
using Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;

namespace Wind.Tests.TestFixtures;

/// <summary>
/// Orleans测试集群固件 - 为集成测试提供Orleans环境
/// </summary>
public class ClusterFixture : IDisposable
{
    public TestCluster Cluster { get; private set; }

    public ClusterFixture()
    {
        var builder = new TestClusterBuilder();
        
        // 配置Orleans Silo
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        
        // 配置Orleans Client
        builder.AddClientBuilderConfigurator<TestClientConfigurator>();
        
        // 全局配置MessagePack序列化器
        builder.ConfigureHostConfiguration(config =>
        {
            // 确保测试集群正确配置
        });

        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster?.StopAllSilos();
        Cluster?.Dispose();
    }
}

/// <summary>
/// 测试Silo配置器
/// </summary>
public class TestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder
            .ConfigureLogging(logging => 
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning); // 减少测试日志噪音
            })
            .AddMemoryGrainStorageAsDefault() // 使用内存存储用于测试
            .ConfigureServices(services => 
            {
                // 为测试环境配置MessagePack序列化器
                services.AddSerializer(serializerBuilder => 
                {
                    serializerBuilder.AddMessagePackSerializer();
                });
            });
    }
}

/// <summary>
/// 测试客户端配置器
/// </summary>
public class TestClientConfigurator : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        clientBuilder.ConfigureServices(services => 
        {
            // 为客户端配置MessagePack序列化器
            services.AddSerializer(serializerBuilder => 
            {
                serializerBuilder.AddMessagePackSerializer();
            });
        });
    }
}
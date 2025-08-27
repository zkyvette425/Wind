using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Orleans;
using Orleans.TestingHost;
using Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;
using Wind.Server.Services;
using Wind.Server.Configuration;
using Microsoft.Extensions.Options;

namespace Wind.Tests.TestFixtures;

/// <summary>
/// Orleans测试集群固件 - 为集成测试提供Orleans环境
/// </summary>
public class ClusterFixture : IDisposable
{
    public TestCluster Cluster { get; private set; }
    public IServiceProvider ServiceProvider => Cluster.Client.ServiceProvider;

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
                
                // 添加限流服务配置
                services.Configure<RateLimitOptions>(options =>
                {
                    options.DefaultPolicy = new RateLimitPolicy
                    {
                        Name = "TestDefault",
                        WindowSize = TimeSpan.FromSeconds(10),
                        MaxRequests = 100,
                        GlobalMaxRequests = 1000
                    };
                    options.EndpointPolicies = new Dictionary<string, RateLimitPolicy>
                    {
                        ["LoginAsync"] = new RateLimitPolicy
                        {
                            Name = "TestLogin",
                            WindowSize = TimeSpan.FromMinutes(1),
                            MaxRequests = 10,
                            GlobalMaxRequests = 100
                        }
                    };
                    options.WhitelistedClients = new List<string>();
                    options.EnableRateLimit = true;
                    options.EnableLogging = false; // 测试时关闭日志
                });
                
                // 注册限流服务
                services.AddSingleton<RateLimitingService>();
                
                // 注册数据存储相关服务（用于集成测试）
                // 配置Redis连接 (使用测试专用端口)
                services.Configure<RedisOptions>(options =>
                {
                    options.ConnectionString = "localhost:6380";
                    options.Password = "windgame123";
                    options.Database = 0;
                    options.KeyPrefix = "Wind:Test:";
                });
                
                // 配置MongoDB连接 (使用测试专用端口)
                services.Configure<MongoDbOptions>(options =>
                {
                    options.ConnectionString = "mongodb://localhost:27018/windgame_test?replicaSet=rs0&directConnection=false";
                    options.DatabaseName = "windgame_test";
                });
                
                services.AddSingleton<RedisConnectionManager>();
                services.AddSingleton<MongoDbConnectionManager>();
                services.AddSingleton<IPlayerPersistenceService, PlayerPersistenceService>();
                services.AddSingleton<IRoomPersistenceService, RoomPersistenceService>();
                services.AddSingleton<IGameRecordPersistenceService, GameRecordPersistenceService>();
                
                // 注册缓存策略服务
                services.Configure<LruCacheOptions>(options =>
                {
                    options.MaxCapacity = 1000;
                    options.TargetHitRate = 0.8;
                });
                
                services.Configure<DistributedLockOptions>(options =>
                {
                    options.DefaultExpiryMinutes = 5;
                    options.DefaultTimeoutSeconds = 30;
                });
                
                services.AddSingleton<RedisCacheStrategyService>();
                services.AddSingleton<RedisDistributedLockService>();
                
                // 注册分布式事务和冲突检测服务
                services.AddSingleton<DistributedTransactionService>();
                services.AddSingleton<ConflictDetectionService>();
                
                // 配置数据同步选项
                services.Configure<DataSyncOptions>(options =>
                {
                    options.FlushIntervalMs = 1000; // 测试环境更快的刷新间隔
                    options.FlushBatchSize = 10;
                    options.MaxPendingWrites = 100;
                    options.DefaultCacheExpirySeconds = 300;
                    options.EnableStatistics = true;
                    options.MongoCollections = new[] { "Players", "Rooms", "GameRecords", "Messages" };
                    options.SyncStrategy = new SyncStrategyConfig
                    {
                        DefaultStrategy = SyncStrategyType.WriteThrough,
                        TypeStrategyOverrides = new Dictionary<string, SyncStrategyType>
                        {
                            ["PlayerState"] = SyncStrategyType.WriteThrough,
                            ["RoomState"] = SyncStrategyType.WriteThrough,
                            ["MessageInfo"] = SyncStrategyType.WriteBehind
                        }
                    };
                });
                
                // services.AddSingleton<IDataSyncService, DataSyncService>();
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
            
            // 在客户端也注册测试需要的服务 (使用测试专用端口)
            services.Configure<RedisOptions>(options =>
            {
                options.ConnectionString = "localhost:6380";
                options.Password = "windgame123";
                options.Database = 0;
                options.KeyPrefix = "Wind:Test:";
            });
            
            services.Configure<MongoDbOptions>(options =>
            {
                options.ConnectionString = "mongodb://localhost:27018/windgame_test?replicaSet=rs0&directConnection=false";
                options.DatabaseName = "windgame_test";
            });
            
            services.AddSingleton<RedisConnectionManager>();
            services.AddSingleton<MongoDbConnectionManager>();
            services.AddSingleton<RedisDistributedLockService>();
            services.AddSingleton<DistributedTransactionService>();
            services.AddSingleton<ConflictDetectionService>();
        });
    }
}
# Redis技术研究记录

## 🎯 使用指南
**查阅优先级**：
1. **优先查阅本文件** - 包含Redis相关的实际解决方案和最佳实践
2. **其次使用Context7** - 获取官方文档参考
3. **最后查阅源码** - GitHub Issues/官方源码

**记录格式**：每个技术问题包含【问题背景】【解决方案】【关键发现】【避坑指南】

---

## 📚 Redis技术案例库

### 🚨 案例1: Orleans 9.2.1 Redis存储配置API兼容性问题

**研究日期**: 2025-08-15 (北京时间)  
**解决状态**: ✅ 已解决  
**影响范围**: Orleans Redis持久化存储配置失败 → 正确的配置语法应用

#### 【问题背景】
- Orleans 9.2.1官方文档中的RedisStorageOptions配置API在实际版本中不存在
- Context7查阅的RedisStorageOptions属性（ConnectionString、Database、UseJson等）不可用
- 编译时报错：CS1061 未包含 ConnectionString、Database 等属性的定义
- 文档与实际API版本不匹配，导致按文档配置完全无法工作

#### 【错误配置方式】(来自官方文档)
```csharp
// ❌ 错误：Orleans 9.2.1中这些属性不存在
siloBuilder.AddRedisGrainStorage("PlayerStorage", options =>
{
    options.ConnectionString = "localhost:6379";  // 不存在
    options.Database = 0;                         // 不存在  
    options.UseJson = false;                      // 不存在
    options.KeyPrefix = "player";                 // 不存在
});
```

#### 【用户提供的正确解决方案】
```csharp
// ✅ 正确：Orleans 9.2.1实际可用的配置方式
siloBuilder.AddRedisGrainStorage(
    name: "PlayerStorage",
    options =>
    {
        // 使用StackExchange.Redis.ConfigurationOptions
        options.ConfigurationOptions = ConfigurationOptions.Parse("localhost:6379,password=windgame123");
        
        // 自定义存储键生成逻辑
        options.GetStorageKey = (type, id) => $"player:{type}-{id}";
    });

// 多数据库配置示例
siloBuilder.AddRedisGrainStorage(
    name: "RoomStorage", 
    options =>
    {
        var redisConfig = ConfigurationOptions.Parse("localhost:6379,password=windgame123");
        redisConfig.DefaultDatabase = 1;  // 使用数据库1
        options.ConfigurationOptions = redisConfig;
        options.GetStorageKey = (type, id) => $"room:{type}-{id}";
    });
```

#### 【关键发现】
1. **API变更**: Orleans 9.2.1使用StackExchange.Redis.ConfigurationOptions代替简单字符串配置
2. **连接配置**: 通过ConfigurationOptions.Parse()方法解析完整连接字符串，支持密码等参数
3. **数据库选择**: 通过ConfigurationOptions.DefaultDatabase属性设置Redis数据库
4. **存储键自定义**: 必须通过GetStorageKey委托自定义键生成策略，无内置KeyPrefix
5. **序列化器**: 可选配置GrainStorageSerializer，但用户建议暂时省略复杂配置

#### 【避坑指南】
1. **API兼容性**: Orleans版本升级时重点关注存储提供程序Breaking Changes
2. **文档时效性**: 官方文档可能滞后于实际API版本，需多方验证
3. **技术困难处理**: 遇到API兼容性问题时立即寻求有经验人员指导，不要自行简化
4. **配置验证**: Redis存储配置后必须实际测试连接和存储功能
5. **密码认证**: 生产Redis通常需要密码认证，连接字符串中必须包含密码参数

#### 【工作方式教训】
- **困难汇报机制**: 遇到技术困难应立即详细汇报，而非尝试简化绕过
- **用户指导价值**: 用户提供的解决方案准确有效，证明了寻求指导的重要性
- **协作解决**: 技术问题通过协作解决比单兵作战更高效

#### 【成功应用结果】
- Redis存储配置编译通过
- 支持密码认证连接
- 实现了数据库分离（database 0, 1, 2）
- 建立了有意义的存储键前缀（player:, room:, match:）
- 为后续Orleans Grain持久化奠定了正确基础

### 🚨 案例2: Orleans 9.2.1 Redis存储配置完整解决方案

**研究日期**: 2025-08-15 (北京时间)  
**解决状态**: ✅ 已解决  
**影响范围**: Orleans启动失败 → 成功实现Redis持久化存储

#### 【问题背景】
- Orleans 9.2.1使用AddRedisGrainStorage配置Redis存储时报错："Default storage provider"
- 多重配置错误：配置位置错误、API语法错误、appsettings.json冲突
- Orleans启动时无法找到正确的存储提供程序，导致分布式架构无法运行
- Microsoft.Orleans.Persistence.Redis 9.2.1的实际API与文档不匹配

#### 【错误的解决尝试过程】
```csharp
// ❌ 错误1：在Services层配置Orleans存储
builder.Services.AddRedisGrainStorage("PlayerStorage")  // 不是Orleans配置方式

// ❌ 错误2：使用不存在的API属性
siloBuilder.AddRedisGrainStorage("PlayerStorage", options =>
{
    options.ConnectionString = "localhost:6379";  // 不存在此属性
    options.Database = 0;                         // 不存在此属性  
    options.UseJson = false;                      // 不存在此属性
});

// ❌ 错误3：appsettings.json配置冲突
"Orleans": {
  "GrainStorage": {
    "PlayerStorage": { /* 配置导致"Default storage provider"错误 */ }
  }
}
```

#### 【正确解决方案】
```csharp
// ✅ 正确：在SiloBuilder层配置Redis存储
var redisConnectionString = "localhost:6379,password=windgame123";

siloBuilder
    .AddRedisGrainStorage("PlayerStorage", options => {
        var playerConfigOptions = ConfigurationOptions.Parse(redisConnectionString);
        playerConfigOptions.DefaultDatabase = 0;
        playerConfigOptions.AbortOnConnectFail = false;
        options.ConfigurationOptions = playerConfigOptions;
        Log.Information("PlayerStorage Redis配置完成: DB=0");
    })
    .AddRedisGrainStorage("RoomStorage", options => {
        var roomConfigOptions = ConfigurationOptions.Parse(redisConnectionString);
        roomConfigOptions.DefaultDatabase = 1;
        roomConfigOptions.AbortOnConnectFail = false;
        options.ConfigurationOptions = roomConfigOptions;
        Log.Information("RoomStorage Redis配置完成: DB=1");
    })
    .AddRedisGrainStorage("MatchmakingStorage", options => {
        var matchmakingConfigOptions = ConfigurationOptions.Parse(redisConnectionString);
        matchmakingConfigOptions.DefaultDatabase = 2;
        matchmakingConfigOptions.AbortOnConnectFail = false;
        options.ConfigurationOptions = matchmakingConfigOptions;
        Log.Information("MatchmakingStorage Redis配置完成: DB=2");
    });

// ✅ 关键：完全删除appsettings.json中的Orleans配置避免冲突
// 删除所有"Orleans"配置节点
```

#### 【关键发现】
1. **配置位置必须在SiloBuilder**: Orleans存储必须在`siloBuilder.AddRedisGrainStorage()`配置，不能在`builder.Services`层
2. **使用StackExchange.Redis.ConfigurationOptions**: Orleans 9.2.1使用ConfigurationOptions对象，不支持简单字符串属性
3. **appsettings.json配置冲突**: Orleans会同时读取代码配置和JSON配置，导致"Default storage provider"错误
4. **数据库分离策略**: 每个存储使用独立Redis数据库(0,1,2)，避免键名冲突
5. **AbortOnConnectFail设置**: 设为false防止Redis连接临时失败导致Orleans启动失败

#### 【技术突破点】
**关键配置API模式发现**:
```csharp
// 标准模式：使用ConfigurationOptions.Parse + 自定义数据库
options.ConfigurationOptions = ConfigurationOptions.Parse(connectionString);
options.ConfigurationOptions.DefaultDatabase = databaseNumber;
```

#### 【依赖注入修复】
同时解决了Microsoft.Extensions.Logging与Serilog的冲突:
```csharp
// ❌ 原来的错误依赖
public RedisCacheStrategy(IConnectionMultiplexer redis, IOptions<RedisOptions> redisOptions, Serilog.ILogger logger)

// ✅ 修复后的正确依赖  
public RedisCacheStrategy(IConnectionMultiplexer redis, IOptions<RedisOptions> redisOptions, ILogger<RedisCacheStrategy> logger)
```

#### 【成功验证结果】
- ✅ Orleans Silo成功启动，无"Default storage provider"错误
- ✅ PlayerGrain、RoomGrain、MatchmakingGrain均可正常创建和调用
- ✅ Redis存储配置生效，数据库0、1、2分别用于不同存储
- ✅ 测试项目依赖注入问题同步解决
- ✅ 构建编译0错误0警告

#### 【避坑指南】
1. **Orleans存储配置位置**: 绝对不能在Services层配置Orleans存储，必须在SiloBuilder层
2. **API版本兼容**: Orleans 9.2.1不支持简单字符串配置属性，必须使用ConfigurationOptions
3. **配置冲突检查**: 删除appsettings.json中所有Orleans相关配置，避免双重配置冲突
4. **日志依赖统一**: 项目内统一使用Microsoft.Extensions.Logging，避免混用Serilog接口
5. **Redis连接容错**: 设置AbortOnConnectFail=false，提高生产环境稳定性

#### 【技术价值】
这个解决方案解决了Orleans分布式架构的核心存储问题，是项目从v1.1进入v1.2数据存储层的关键技术突破。为后续PlayerGrain状态持久化、房间数据管理、匹配系统数据存储奠定了正确的技术基础。

---

*本记录将在项目进展过程中持续更新，记录新的技术发现和实践经验。*
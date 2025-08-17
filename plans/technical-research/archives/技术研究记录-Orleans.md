# Orleans技术研究记录

## 🎯 使用指南
**查阅优先级**：
1. **优先查阅本文件** - 包含Orleans相关的实际解决方案和最佳实践
2. **其次使用Context7** - 获取官方文档参考
3. **最后查阅源码** - GitHub Issues/官方源码

**记录格式**：每个技术问题包含【问题背景】【解决方案】【关键发现】【避坑指南】

---

## 📚 Orleans技术案例库

### 🚨 案例1: Orleans 9.2.1 MessagePack序列化配置冲突

**研究日期**: 2025-08-13 (北京时间)  
**解决状态**: ✅ 已解决  
**影响范围**: Orleans Silo启动失败 → 生产环境和测试环境正常运行

#### 【问题背景】
- Orleans Silo启动时报错：`Found unserializable or uncopyable types`
- Context7文档建议同时使用Orleans原生序列化和MessagePack序列化
- 实际配置导致序列化器冲突，Silo无法启动

#### 【错误配置方式】(来自Context7文档)
```csharp
// ❌ 错误：混合使用两种序列化器导致冲突
[GenerateSerializer]     // Orleans原生序列化
[MessagePackObject]      // MessagePack序列化
public class PlayerState
{
    [Id(0)]             // Orleans序列化ID
    [Key(0)]            // MessagePack序列化Key
    public string PlayerId { get; set; }
}
```

#### 【正确解决方案】
```csharp
// ✅ 正确：仅使用MessagePack序列化
[MessagePackObject]
public class PlayerState 
{
    [MPKey(0)]  // 使用alias避免命名冲突
    public string PlayerId { get; set; }
}

// using alias解决命名空间冲突
using MPKey = MessagePack.KeyAttribute;

// Orleans Silo配置 
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder
        .UseLocalhostClustering()
        .AddMemoryGrainStorage("PlayerStorage");
});

// 在Services层配置MessagePack序列化器
builder.Services.AddSerializer(serializerBuilder => 
    serializerBuilder.AddMessagePackSerializer());
```

#### 【关键发现】
1. **序列化器互斥**: Orleans 9.2.1中，原生序列化和MessagePack不能同时使用
2. **配置位置**: MessagePack序列化器应在`Services`层配置，不是在`SiloBuilder`层
3. **命名空间冲突**: `System.ComponentModel.DataAnnotations.KeyAttribute` vs `MessagePack.KeyAttribute`
4. **包依赖**: 需要`Microsoft.Orleans.Serialization.MessagePack`包

#### 【避坑指南】
- ⚠️ **不要**同时使用`[GenerateSerializer]`和`[MessagePackObject]`
- ⚠️ **不要**在SiloBuilder上直接调用`AddSerializer`
- ✅ **使用**alias解决Key属性冲突：`using MPKey = MessagePack.KeyAttribute`
- ✅ **确保**测试环境和生产环境配置一致
- ✅ **优先**查阅本记录，Context7的Orleans文档可能包含过时的混合配置示例

### 🔍 案例2: Orleans 9.2.1 MemoryPack序列化可行性评估

**研究日期**: 2025-08-13 (北京时间)  
**解决状态**: 📊 已评估完成  
**影响范围**: 技术选型决策 - MessagePack vs MemoryPack

#### 【问题背景】
- MemoryPack性能比MessagePack快2-5倍，考虑技术升级
- 用户询问为什么不使用更快的MemoryPack序列化器
- 需要评估Orleans对MemoryPack的支持情况

#### 【研究发现】
**Context7查询结果**: Orleans 9.2.1官方支持的序列化器
- ✅ MessagePack (`Microsoft.Orleans.Serialization.MessagePack`)
- ✅ System.Text.Json (`Microsoft.Orleans.Serialization.SystemTextJson`)
- ✅ Newtonsoft.Json (`Microsoft.Orleans.Serialization.NewtonsoftJson`)
- ✅ Protobuf (`Microsoft.Orleans.Serialization.Protobuf`)
- ✅ F# 原生支持 (`Microsoft.Orleans.Serialization.FSharp`)
- ❌ **无MemoryPack官方支持**

**WebSearch补充结果**:
- MemoryPack基准测试显示比MessagePack快2-5倍
- Orleans提供可扩展序列化框架，理论上可自定义集成
- 但需要社区驱动开发，无官方支持计划

#### 【技术评估】
**MemoryPack优势**:
- 🚀 **性能**: 比MessagePack快2-5倍
- 🎯 **C#优化**: 专为.NET 7+和C# 11优化
- 💾 **内存效率**: 零拷贝序列化
- 🔧 **现代化**: 使用增量源生成器

**当前选择MessagePack的原因**:
- ✅ **官方支持**: Orleans官方维护的集成包
- ✅ **稳定性**: 成熟的生产环境验证
- ✅ **生态兼容**: 与Unity、其他语言客户端兼容
- ✅ **维护成本**: 无需自维护集成代码

#### 【技术决策建议】
**短期**: 继续使用MessagePack
- 风险低，稳定可靠
- 性能已经比JSON序列化快3-5倍
- 官方支持和维护

**长期**: 关注MemoryPack发展
- 监控Orleans社区对MemoryPack的集成需求
- 评估自定义序列化器开发成本
- 在v2.0架构升级时重新评估

#### 【避坑指南】
- ⚠️ **不建议**在生产环境中使用自定义MemoryPack集成
- ⚠️ **性能优化**应先优化业务逻辑，序列化通常不是瓶颈
- ✅ **基准测试**如需验证性能影响，应建立完整的基准测试框架
- ✅ **技术监控**定期检查Orleans社区对MemoryPack的官方支持进展

---

## Orleans .NET 9 研究记录

### 研究日期: 2025-08-11 (北京时间)
### 研究方式: WebSearch (Context7暂时无法访问)
### 研究来源: Microsoft Learn官方文档

#### 核心发现

##### 1. Orleans现代化宿主模式 (.NET 9兼容)
```csharp
// Program.cs - 现代化的Orleans Silo宿主配置
using Microsoft.Extensions.Hosting;

await Host.CreateDefaultBuilder(args)
    .UseOrleans(siloBuilder => 
    {
        siloBuilder.UseLocalhostClustering(); // 本地开发环境
        siloBuilder.ConfigureEndpoints(siloPort: 11111, gatewayPort: 30000);
    })
    .RunConsoleAsync();
```

##### 2. 生产环境配置示例
```csharp
using IHost host = Host.CreateDefaultBuilder(args)
    .UseOrleans(builder => 
    {
        builder.UseAzureStorageClustering(options => 
            options.ConfigureTableServiceClient(connectionString));
    })
    .UseConsoleLifetime()
    .Build();
```

##### 3. 关键配置要素
- **ClusterId**: 集群唯一标识，同一集群内所有Silo和Client可以直接通信
- **ServiceId**: 应用唯一标识，用于持久化提供程序，部署过程中应保持稳定
- **端口配置**: 
  - 默认Silo间通信端口: 11111
  - 默认Client-Silo通信端口: 30000
  - 可通过ConfigureEndpoints自定义

##### 4. 必需的NuGet包
- **开发Silo**: `Microsoft.Orleans.Server` + `Microsoft.Extensions.Hosting`
- **架构**: 遵循.NET现代化配置模式和依赖注入

---

*本记录将在项目进展过程中持续更新，记录新的技术发现和实践经验。*
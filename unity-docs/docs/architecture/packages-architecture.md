# Wind Unity包架构设计

> **文档版本**: v1.0.0  
> **创建时间**: 2025-08-29 (北京时间)  
> **适用项目版本**: Wind Unity v1.0+  
> **关联文档**: `unity-纲领.md`, `user-onboarding.md`  
> **最后更新**: 2025-08-29  

---

## 📋 版本变更历史

| 版本 | 日期 | 变更内容 | 影响范围 |
|------|------|----------|----------|
| v1.0.0 | 2025-08-29 | 创建Unity包架构完整设计文档 | 新增功能 |

---

## 🎯 架构概览

Wind Unity客户端采用**统一包 + 智能适配**的创新架构，通过单一com.wind.core包提供完整功能，根据项目环境自动启用相应模块，彻底解决用户选择困惑和功能分散问题。

### 核心设计理念
- **零选择成本**: 用户只需安装com.wind.core，无需选择功能版本
- **智能环境适配**: 自动检测项目需求，按需启用功能模块
- **统一用户体验**: 从单机游戏到多人网络游戏的平滑升级路径
- **精确数据统计**: 所有用户使用同一包，获得准确的使用情况分析

## 🏗️ 分层包架构

### Layer 0: 基础设施层 (Foundation Layer)

#### com.wind.core - 核心统一包 🎯
```csharp
// 智能适配示例
public static class WindFramework
{
    public static void Initialize(WindConfig config = null)
    {
        config ??= WindConfig.AutoDetect();
        
        // 基础模块(总是启用)
        InitializeCore();
        InitializeDIContainer();
        InitializeLogging(config.EnableSerilog);
        
        // 按需模块
        if (config.HasNetworkCapability)
            InitializeNetworking();
            
        if (config.SupportsHotUpdate)
            InitializeHotUpdate();
            
        WindLogger.Info($"Wind框架初始化完成: {config}");
    }
}
```

**核心功能**:
- 自研DI容器: 零反射、零GC、编译时检查
- 智能适配器: 环境检测和功能按需启用
- 配置系统: 支持热重载的统一配置管理
- 基础工具: 扩展方法、工具类、性能监控

**技术特性**:
- 启动时间: <100ms
- 内存占用: <5MB
- 支持Unity版本: 2022.3 LTS+
- 平台兼容: Windows/Mac/Android/iOS/WebGL

#### com.wind.serilog - 可选日志增强 📝
```csharp
// 自动替换默认日志
public class SerilogWindLogger : IWindLogger
{
    public void Log(LogLevel level, string message, Exception ex = null)
    {
        Serilog.Log.Write(level.ToSerilogLevel(), ex, message);
    }
}

// 使用方式
[RuntimeInitializeOnLoadMethod]
static void InitializeSerilog()
{
    if (WindPackageDetector.HasSerilogPackage())
    {
        WindServiceContainer.Replace<IWindLogger, SerilogWindLogger>();
    }
}
```

#### com.wind.config - 配置管理系统 ⚙️
- 分层配置: Development/Staging/Production
- 热重载支持: 运行时配置更新
- 类型安全: 强类型配置对象
- 环境变量集成: 敏感信息外部化

#### com.wind.packagemanager - 包管理器UI扩展 📦
```csharp
// Unity编辑器扩展
[MenuItem("Wind/Package Manager")]
public static void OpenWindPackageManager()
{
    WindPackageManagerWindow.Open();
}

public class WindPackageManagerWindow : EditorWindow
{
    // GitHub PAT认证
    // 私有Registry连接
    // 包依赖分析和冲突解决
    // 一键安装和更新
}
```

### Layer 1: 框架服务层 (Framework Layer)

#### com.wind.network - 网络通信 🌐
- MagicOnion客户端完整封装
- 自动重连和故障转移
- 连接池管理和性能优化
- 统一的异步调用接口

#### com.wind.hotfix - 热更新系统 🔥
```csharp
// HybridCLR集成封装
public static class WindHotUpdate
{
    public static async Task<bool> CheckAndApplyUpdatesAsync()
    {
        var updates = await HotUpdateChecker.CheckUpdatesAsync();
        if (updates.HasUpdates)
        {
            return await HotUpdateApplier.ApplyAsync(updates);
        }
        return false;
    }
}
```

#### com.wind.assets - 自研资源管理 📁
**借鉴YooAsset核心思想的完全自研实现**:
- 可寻址资源定位系统
- 引用计数安全管理
- 边玩边下载异步加载
- 版本控制和增量更新
- 多种缓存策略支持

#### com.wind.storage - 本地存储 💾
- 跨平台存储抽象
- 数据加密和压缩
- 版本化数据迁移
- 存储空间管理

#### com.wind.localserver - 本地服务器服务 🖥️
```csharp
// Unity内嵌服务器管理
[MenuItem("Wind/Local Server/Start All")]
public static void StartLocalServices()
{
    LocalServerManager.StartRedis();
    LocalServerManager.StartMongoDB();
    LocalServerManager.StartOrleansHost();
    LocalServerManager.StartMagicOnionServer();
    
    EditorUtility.DisplayDialog("Wind", "本地服务器已启动", "OK");
}
```

### Layer 2: 游戏系统层 (Game Systems Layer)

#### com.wind.ui - UI框架 🎨
- UGUI和UI Toolkit统一封装
- MVVM架构模式支持
- 响应式UI更新机制
- 主题和本地化支持

#### com.wind.input - 输入系统 🎮
- Unity Input System封装
- 多设备输入统一管理
- 输入映射和配置系统
- 手势识别和触控支持

#### com.wind.audio - 音频系统 🔊
- 3D空间音效支持
- 音频资源池管理
- 动态音频加载
- 音效和背景音乐分离管理

#### com.wind.effects - 特效动画系统 ✨
- 粒子系统集成
- Timeline和Animation统一管理
- 特效资源池和性能优化
- 可编程渲染管线支持

#### com.wind.scene - 场景管理 🎬
- 异步场景加载
- 场景资源预加载
- 场景过渡动画
- 场景数据持久化

### Layer 3: 业务模块层 (Business Module Layer)

#### 游戏类型专用包
- **com.wind.rts**: RTS游戏专用功能(单位管理、战争迷雾、建筑系统)
- **com.wind.moba**: MOBA游戏专用功能(英雄技能、装备系统、匹配机制)  
- **com.wind.rpg**: RPG游戏专用功能(角色成长、任务系统、背包管理)
- **com.wind.simulation**: 模拟经营专用功能(资源管理、建设系统、经济模拟)

### Layer 4-5: 工具服务层 (Tools & Services Layer)

#### 开发工具包
- **com.wind.editor**: Unity编辑器扩展工具
- **com.wind.debug**: 运行时调试面板
- **com.wind.testing**: 自动化测试框架
- **com.wind.profiler**: 性能分析工具

#### 企业级服务
- **com.wind.monitoring**: 实时性能监控
- **com.wind.security**: 安全防护服务
- **com.wind.cicd**: CI/CD集成工具
- **com.wind.docs**: 自动化文档生成

## 🔄 依赖关系设计

### 严格分层依赖规则
```
依赖方向: Layer N → Layer N-1 (单向依赖)

Layer 4-5 ↓
Layer 3   ↓  
Layer 2   ↓
Layer 1   ↓
Layer 0   (基础层)
```

### 依赖关系矩阵
| From/To | Layer 0 | Layer 1 | Layer 2 | Layer 3 | Layer 4-5 |
|---------|---------|---------|---------|---------|-----------|
| Layer 0 | ❌      | ❌      | ❌      | ❌      | ❌        |
| Layer 1 | ✅      | ❌      | ❌      | ❌      | ❌        |
| Layer 2 | ✅      | ✅      | ❌      | ❌      | ❌        |
| Layer 3 | ✅      | ✅      | ✅      | ❌      | ❌        |
| Layer 4-5| ✅      | ✅      | ✅      | ✅      | ❌        |

### 循环依赖避免策略
```csharp
// 使用接口和事件解耦
public interface IWindEventBus
{
    void Publish<T>(T eventData);
    void Subscribe<T>(Action<T> handler);
}

// 避免直接类型依赖
public class PlayerSystem
{
    void OnLevelUp() => WindEvents.Publish(new PlayerLevelUpEvent());
}

public class UISystem  
{
    void Start() => WindEvents.Subscribe<PlayerLevelUpEvent>(OnPlayerLevelUp);
}
```

## 🎯 统一包智能适配机制

### 环境检测算法
```csharp
public static class WindCapabilityDetector
{
    public static WindCapabilities Detect()
    {
        var caps = new WindCapabilities();
        
        // 检测网络依赖
        caps.HasMagicOnion = HasAssembly("MagicOnion.Client");
        caps.HasNetworkCode = HasDefine("WIND_NETWORK");
        
        // 检测热更新依赖
        caps.HasHybridCLR = HasAssembly("HybridCLR.Runtime");
        caps.HasHotUpdateCode = HasDefine("WIND_HOTUPDATE");
        
        // 检测平台特性
        caps.Platform = Application.platform;
        caps.IsEditor = Application.isEditor;
        caps.IsDevelopmentBuild = Debug.isDebugBuild;
        
        return caps;
    }
}
```

### 按需模块加载
```csharp
public class WindModuleLoader
{
    private static readonly Dictionary<Type, IWindModule> _modules = new();
    
    public static T LoadModule<T>() where T : class, IWindModule, new()
    {
        if (!_modules.ContainsKey(typeof(T)))
        {
            var module = new T();
            module.Initialize();
            _modules[typeof(T)] = module;
        }
        return (T)_modules[typeof(T)];
    }
    
    public static void LoadConditionalModules(WindCapabilities caps)
    {
        // 基础模块
        LoadModule<CoreModule>();
        LoadModule<ConfigModule>();
        
        // 条件模块
        if (caps.HasMagicOnion)
            LoadModule<NetworkModule>();
            
        if (caps.HasHybridCLR)
            LoadModule<HotUpdateModule>();
    }
}
```

### 功能开关配置
```csharp
[CreateAssetMenu(fileName = "WindConfig", menuName = "Wind/Framework Config")]
public class WindFrameworkConfig : ScriptableObject
{
    [Header("基础设置")]
    public bool enableDebugMode = true;
    public LogLevel minLogLevel = LogLevel.Info;
    
    [Header("可选功能")]
    public bool enableSerilogIntegration = false;
    public bool forceOfflineMode = false;
    
    [Header("性能设置")]  
    public int maxConcurrentAssetLoads = 10;
    public float assetUnloadDelay = 30f;
    
    [Header("网络设置")]
    [ShowIf("@HasNetworkCapability()")]
    public string serverAddress = "localhost:5271";
    public int connectionTimeout = 10000;
    
    public bool HasNetworkCapability()
    {
        return WindCapabilityDetector.Detect().HasMagicOnion;
    }
}
```

## 🚀 用户体验设计

### 零配置初始化
```csharp
// 最简单的使用方式
public class GameBootstrap : MonoBehaviour
{
    async void Start()
    {
        // 一行代码完成所有初始化
        await WindFramework.InitializeAsync();
        
        // 框架会自动:
        // 1. 检测环境能力
        // 2. 加载必要模块  
        // 3. 配置日志系统
        // 4. 启用合适功能
        
        StartGame();
    }
}
```

### 渐进式功能启用
```csharp
// 开发过程中平滑升级
public class GameClient : MonoBehaviour
{
    async void Start()
    {
        var config = WindConfig.Create()
            .EnableNetworking()      // 启用网络功能
            .EnableHotUpdate()       // 启用热更新
            .EnableProfiling();      // 启用性能分析
            
        await WindFramework.InitializeAsync(config);
    }
}
```

## 📊 性能指标和监控

### 核心性能指标
- **初始化时间**: 目标<100ms，监控平均值和P95
- **内存使用**: 基础<5MB，每个功能模块<2MB
- **包大小**: com.wind.core<10MB，可选模块<5MB
- **启动性能**: 框架初始化不影响游戏启动时间

### 监控和统计
```csharp
public static class WindTelemetry
{
    public static void TrackModuleLoad(string moduleName, TimeSpan duration)
    {
        var data = new
        {
            Module = moduleName,
            Duration = duration.TotalMilliseconds,
            Platform = Application.platform.ToString(),
            UnityVersion = Application.unityVersion
        };
        
        // 发送到统计服务器
        TelemetryService.Track("module_load", data);
    }
    
    public static void TrackFeatureUsage(string feature, Dictionary<string, object> properties)
    {
        TelemetryService.Track($"feature_{feature}", properties);
    }
}
```

## 🔧 包开发规范

### 标准包结构
```
com.wind.example/
├── package.json              # 包元数据和依赖
├── README.md                 # 包说明文档
├── CHANGELOG.md              # 变更历史
├── LICENSE.md                # 许可证信息
├── Runtime/                  # 运行时代码
│   ├── Scripts/              # C#脚本
│   ├── Resources/            # 运行时资源
│   └── com.wind.example.asmdef
├── Editor/                   # 编辑器代码
│   ├── Scripts/              # 编辑器脚本
│   └── com.wind.example.editor.asmdef
├── Tests/                    # 测试代码
│   ├── Runtime/              # 运行时测试
│   └── Editor/               # 编辑器测试
├── Documentation~/           # 包文档
│   ├── index.md              # API文档入口
│   └── examples/             # 使用示例
└── Samples~/                 # 示例代码
    └── BasicExample/
```

### 包依赖声明
```json
{
  "name": "com.wind.example",
  "version": "1.0.0",
  "dependencies": {
    "com.wind.core": "1.0.0"
  },
  "optionalDependencies": {
    "com.unity.addressables": "1.19.19"
  },
  "keywords": ["wind", "framework", "unity"],
  "author": "Wind Development Team"
}
```

## 💡 最佳实践

### 1. 模块设计原则
- **单一职责**: 每个包专注一个特定领域
- **松耦合**: 通过接口和事件通信，避免直接依赖
- **高内聚**: 相关功能集中在同一个包中
- **可测试**: 所有public API都要有对应测试

### 2. 性能优化策略
- **懒加载**: 非必要功能延迟初始化
- **对象池**: 频繁创建的对象使用对象池
- **异步优先**: 所有IO操作使用异步模式
- **内存管理**: 及时释放不再使用的资源

### 3. 错误处理规范
```csharp
public class WindException : Exception
{
    public WindErrorCode ErrorCode { get; }
    public string Context { get; }
    
    public WindException(WindErrorCode code, string message, string context = null) 
        : base(message)
    {
        ErrorCode = code;
        Context = context;
    }
}

// 使用示例
public async Task<PlayerData> LoadPlayerDataAsync(string playerId)
{
    try
    {
        return await PlayerDataService.LoadAsync(playerId);
    }
    catch (Exception ex)
    {
        throw new WindException(
            WindErrorCode.PlayerDataLoadFailed, 
            $"Failed to load player data: {ex.Message}",
            $"PlayerId: {playerId}"
        );
    }
}
```

---

## 🔗 相关文档

- [Unity客户端纲领](../plans/project-management/governance/unity-纲领.md) - 完整技术决策和架构原则
- [用户入手流程](../user-guides/user-onboarding.md) - 从GitHub到实际使用的完整指南
- [技术分析报告](../plans/technical-research/current/technical-analysis.md) - 深度技术分析
- [实施路线图](../plans/project-management/roadmaps/implementation-roadmap.md) - 44-52周开发计划

---

*Wind Unity包架构设计体现了现代软件工程的最佳实践，通过统一包+智能适配的创新策略，为Unity开发者提供零学习成本、高性能、企业级的游戏开发框架。*
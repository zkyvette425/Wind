# com.wind.core - Wind核心框架包

> **包名**: com.wind.core  
> **版本**: 1.0.0  
> **Unity版本**: 2022.3+  
> **状态**: 核心包，所有其他包的基础依赖  

---

## 📋 包信息

| 属性 | 值 |
|------|-----|
| 包名 | com.wind.core |
| 显示名 | Wind Core Framework |
| 版本 | 1.0.0 |
| Unity要求 | 2022.3+ |
| .NET要求 | .NET Standard 2.1 |
| 包类型 | 核心基础包 |

---

## ✨ 功能特性

- 🚀 **自研DI容器** - 零反射、零GC分配的高性能依赖注入
- ⚡ **智能适配机制** - 自动环境检测，按需启用功能模块
- 🛡️ **统一配置管理** - 多环境配置，类型安全，热重载支持
- 📊 **结构化日志** - 高性能日志系统，支持多种输出目标
- 🔧 **扩展方法库** - Unity开发常用扩展方法集合
- 🎯 **事件总线** - 松耦合的事件通信机制

---

## 🔧 安装

### 通过Wind Package Manager安装（推荐）
1. 打开Unity编辑器
2. 选择 `Wind > Package Manager`
3. 搜索 `com.wind.core`
4. 点击安装

### 通过Unity Package Manager安装
1. 打开 `Window > Package Manager`
2. 点击 `+` 按钮
3. 选择 `Add package from git URL`
4. 输入：`https://github.com/wind-org/com.wind.core.git`

---

## 🚀 快速开始

### 基础初始化
```csharp
using Wind.Core;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private async void Start()
    {
        // 自动初始化Wind框架
        await WindFramework.InitializeAsync();
        
        Debug.Log("Wind框架初始化完成");
        
        // 开始游戏逻辑
        StartGame();
    }
    
    private void StartGame()
    {
        // 使用DI容器获取服务
        var playerService = WindContainer.Resolve<IPlayerService>();
        playerService.InitializePlayer();
    }
}
```

### DI容器使用
```csharp
using Wind.Core.DI;

// 服务注册（通常在游戏启动时）
public class ServiceRegistration : MonoBehaviour
{
    private void Awake()
    {
        // 注册单例服务
        WindContainer.RegisterSingleton<IPlayerService, PlayerService>();
        
        // 注册瞬时服务
        WindContainer.RegisterTransient<IWeapon, Sword>();
        
        // 注册作用域服务
        WindContainer.RegisterScoped<IGameSession, GameSession>();
    }
}

// 服务使用
public class PlayerController : MonoBehaviour
{
    private IPlayerService _playerService;
    
    private void Start()
    {
        // 依赖注入
        _playerService = WindContainer.Resolve<IPlayerService>();
        
        // 或者使用属性注入
        WindContainer.Inject(this);
    }
    
    // 属性注入示例
    [Inject]
    public IInputService InputService { get; set; }
}
```

### 智能适配使用
```csharp
using Wind.Core.Adaptation;

public class NetworkFeature : MonoBehaviour
{
    private void Start()
    {
        // 检查网络功能是否可用
        if (WindCapabilities.HasNetworkSupport)
        {
            InitializeNetworking();
        }
        else
        {
            Debug.Log("网络功能未启用，使用离线模式");
            InitializeOfflineMode();
        }
    }
    
    private void InitializeNetworking()
    {
        // 初始化网络功能
        var networkService = WindContainer.Resolve<INetworkService>();
        networkService.Connect("game-server.com");
    }
}
```

---

## 🏗️ 核心架构

### DI容器架构
```
WindContainer (核心DI容器)
├── ServiceRegistry (服务注册表)
├── ServiceResolver (服务解析器)
├── LifecycleManager (生命周期管理)
└── CircularDependencyDetector (循环依赖检测)
```

### 智能适配架构
```
WindCapabilityDetector (能力检测器)
├── AssemblyDetector (程序集检测)
├── DefineSymbolDetector (宏定义检测)
├── PlatformDetector (平台检测)
└── EnvironmentDetector (环境检测)
```

---

## 📖 API参考

### WindContainer - DI容器
```csharp
public static class WindContainer
{
    // 服务注册
    public static void RegisterSingleton<TInterface, TImplementation>()
        where TImplementation : class, TInterface;
    
    public static void RegisterTransient<TInterface, TImplementation>()
        where TImplementation : class, TInterface;
    
    public static void RegisterScoped<TInterface, TImplementation>()
        where TImplementation : class, TInterface;
    
    // 服务解析
    public static T Resolve<T>();
    public static object Resolve(Type type);
    
    // 依赖注入
    public static void Inject(object target);
    
    // 容器管理
    public static void Initialize();
    public static void Dispose();
}
```

### WindCapabilities - 能力检测
```csharp
public static class WindCapabilities
{
    // 功能检测
    public static bool HasNetworkSupport { get; }
    public static bool HasHotUpdateSupport { get; }
    public static bool HasAdvancedUI { get; }
    
    // 平台检测
    public static RuntimePlatform Platform { get; }
    public static bool IsMobile { get; }
    public static bool IsEditor { get; }
    
    // 环境检测
    public static bool IsDevelopment { get; }
    public static bool IsProduction { get; }
}
```

### WindLogger - 日志系统
```csharp
public static class WindLogger
{
    // 基础日志
    public static void Debug(string message);
    public static void Info(string message);
    public static void Warning(string message);
    public static void Error(string message);
    
    // 结构化日志
    public static void Log(LogLevel level, string message, object context = null);
    
    // 性能日志
    public static IDisposable BeginScope(string operation);
}
```

---

## ⚙️ 配置选项

### WindConfig - 框架配置
```csharp
[CreateAssetMenu(fileName = "WindConfig", menuName = "Wind/Framework Config")]
public class WindConfig : ScriptableObject
{
    [Header("DI容器设置")]
    public bool EnableCircularDependencyCheck = true;
    public bool EnablePerformanceMonitoring = false;
    
    [Header("日志设置")]
    public LogLevel MinLogLevel = LogLevel.Info;
    public bool EnableFileLogging = false;
    public string LogFilePath = "Logs/wind.log";
    
    [Header("智能适配设置")]
    public bool EnableAutoDetection = true;
    public bool EnableFallbackMode = true;
    
    [Header("性能设置")]
    public int MaxServicesPerContainer = 1000;
    public bool EnableServiceCaching = true;
}
```

### 配置使用示例
```csharp
// 使用自定义配置初始化
public class CustomBootstrap : MonoBehaviour
{
    [SerializeField] private WindConfig customConfig;
    
    private async void Start()
    {
        // 使用自定义配置初始化
        await WindFramework.InitializeAsync(customConfig);
        
        // 验证配置生效
        Debug.Log($"最小日志级别: {WindLogger.MinLevel}");
    }
}
```

---

## 🧪 示例项目

### HelloWind - 5分钟快速体验
展示Wind框架的基本使用，包括DI容器、日志系统、配置管理的简单使用。

**位置**: `Samples~/HelloWind/`
**运行时间**: 约5分钟
**学习内容**: 
- Wind框架初始化
- 基础DI容器使用
- 智能适配机制

### AdvancedExample - 高级功能演示
展示Wind框架的高级功能，包括自定义配置、性能监控、扩展开发等。

**位置**: `Samples~/AdvancedExample/`
**运行时间**: 约30分钟
**学习内容**:
- 自定义服务注册和解析
- 配置热重载机制
- 性能监控和优化

---

## 🔧 故障排除

### 常见问题1: DI容器初始化失败
**错误信息**: "WindContainer not initialized"

**解决方案**:
1. 确保调用了 `WindFramework.InitializeAsync()`
2. 检查初始化是否在合适的生命周期中调用
3. 验证没有在初始化前使用容器

### 常见问题2: 循环依赖错误
**错误信息**: "Circular dependency detected"

**解决方案**:
1. 检查服务依赖关系，避免A依赖B，B又依赖A
2. 使用工厂模式或延迟初始化打破循环
3. 重新设计接口，减少直接依赖

### 常见问题3: 性能问题
**问题描述**: 服务解析速度慢

**解决方案**:
1. 启用服务缓存：`WindConfig.EnableServiceCaching = true`
2. 减少不必要的服务注册
3. 使用单例模式替代瞬时服务

---

## 📊 性能基准

### DI容器性能
- **初始化时间**: < 50ms (1000个服务)
- **服务解析**: < 0.1ms (缓存启用)
- **内存占用**: < 10MB (1000个服务)
- **GC分配**: 零分配 (运行时解析)

### 智能适配性能
- **能力检测**: < 5ms (首次)
- **模块启用**: < 10ms (单个模块)
- **配置加载**: < 1ms (热重载)

---

## 🔗 相关包

### 直接增强包
- [com.wind.serilog](../com.wind.serilog/README.md) - 可选日志增强，自动替换核心日志
- [com.wind.config](../com.wind.config/README.md) - 高级配置管理，扩展基础配置功能

### 依赖此包的包
- 所有其他Wind包都依赖com.wind.core
- 建议作为第一个安装的包

---

## 📋 系统要求

- **Unity版本**: 2022.3 LTS 或更高
- **.NET版本**: .NET Standard 2.1
- **支持平台**: Windows, macOS, Linux, Android, iOS, WebGL
- **最小内存**: 2GB RAM
- **存储空间**: 约5MB

---

## 🤝 贡献指南

我们欢迎社区贡献！请查看以下指南：

1. **代码贡献**: Fork仓库，创建功能分支，提交PR
2. **Bug报告**: 通过GitHub Issues报告问题
3. **功能建议**: 通过Discussions讨论新功能
4. **文档改进**: 改进文档内容和示例

### 开发环境设置
```bash
# 克隆仓库
git clone https://github.com/wind-org/com.wind.core.git

# 安装依赖
# Unity 2022.3+会自动处理依赖

# 运行测试
# 在Unity中运行Test Runner
```

---

## 📄 许可证

本包基于 [MIT许可证](LICENSE) 开源。

---

## 📞 支持

### 技术支持
- **GitHub Issues**: [报告问题](https://github.com/wind-org/com.wind.core/issues)
- **开发者社区**: [Wind社区论坛](https://community.wind.com)
- **API文档**: [在线API参考](https://docs.wind.com/unity/core/)

### 企业支持
- **邮件支持**: support@wind.com
- **技术咨询**: 提供付费技术咨询服务
- **定制开发**: 支持企业定制功能开发

---

**🔄 更新频率**: 本包遵循语义化版本控制，主版本每年发布，次版本每季度发布，补丁版本按需发布。

**📈 路线图**: 查看 [实施路线图](../../../plans/project-management/roadmaps/implementation-roadmap.md) 了解未来开发计划。
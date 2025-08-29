# Wind Unity包开发模板

> **模板版本**: v1.0.0  
> **适用范围**: com.wind.* 系列包开发  
> **创建时间**: 2025-08-30 (北京时间)  
> **使用说明**: 复制此模板开始新包开发  

---

## 📋 包基本信息

### 包标识信息
- **包名**: com.wind.[category].[name]
- **显示名**: Wind [功能描述]
- **版本**: 1.0.0
- **Unity版本要求**: 2022.3+
- **开发者**: Wind Framework Team

### 包分类和依赖
- **所属层级**: Layer X (基础设施/框架服务/游戏系统/业务模块/工具服务)
- **核心依赖**: 
  - com.wind.core (必需)
  - 其他依赖包
- **可选依赖**: 
  - 可选增强包

---

## 🎯 功能设计

### 核心功能描述
[详细描述包的核心功能和解决的问题]

### 功能特性列表
- [ ] 核心功能1
- [ ] 核心功能2  
- [ ] 高级功能1
- [ ] 可选功能1

### API设计概览
```csharp
// 核心API接口设计
namespace Wind.[Category]
{
    public interface I[MainService]
    {
        // 核心API方法
        Task<Result> MainFunctionAsync(Parameters parameters);
        
        // 配置API
        void Configure([ConfigType] config);
        
        // 事件API
        event Action<EventArgs> OnEvent;
    }
    
    // 主要实现类
    public class [MainService] : I[MainService]
    {
        // 实现细节
    }
    
    // 配置类
    [Serializable]
    public class [ConfigType]
    {
        // 配置属性
    }
}
```

---

## 📁 包结构规范

### 标准目录结构
```
com.wind.[category].[name]/
├── package.json              # 包元数据
├── README.md                # 包文档
├── CHANGELOG.md             # 版本变更记录
├── Runtime/                 # 运行时代码
│   ├── Wind[Name].asmdef    # Assembly Definition
│   ├── Core/                # 核心功能
│   │   ├── Interfaces/      # 接口定义
│   │   ├── Services/        # 服务实现
│   │   └── Models/          # 数据模型
│   ├── Utils/               # 工具类
│   ├── Extensions/          # 扩展方法
│   └── Resources/           # 运行时资源
├── Editor/                  # 编辑器扩展
│   ├── Wind[Name]Editor.asmdef
│   ├── Windows/             # 编辑器窗口
│   ├── Inspectors/          # 自定义Inspector
│   ├── Tools/               # 编辑器工具
│   └── Resources/           # 编辑器资源
├── Tests/                   # 测试代码
│   ├── Runtime/             # 运行时测试
│   │   ├── Wind[Name]Tests.asmdef
│   │   ├── Unit/            # 单元测试
│   │   ├── Integration/     # 集成测试
│   │   └── Performance/     # 性能测试
│   └── Editor/              # 编辑器测试
│       ├── Wind[Name]EditorTests.asmdef
│       └── Tools/           # 编辑器工具测试
├── Samples~/                # 示例代码
│   ├── BasicExample/        # 基础示例
│   ├── AdvancedExample/     # 高级示例
│   └── Documentation/       # 示例文档
└── Documentation~/          # 详细文档
    ├── manual/              # 用户手册
    ├── api/                 # API文档
    └── tutorials/           # 教程文档
```

### package.json模板
```json
{
  "name": "com.wind.[category].[name]",
  "version": "1.0.0",
  "displayName": "Wind [功能名称]",
  "description": "[详细功能描述，解决什么问题，为什么有用]",
  "unity": "2022.3",
  "keywords": [
    "wind",
    "[category]",
    "[关键词1]",
    "[关键词2]"
  ],
  "author": {
    "name": "Wind Framework Team",
    "email": "dev@wind.com",
    "url": "https://wind.com"
  },
  "dependencies": {
    "com.wind.core": "1.0.0"
  },
  "optionalDependencies": {
    "com.wind.[optional-package]": "1.0.0"
  },
  "repository": {
    "type": "git",
    "url": "https://github.com/wind-org/com.wind.[category].[name].git"
  },
  "license": "MIT",
  "samples": [
    {
      "displayName": "基础示例",
      "description": "展示[包名]的基本使用方法",
      "path": "Samples~/BasicExample"
    },
    {
      "displayName": "高级示例", 
      "description": "展示[包名]的高级功能和最佳实践",
      "path": "Samples~/AdvancedExample"
    }
  ]
}
```

---

## 🔧 开发规范

### 代码规范

#### 命名约定
```csharp
// 命名空间：Wind.[Category]
namespace Wind.Assets
{
    // 接口：I + 名词
    public interface IAssetManager { }
    
    // 类：名词，PascalCase
    public class AssetManager { }
    
    // 方法：动词 + 名词，PascalCase
    public async Task<Result> LoadAssetAsync(string path) { }
    
    // 属性：名词，PascalCase
    public bool IsInitialized { get; }
    
    // 字段：_camelCase
    private readonly ILogger _logger;
    
    // 常量：UPPER_CASE
    private const int MAX_RETRY_COUNT = 3;
    
    // 事件：On + 动词过去式
    public event Action<AssetEventArgs> OnAssetLoaded;
}
```

#### 异步编程规范
```csharp
// 正确的异步方法命名和实现
public class AssetService
{
    // 异步方法必须以Async结尾
    public async Task<AssetHandle<T>> LoadAssetAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object
    {
        // 验证参数
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("路径不能为空", nameof(path));
        
        try
        {
            // 使用CancellationToken
            cancellationToken.ThrowIfCancellationRequested();
            
            // 异步操作
            var asset = await InternalLoadAssetAsync<T>(path, cancellationToken);
            
            return new AssetHandle<T>(asset, path);
        }
        catch (OperationCanceledException)
        {
            // 取消操作的特殊处理
            WindLogger.Info($"资源加载被取消: {path}");
            throw;
        }
        catch (Exception ex)
        {
            // 错误处理和日志
            WindLogger.Error($"资源加载失败: {path}, 错误: {ex.Message}");
            throw new AssetLoadException($"无法加载资源: {path}", ex);
        }
    }
    
    // 同步包装方法（如果需要）
    public AssetHandle<T> LoadAsset<T>(string path) where T : Object
    {
        return LoadAssetAsync<T>(path).GetAwaiter().GetResult();
    }
}
```

#### 错误处理规范
```csharp
// 自定义异常类
public class WindException : Exception
{
    public string ErrorCode { get; }
    
    public WindException(string message, string errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }
    
    public WindException(string message, Exception innerException, string errorCode = null) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

// 结果包装类
public class Result<T>
{
    public bool Success { get; }
    public T Value { get; }
    public string ErrorMessage { get; }
    public string ErrorCode { get; }
    
    private Result(bool success, T value, string errorMessage, string errorCode)
    {
        Success = success;
        Value = value;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }
    
    public static Result<T> Successful(T value) => new Result<T>(true, value, null, null);
    public static Result<T> Failed(string errorMessage, string errorCode = null) => new Result<T>(false, default, errorMessage, errorCode);
}
```

### 性能规范

#### 内存管理
```csharp
// IDisposable实现模板
public class ResourceManager : IDisposable
{
    private bool _disposed = false;
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // 释放托管资源
                _managedResources?.Dispose();
            }
            
            // 释放非托管资源
            ReleaseUnmanagedResources();
            
            _disposed = true;
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    ~ResourceManager()
    {
        Dispose(false);
    }
}

// 对象池使用
public class PooledResourceManager
{
    private readonly ObjectPool<ResourceRequest> _requestPool;
    
    public PooledResourceManager()
    {
        _requestPool = new ObjectPool<ResourceRequest>(
            () => new ResourceRequest(),
            request => request.Reset(),
            request => request.IsValid()
        );
    }
    
    public async Task<T> LoadAsync<T>(string path) where T : Object
    {
        var request = _requestPool.Rent();
        try
        {
            request.Initialize(path);
            return await ProcessRequestAsync<T>(request);
        }
        finally
        {
            _requestPool.Return(request);
        }
    }
}
```

#### 性能监控
```csharp
// 性能监控装饰器
public class PerformanceMonitoredAssetManager : IAssetManager
{
    private readonly IAssetManager _inner;
    private readonly IPerformanceMonitor _monitor;
    
    public PerformanceMonitoredAssetManager(IAssetManager inner, IPerformanceMonitor monitor)
    {
        _inner = inner;
        _monitor = monitor;
    }
    
    public async Task<AssetHandle<T>> LoadAssetAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object
    {
        using var timing = _monitor.BeginTiming($"LoadAsset<{typeof(T).Name}>", new { path });
        
        try
        {
            var result = await _inner.LoadAssetAsync<T>(path, cancellationToken);
            
            // 记录成功指标
            _monitor.RecordSuccess("asset_load", new { type = typeof(T).Name, path });
            
            return result;
        }
        catch (Exception ex)
        {
            // 记录失败指标
            _monitor.RecordError("asset_load", ex, new { type = typeof(T).Name, path });
            throw;
        }
    }
}
```

---

## 🧪 测试规范

### 单元测试模板
```csharp
// 单元测试基类
public abstract class WindTestBase
{
    protected IServiceProvider ServiceProvider;
    protected IWindContainer Container;
    
    [SetUp]
    public virtual void SetUp()
    {
        Container = new WindContainer();
        ConfigureServices(Container);
        ServiceProvider = Container.BuildServiceProvider();
    }
    
    [TearDown]
    public virtual void TearDown()
    {
        ServiceProvider?.Dispose();
        Container?.Dispose();
    }
    
    protected abstract void ConfigureServices(IWindContainer container);
}

// 具体测试类示例
[TestFixture]
public class AssetManagerTests : WindTestBase
{
    private IAssetManager _assetManager;
    
    protected override void ConfigureServices(IWindContainer container)
    {
        container.RegisterSingleton<IAssetManager, AssetManager>();
        container.RegisterSingleton<ILogger, TestLogger>();
    }
    
    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        _assetManager = ServiceProvider.GetService<IAssetManager>();
    }
    
    [Test]
    public async Task LoadAssetAsync_ValidPath_ReturnsAsset()
    {
        // Arrange
        const string path = "test/valid_texture";
        
        // Act
        var result = await _assetManager.LoadAssetAsync<Texture2D>(path);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(path, result.Path);
    }
    
    [Test]
    public void LoadAssetAsync_InvalidPath_ThrowsException()
    {
        // Arrange
        const string invalidPath = "";
        
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => 
            _assetManager.LoadAssetAsync<Texture2D>(invalidPath));
    }
    
    [Test]
    public async Task LoadAssetAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        const string path = "test/large_asset";
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(() =>
            _assetManager.LoadAssetAsync<Texture2D>(path, cts.Token));
    }
}
```

### 性能测试模板
```csharp
[TestFixture]
public class AssetManagerPerformanceTests
{
    private IAssetManager _assetManager;
    
    [SetUp]
    public void SetUp()
    {
        _assetManager = new AssetManager();
        _assetManager.Initialize();
    }
    
    [Test]
    [Performance]
    public async Task LoadAssetAsync_Performance_MeetsRequirements()
    {
        const string path = "test/performance_texture";
        const int iterations = 100;
        const double maxAverageTime = 50.0; // 最大平均加载时间50ms
        
        var times = new List<double>();
        
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var asset = await _assetManager.LoadAssetAsync<Texture2D>(path);
            
            stopwatch.Stop();
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
            
            // 释放资源
            _assetManager.ReleaseAsset(asset);
        }
        
        var averageTime = times.Average();
        var p95Time = times.OrderBy(t => t).Skip((int)(iterations * 0.95)).First();
        
        Assert.Less(averageTime, maxAverageTime, 
            $"平均加载时间 {averageTime:F2}ms 超过要求的 {maxAverageTime}ms");
        
        Assert.Less(p95Time, maxAverageTime * 2, 
            $"P95加载时间 {p95Time:F2}ms 超过要求的 {maxAverageTime * 2}ms");
    }
    
    [Test]
    public async Task LoadAssetAsync_MemoryUsage_NoLeaks()
    {
        const string path = "test/memory_texture";
        const int iterations = 50;
        
        // 预热
        for (int i = 0; i < 5; i++)
        {
            var warmup = await _assetManager.LoadAssetAsync<Texture2D>(path);
            _assetManager.ReleaseAsset(warmup);
        }
        
        // 强制垃圾回收
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);
        
        // 测试内存使用
        for (int i = 0; i < iterations; i++)
        {
            var asset = await _assetManager.LoadAssetAsync<Texture2D>(path);
            _assetManager.ReleaseAsset(asset);
        }
        
        // 强制垃圾回收
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;
        
        Assert.Less(memoryIncrease, 1024 * 1024, // 内存增长不超过1MB
            $"内存泄漏检测失败，内存增长: {memoryIncrease / 1024.0:F2} KB");
    }
}
```

---

## 📚 文档规范

### README.md模板
```markdown
# Wind [包名]

[简短的包描述和主要用途]

## ✨ 功能特性

- 🚀 [主要功能1]
- ⚡ [主要功能2]  
- 🛡️ [主要功能3]
- 📊 [主要功能4]

## 🔧 安装

### 通过Wind Package Manager安装（推荐）
1. 打开Unity编辑器
2. 选择 `Wind > Package Manager`
3. 搜索 `com.wind.[category].[name]`
4. 点击安装

### 通过Unity Package Manager安装
1. 打开 `Window > Package Manager`
2. 点击 `+` 按钮
3. 选择 `Add package from git URL`
4. 输入：`https://github.com/wind-org/com.wind.[category].[name].git`

### 手动安装
1. 下载最新版本
2. 将包文件夹放入 `Packages` 目录
3. Unity会自动检测并导入包

## 🚀 快速开始

### 基础使用
\```csharp
using Wind.[Category];

public class ExampleScript : MonoBehaviour
{
    private I[MainService] _service;
    
    private async void Start()
    {
        // 获取服务实例
        _service = WindContainer.Resolve<I[MainService]>();
        
        // 基础使用示例
        var result = await _service.MainFunctionAsync(parameters);
        
        if (result.Success)
        {
            Debug.Log("操作成功");
        }
    }
}
\```

### 高级配置
\```csharp
// 自定义配置
var config = new [ConfigType]
{
    // 配置属性
};

_service.Configure(config);
\```

## 📖 详细文档

- [用户手册](Documentation~/manual/README.md)
- [API参考](Documentation~/api/README.md)
- [示例教程](Documentation~/tutorials/README.md)

## 🔗 相关包

- [com.wind.core](https://github.com/wind-org/com.wind.core) - Wind核心框架
- [相关包链接]

## 📋 系统要求

- Unity 2022.3 或更高版本
- .NET Standard 2.1
- 支持平台：Windows, macOS, Linux, Android, iOS

## 🤝 贡献指南

我们欢迎社区贡献！请查看 [贡献指南](CONTRIBUTING.md) 了解如何参与。

## 📄 许可证

本项目基于 [MIT许可证](LICENSE) 开源。

## 📞 支持

- [GitHub Issues](https://github.com/wind-org/com.wind.[category].[name]/issues)
- [Wind开发者社区](https://community.wind.com)
- 企业支持：support@wind.com
```

### CHANGELOG.md模板
```markdown
# 变更日志

本文件记录了此包的所有重要变更。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，
并且本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [未发布]

## [1.0.0] - 2025-XX-XX

### 新增
- 初始版本发布
- [核心功能1] 实现
- [核心功能2] 实现

### 已修改
- 无

### 已移除
- 无

### 修复
- 无

### 安全性
- 无

## 版本比较链接
[未发布]: https://github.com/wind-org/com.wind.[category].[name]/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/wind-org/com.wind.[category].[name]/releases/tag/v1.0.0
```

---

## 🚀 发布流程

### 版本发布检查清单

#### 发布前检查
- [ ] 所有单元测试通过
- [ ] 性能测试达标
- [ ] 代码覆盖率达标（>85%）
- [ ] 文档完整且准确
- [ ] 示例代码可正常运行
- [ ] 兼容性测试通过
- [ ] 安全扫描通过

#### 版本号规则
- **主版本号(Major)**: 不兼容的API修改
- **次版本号(Minor)**: 向下兼容的功能性新增
- **修订号(Patch)**: 向下兼容的问题修正

#### 发布脚本
```powershell
# release.ps1
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [string]$ReleaseNotes = ""
)

Write-Host "开始发布版本 $Version"

# 1. 运行所有测试
Write-Host "运行测试..."
$testResult = dotnet test --no-build --verbosity normal
if ($LASTEXITCODE -ne 0) {
    Write-Error "测试失败，发布中止"
    exit 1
}

# 2. 更新版本号
Write-Host "更新版本号..."
$packageJson = Get-Content "package.json" | ConvertFrom-Json
$packageJson.version = $Version
$packageJson | ConvertTo-Json -Depth 10 | Set-Content "package.json"

# 3. 更新CHANGELOG
Write-Host "更新CHANGELOG..."
# 脚本逻辑更新changelog

# 4. 创建Git标签
Write-Host "创建Git标签..."
git add .
git commit -m "Release version $Version"
git tag -a "v$Version" -m "Release $Version`n`n$ReleaseNotes"

# 5. 推送到远程仓库
Write-Host "推送到远程仓库..."
git push origin main
git push origin "v$Version"

# 6. 发布到私有Registry
Write-Host "发布到私有Registry..."
npm publish --registry https://npm.wind.com

Write-Host "版本 $Version 发布完成!" -ForegroundColor Green
```

---

## 📞 支持和维护

### 问题报告模板
```markdown
**问题描述**
简要描述问题

**重现步骤**
1. 步骤1
2. 步骤2
3. 步骤3

**期望行为**
描述期望发生什么

**实际行为**
描述实际发生了什么

**环境信息**
- Unity版本: 
- 包版本: 
- 操作系统: 
- 设备信息: 

**附加信息**
其他相关信息，日志，截图等
```

### 维护计划
- **日常维护**: 监控问题报告，回复用户问题
- **版本维护**: 修复bug，发布补丁版本
- **功能维护**: 添加新功能，发布次版本
- **文档维护**: 更新文档，改进示例

---

**📝 模板使用说明**: 使用此模板创建新包时，请将所有 `[占位符]` 替换为实际内容，并根据包的具体功能调整代码示例和文档内容。
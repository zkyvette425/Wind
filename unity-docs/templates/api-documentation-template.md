# [包名] API文档模板

> **模板版本**: v1.0.0  
> **适用范围**: com.wind.* 系列包API文档  
> **创建时间**: 2025-08-30 (北京时间)  
> **使用说明**: 复制此模板创建API文档  

---

## 📋 API文档基本信息

### 包标识信息
- **包名**: com.wind.[category].[name]
- **API版本**: 1.0.0
- **最后更新**: 2025-XX-XX
- **兼容性**: Unity 2022.3+

### 命名空间层次
```csharp
Wind.[Category]                    // 主命名空间
├── Core                          // 核心接口和类
├── Models                        // 数据模型
├── Services                      // 服务实现
├── Extensions                    // 扩展方法
└── Utilities                     // 工具类
```

---

## 🏗️ 核心接口

### I[MainService] - 主服务接口
**命名空间**: `Wind.[Category].Core`  
**继承**: `IDisposable`  
**描述**: [主服务的核心功能描述]

#### 接口定义
```csharp
public interface I[MainService] : IDisposable
{
    // 属性
    bool IsInitialized { get; }
    [ConfigType] Configuration { get; }
    
    // 事件
    event Action<[EventArgs]> On[Event];
    
    // 核心方法
    Task<Result<T>> [MainMethod]Async<T>([Parameters] parameters, CancellationToken cancellationToken = default);
    
    // 配置方法
    void Configure([ConfigType] configuration);
    
    // 状态方法
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync();
}
```

#### 属性详细说明

##### IsInitialized
- **类型**: `bool`
- **访问**: 只读
- **描述**: 获取服务是否已初始化的状态
- **返回值**: 
  - `true`: 服务已成功初始化
  - `false`: 服务未初始化或初始化失败

**使用示例**:
```csharp
if (!service.IsInitialized)
{
    await service.InitializeAsync();
}
```

##### Configuration
- **类型**: `[ConfigType]`
- **访问**: 只读
- **描述**: 获取当前服务配置
- **返回值**: 当前有效的配置对象

#### 事件详细说明

##### On[Event]
- **类型**: `Action<[EventArgs]>`
- **描述**: [事件发生时机和用途描述]
- **参数**: `[EventArgs]` - 事件参数对象

**使用示例**:
```csharp
service.On[Event] += (eventArgs) =>
{
    Debug.Log($"事件发生: {eventArgs.[Property]}");
};
```

#### 方法详细说明

##### [MainMethod]Async<T>
**签名**: `Task<Result<T>> [MainMethod]Async<T>([Parameters] parameters, CancellationToken cancellationToken = default)`

**描述**: [方法的主要功能和用途]

**类型参数**:
- `T`: [类型参数说明]

**参数**:
- `parameters` ([Parameters]): [参数说明]
- `cancellationToken` (CancellationToken): 可选的取消令牌，默认为 `default`

**返回值**: 
- `Task<Result<T>>`: 异步任务，返回操作结果
  - `Result<T>.Success`: 操作成功，包含结果数据
  - `Result<T>.Failed`: 操作失败，包含错误信息

**异常**:
- `ArgumentException`: 参数无效时抛出
- `InvalidOperationException`: 服务未初始化时调用抛出
- `OperationCanceledException`: 操作被取消时抛出

**使用示例**:
```csharp
try
{
    var parameters = new [Parameters]
    {
        // 设置参数
    };
    
    var result = await service.[MainMethod]Async<TargetType>(parameters);
    
    if (result.Success)
    {
        Debug.Log($"操作成功: {result.Value}");
    }
    else
    {
        Debug.LogError($"操作失败: {result.ErrorMessage}");
    }
}
catch (Exception ex)
{
    Debug.LogError($"异常: {ex.Message}");
}
```

---

## 📦 数据模型

### [MainModel] - 主数据模型
**命名空间**: `Wind.[Category].Models`  
**描述**: [数据模型的用途和包含的信息]

#### 类定义
```csharp
[Serializable]
public class [MainModel]
{
    [Header("基本信息")]
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    
    [Header("配置信息")]
    public [ConfigProperty] ConfigProperty { get; set; }
    
    [Header("状态信息")]
    public [ModelState] State { get; set; }
    
    // 构造函数
    public [MainModel]()
    {
        Id = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
        State = [ModelState].Default;
    }
    
    public [MainModel](string name) : this()
    {
        Name = name;
    }
}
```

#### 属性说明

##### Id
- **类型**: `string`
- **描述**: 唯一标识符
- **默认值**: 自动生成的GUID
- **验证**: 不能为空或空字符串

##### Name
- **类型**: `string`
- **描述**: 显示名称
- **默认值**: `null`
- **验证**: 长度不超过100个字符

##### CreatedAt
- **类型**: `DateTime`
- **描述**: 创建时间
- **默认值**: 当前UTC时间
- **格式**: ISO 8601格式

---

## ⚙️ 配置类

### [ConfigType] - 配置类
**命名空间**: `Wind.[Category].Models`  
**描述**: [配置类的用途和配置项说明]

#### 类定义
```csharp
[CreateAssetMenu(fileName = "[ConfigName]", menuName = "Wind/[Category]/[ConfigName]")]
[Serializable]
public class [ConfigType] : ScriptableObject
{
    [Header("基础配置")]
    [SerializeField] private bool _enableFeature = true;
    [SerializeField] private int _maxItems = 100;
    [SerializeField] private float _timeout = 30.0f;
    
    [Header("高级配置")]
    [SerializeField] private [ConfigEnum] _mode = [ConfigEnum].Auto;
    [SerializeField] private string[] _allowedValues = new string[0];
    
    // 属性访问器
    public bool EnableFeature => _enableFeature;
    public int MaxItems => _maxItems;
    public float Timeout => _timeout;
    public [ConfigEnum] Mode => _mode;
    public IReadOnlyList<string> AllowedValues => _allowedValues;
    
    // 验证方法
    public ValidationResult Validate()
    {
        var errors = new List<string>();
        
        if (MaxItems <= 0)
            errors.Add("MaxItems must be greater than 0");
            
        if (Timeout <= 0)
            errors.Add("Timeout must be greater than 0");
            
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
    
    // 默认配置
    public static [ConfigType] CreateDefault()
    {
        var config = CreateInstance<[ConfigType]>();
        config._enableFeature = true;
        config._maxItems = 100;
        config._timeout = 30.0f;
        config._mode = [ConfigEnum].Auto;
        return config;
    }
}
```

---

## 🔧 扩展方法

### [Category]Extensions - 扩展方法类
**命名空间**: `Wind.[Category].Extensions`  
**描述**: [扩展方法的用途和适用对象]

#### 扩展方法定义
```csharp
public static class [Category]Extensions
{
    // GameObject扩展
    public static T GetOrAdd[Component]<T>(this GameObject gameObject) where T : Component
    {
        var component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }
        return component;
    }
    
    // Transform扩展
    public static void Set[Property](this Transform transform, [PropertyType] value)
    {
        // 实现逻辑
    }
    
    // 泛型扩展
    public static bool TryGet[Value]<T>(this T source, out [ValueType] value) where T : I[Interface]
    {
        // 实现逻辑
        value = default;
        return false;
    }
}
```

#### 方法详细说明

##### GetOrAdd[Component]<T>
**签名**: `public static T GetOrAdd[Component]<T>(this GameObject gameObject) where T : Component`

**描述**: 获取GameObject上的组件，如果不存在则添加

**类型约束**: `T` 必须继承自 `Component`

**参数**:
- `gameObject` (GameObject): 目标游戏对象

**返回值**: 
- `T`: 组件实例（获取到的或新添加的）

**使用示例**:
```csharp
// 获取或添加Rigidbody组件
var rigidbody = gameObject.GetOrAdd[Component]<Rigidbody>();
```

---

## 🚀 服务实现

### [MainService] - 主服务实现
**命名空间**: `Wind.[Category].Services`  
**实现接口**: `I[MainService]`  
**描述**: [服务实现的特点和使用场景]

#### 类定义概述
```csharp
public class [MainService] : I[MainService]
{
    // 私有字段
    private readonly ILogger _logger;
    private readonly [ConfigType] _config;
    private bool _isInitialized;
    private bool _disposed;
    
    // 公共属性实现
    public bool IsInitialized => _isInitialized;
    public [ConfigType] Configuration => _config;
    
    // 事件实现
    public event Action<[EventArgs]> On[Event];
    
    // 构造函数
    public [MainService](ILogger logger, [ConfigType] config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }
    
    // 接口方法实现
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 实现初始化逻辑
    }
    
    // IDisposable实现
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
```

---

## 📊 枚举定义

### [MainEnum] - 主枚举
**命名空间**: `Wind.[Category].Models`  
**描述**: [枚举的用途和取值含义]

#### 枚举定义
```csharp
public enum [MainEnum]
{
    /// <summary>
    /// [选项1描述]
    /// </summary>
    Option1 = 0,
    
    /// <summary>
    /// [选项2描述]  
    /// </summary>
    Option2 = 1,
    
    /// <summary>
    /// [选项3描述]
    /// </summary>
    Option3 = 2
}
```

#### 枚举值说明

##### Option1 (0)
- **用途**: [使用场景说明]
- **行为**: [选择此选项时的行为]
- **适用场景**: [推荐的使用场景]

##### Option2 (1)
- **用途**: [使用场景说明]
- **行为**: [选择此选项时的行为]
- **适用场景**: [推荐的使用场景]

---

## 🛠️ 工具类

### [Utility]Helper - 工具类
**命名空间**: `Wind.[Category].Utilities`  
**描述**: [工具类的功能和使用场景]

#### 静态方法
```csharp
public static class [Utility]Helper
{
    // 转换方法
    public static [OutputType] Convert[Input]To[Output]([InputType] input)
    {
        // 实现转换逻辑
    }
    
    // 验证方法
    public static bool IsValid[Object]([ObjectType] obj)
    {
        // 实现验证逻辑
    }
    
    // 创建方法
    public static [ObjectType] Create[Object]([Parameters] parameters)
    {
        // 实现创建逻辑
    }
}
```

---

## 🧪 示例代码

### 基础使用示例
```csharp
using Wind.[Category];
using Wind.[Category].Models;

public class [Category]Example : MonoBehaviour
{
    private I[MainService] _service;
    
    private async void Start()
    {
        // 创建配置
        var config = [ConfigType].CreateDefault();
        
        // 获取服务实例
        _service = WindContainer.Resolve<I[MainService]>();
        
        // 配置服务
        _service.Configure(config);
        
        // 初始化服务
        if (await _service.InitializeAsync())
        {
            Debug.Log("服务初始化成功");
            
            // 订阅事件
            _service.On[Event] += HandleEvent;
            
            // 使用服务
            await UseService();
        }
    }
    
    private async Task UseService()
    {
        try
        {
            var parameters = new [Parameters]();
            var result = await _service.[MainMethod]Async<string>(parameters);
            
            if (result.Success)
            {
                Debug.Log($"操作成功: {result.Value}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"操作异常: {ex.Message}");
        }
    }
    
    private void HandleEvent([EventArgs] eventArgs)
    {
        Debug.Log($"事件触发: {eventArgs}");
    }
    
    private void OnDestroy()
    {
        // 清理资源
        if (_service != null)
        {
            _service.On[Event] -= HandleEvent;
            _service.Dispose();
        }
    }
}
```

### 高级使用示例
```csharp
using Wind.[Category];
using Wind.[Category].Extensions;

public class Advanced[Category]Example : MonoBehaviour
{
    [SerializeField] private [ConfigType] customConfig;
    
    private async void Start()
    {
        // 使用自定义配置
        var service = new [MainService](WindLogger.Instance, customConfig);
        
        // 使用扩展方法
        var component = gameObject.GetOrAdd[Component]<[ComponentType]>();
        
        // 高级功能使用
        await AdvancedUsage(service);
    }
    
    private async Task AdvancedUsage(I[MainService] service)
    {
        // 批量操作示例
        var tasks = new List<Task<Result<string>>>();
        
        for (int i = 0; i < 10; i++)
        {
            var parameters = new [Parameters] { /* 参数设置 */ };
            tasks.Add(service.[MainMethod]Async<string>(parameters));
        }
        
        var results = await Task.WhenAll(tasks);
        
        foreach (var result in results)
        {
            if (result.Success)
            {
                // 处理成功结果
                ProcessResult(result.Value);
            }
            else
            {
                // 处理失败结果
                HandleError(result.ErrorMessage);
            }
        }
    }
}
```

---

## 📋 版本兼容性

### API变更历史

#### v1.0.0 (2025-XX-XX)
- 初始API发布
- 包含所有核心接口和实现

#### v1.1.0 (计划中)
- 新增[新功能]相关API
- 废弃[旧方法]，推荐使用[新方法]
- 向下兼容v1.0.0

### 废弃API
```csharp
[Obsolete("请使用NewMethod替代", false)]
public void OldMethod()
{
    // 废弃的实现
}

// 推荐的新方法
public async Task NewMethodAsync()
{
    // 新的实现
}
```

---

## ⚠️ 注意事项

### 性能注意事项
1. **异步调用**: 所有异步方法都应该使用 `await` 关键字
2. **资源释放**: 实现 `IDisposable` 的对象必须正确释放
3. **取消令牌**: 长时间运行的操作应支持取消令牌

### 线程安全
- **标记说明**: 文档中会明确标记每个API的线程安全性
- **同步方法**: 除非特别说明，否则假定API不是线程安全的
- **异步方法**: 异步方法通常是线程安全的，但需要检查具体实现

### 平台差异
- **移动平台**: 某些功能在移动平台上可能有限制
- **WebGL平台**: WebGL平台不支持多线程操作
- **编辑器差异**: 编辑器中的行为可能与构建后不同

---

**📝 文档维护**: 本API文档与代码同步维护，确保准确性。如发现文档与实际API不符，请提交Issue报告。

**🔗 相关文档**: 
- [包概述文档](README.md) - 包的基本信息和使用指南
- [示例教程](EXAMPLES.md) - 详细的使用示例和教程
- [故障排除](TROUBLESHOOTING.md) - 常见问题和解决方案
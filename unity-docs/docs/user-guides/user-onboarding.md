# Wind Unity客户端用户入手流程

> **文档版本**: v1.0.0  
> **创建时间**: 2025-08-29 (北京时间)  
> **适用人群**: Unity游戏开发者、技术决策者、架构师  
> **预计用时**: 5分钟快速体验，2小时完整掌握  

---

## 📋 版本变更历史

| 版本 | 日期 | 变更内容 | 影响范围 |
|------|------|----------|----------|
| v1.0.0 | 2025-08-29 | 创建用户入手流程完整指南 | 新增功能 |

---

## 🎯 用户入手场景

Wind Unity客户端支持三种不同的入手场景，满足不同开发者的需求：

### 场景对比
| 场景 | 目标用户 | 网络要求 | 权限要求 | 体验深度 |
|------|----------|----------|----------|----------|
| 🆓 免费体验 | 评估用户 | 可选 | 无需鉴权 | 基础功能 |
| 🔐 企业授权 | 正式用户 | 必需 | GitHub PAT | 完整功能 |
| 📱 单机开发 | 单机游戏开发者 | 无需 | 无需鉴权 | 离线完整 |

---

## 🆓 场景1: 免费体验流程

### Step 1: 发现和克隆主仓库
```bash
# 在GitHub发现Wind项目
https://github.com/wind-org/Wind

# 克隆公开仓库
git clone https://github.com/wind-org/Wind.git
cd Wind
```

### Step 2: 查看仓库结构
```
Wind/                          # 主仓库(公开)
├── 📁 server/                 # 服务端代码
├── 📁 samples/               # Unity示例项目
│   ├── HelloWind/            # 5分钟快速体验
│   ├── OfflineRPG/           # 单机RPG演示
│   └── MultiplayerDemo/      # 多人游戏演示
├── 📁 docs/                  # 公开文档
├── 🚀 QuickStart.md          # 5分钟快速开始
└── README.md                 # 项目介绍
```

### Step 3: 启动服务端环境(可选)
```bash
# 如需测试网络功能，启动本地服务器
cd server
docker-compose up -d

# 启动Wind游戏服务器
dotnet run --project Wind.Server

# ✅ 预期结果: 服务器运行在 http://localhost:5271
```

### Step 4: 打开Unity示例项目
```bash
# 使用Unity 2022.3 LTS或更高版本
# 打开项目路径: Wind/samples/HelloWind/
```

### Step 5: 体验基础功能
```csharp
// HelloWind项目中的预置脚本
public class HelloWindDemo : MonoBehaviour
{
    async void Start()
    {
        // Wind框架自动初始化
        await WindFramework.InitializeAsync();
        
        WindLogger.Info("Wind Framework 初始化完成!");
        
        // 离线模式演示
        DemoOfflineFeatures();
        
        // 如果服务器可用，演示网络功能
        if (await IsServerAvailable())
        {
            await DemoNetworkFeatures();
        }
    }
    
    void DemoOfflineFeatures()
    {
        // DI容器演示
        var playerService = WindContainer.Resolve<IPlayerService>();
        var player = playerService.CreatePlayer("TestPlayer");
        
        WindLogger.Info($"创建玩家: {player.Name}");
        
        // 资源管理演示
        var texture = WindAssets.Load<Texture2D>("demo_texture");
        WindLogger.Info($"加载资源: {texture.name}");
    }
    
    async Task DemoNetworkFeatures()
    {
        try
        {
            var client = WindClient.CreateDefault();
            await client.ConnectAsync("localhost:5271");
            
            var response = await client.SayHelloAsync("Unity");
            WindLogger.Info($"服务器回复: {response}");
        }
        catch (Exception ex)
        {
            WindLogger.Warning($"网络功能需要启动服务器: {ex.Message}");
        }
    }
}
```

### 免费体验功能清单
✅ **可用功能:**
- 基础DI容器和服务定位
- 离线资源管理和加载
- 本地存储和配置管理  
- UI框架基础功能
- 单机游戏开发能力

❌ **限制功能:**
- 网络通信功能(仅演示)
- 热更新系统(仅演示)
- 高级游戏模块(RTS/MOBA等)
- 企业级监控和分析

---

## 🔐 场景2: 企业授权使用流程

### Step 1: 获取企业权限
```bash
# 企业授权包含:
# 1. GitHub Organization邀请
# 2. Personal Access Token (PAT)
# 3. 私有Registry访问权限  
# 4. 技术支持服务
```

### Step 2: 配置Unity私有Registry
```toml
# 创建文件: ~/.upmconfig.toml (用户配置)
[npmAuth."https://npm.wind.com"]
token = "ghp_你的企业PAT令牌"
email = "your.email@company.com"
alwaysAuth = true
```

### Step 3: 配置Unity项目
```json
// Packages/manifest.json
{
  "dependencies": {
    "com.wind.core": "1.0.0"
  },
  "scopedRegistries": [
    {
      "name": "Wind Enterprise Registry",
      "url": "https://npm.wind.com",
      "scopes": ["com.wind"]
    }
  ]
}
```

### Step 4: 使用Wind Package Manager
```csharp
// Unity菜单: Wind -> Package Manager
[MenuItem("Wind/Package Manager")]
public static void OpenWindPackageManager()
{
    WindPackageManagerWindow.Open();
}
```

### Wind Package Manager界面
```
┌─────────────────────────────────────────┐
│ Wind Enterprise Package Manager        │
├─────────────────────────────────────────┤
│ Authentication: ✅ Connected            │
│ Registry: https://npm.wind.com          │
├─────────────────────────────────────────┤
│ Available Packages:                     │
│ ✅ com.wind.core          v1.0.0        │
│ ⬜ com.wind.rts           v1.0.0        │
│ ⬜ com.wind.moba          v1.0.0        │
│ ⬜ com.wind.monitoring    v1.0.0        │
├─────────────────────────────────────────┤
│ [Install Selected] [Update All] [Docs] │
└─────────────────────────────────────────┘
```

### Step 5: 企业级功能使用
```csharp
public class EnterpriseGameClient : MonoBehaviour
{
    async void Start()
    {
        // 企业级配置
        var config = WindConfig.CreateEnterprise()
            .EnableFullNetworking()      // 完整网络功能
            .EnableHotUpdate()           // 生产级热更新
            .EnableMonitoring()          // 性能监控
            .EnableAdvancedProfiling();  // 深度性能分析
            
        await WindFramework.InitializeAsync(config);
        
        // 连接生产服务器
        var client = WindClient.CreateEnterprise();
        await client.ConnectAsync("your-game-server.com");
        
        // 启用实时监控
        WindMonitor.StartReporting("your-analytics-endpoint");
        
        StartGame();
    }
}
```

---

## 📱 场景3: 单机游戏开发流程

### Step 1: 获取离线开发包
```bash
# 方式1: 从主仓库获取
git clone https://github.com/wind-org/Wind.git
cd Wind/samples/OfflineRPG/

# 方式2: 直接下载离线包
# https://releases.wind.com/offline/wind-offline-v1.0.0.zip
```

### Step 2: 离线项目结构
```
OfflineRPG/                    # 单机RPG示例
├── Assets/
│   ├── Scripts/
│   │   ├── GameManager.cs     # 游戏主逻辑
│   │   ├── Player/            # 玩家系统
│   │   ├── Inventory/         # 背包系统
│   │   └── Save/              # 存档系统
├── Packages/
│   ├── com.wind.core.offline/ # 离线核心包
│   ├── com.wind.ui.offline/   # 离线UI系统
│   ├── com.wind.save/         # 存档系统
│   └── manifest.json
└── ProjectSettings/
```

### Step 3: 离线模式配置
```csharp
[CreateAssetMenu(fileName = "OfflineConfig", menuName = "Wind/Offline Config")]
public class WindOfflineConfig : WindConfig
{
    public override bool RequiresNetwork => false;
    public override bool RequiresServer => false;
    public override StorageMode Storage => StorageMode.LocalOnly;
    public override bool EnableTelemetry => false;
    
    [Header("单机游戏设置")]
    public bool enableAutoSave = true;
    public float autoSaveInterval = 60f; // 秒
    public int maxSaveSlots = 10;
}
```

### Step 4: 单机游戏开发示例
```csharp
public class OfflineRPGManager : MonoBehaviour
{
    [SerializeField] private WindOfflineConfig offlineConfig;
    
    async void Start()
    {
        // 初始化离线模式
        await WindFramework.InitializeAsync(offlineConfig);
        
        // 加载游戏系统
        InitializeGameSystems();
        
        // 加载存档
        await LoadGameSave();
        
        StartGame();
    }
    
    void InitializeGameSystems()
    {
        // 玩家系统
        WindContainer.RegisterSingleton<IPlayerSystem, PlayerSystem>();
        
        // 背包系统
        WindContainer.RegisterSingleton<IInventorySystem, InventorySystem>();
        
        // 任务系统
        WindContainer.RegisterSingleton<IQuestSystem, QuestSystem>();
        
        // 存档系统
        WindContainer.RegisterSingleton<ISaveSystem, LocalSaveSystem>();
    }
    
    async Task LoadGameSave()
    {
        var saveSystem = WindContainer.Resolve<ISaveSystem>();
        
        if (await saveSystem.HasSaveAsync())
        {
            var gameData = await saveSystem.LoadAsync<GameData>();
            ApplyGameData(gameData);
            WindLogger.Info("游戏存档加载完成");
        }
        else
        {
            CreateNewGame();
            WindLogger.Info("开始新游戏");
        }
    }
}
```

### 单机功能特性
✅ **完整可用:**
- 完全离线运行，无网络依赖
- 本地存档系统，多存档槽支持
- 完整的UI和游戏系统
- 资源管理和加载优化
- 单机AI和游戏逻辑

⚡ **性能优化:**
- 启动时间: <2秒
- 存档加载: <1秒  
- 资源加载: <500ms
- 内存使用: <100MB

---

## 🎯 进阶开发指南

### 自定义包开发
```bash
# 1. 创建包结构
mkdir com.yourcompany.custom
cd com.yourcompany.custom

# 2. 初始化包配置
wind-cli create-package --name "com.yourcompany.custom" --type "game-module"

# 3. 添加依赖
wind-cli add-dependency "com.wind.core@1.0.0"
```

### 多人游戏升级
```csharp
// 从单机升级到多人游戏
public class MultiplayerUpgrade : MonoBehaviour
{
    async void UpgradeToMultiplayer()
    {
        // 检测网络包可用性
        if (WindCapabilityDetector.HasNetworkCapability())
        {
            // 启用网络功能
            await WindFramework.EnableModuleAsync<NetworkModule>();
            
            // 连接多人服务器
            var client = WindContainer.Resolve<IWindClient>();
            await client.ConnectAsync("your-server.com");
            
            WindLogger.Info("成功升级到多人游戏模式");
        }
        else
        {
            WindLogger.Warning("需要安装网络包才能启用多人功能");
        }
    }
}
```

### 热更新集成
```csharp
public class HotUpdateIntegration : MonoBehaviour
{
    async void EnableHotUpdate()
    {
        // 检查热更新支持
        if (WindCapabilityDetector.HasHotUpdateCapability())
        {
            // 检查更新
            var updateInfo = await WindHotUpdate.CheckUpdatesAsync();
            
            if (updateInfo.HasUpdates)
            {
                // 下载并应用更新
                await WindHotUpdate.DownloadAndApplyAsync(updateInfo);
                
                WindLogger.Info($"成功应用热更新: {updateInfo.Version}");
            }
        }
    }
}
```

## 📊 用户统计和反馈

Wind框架会收集匿名使用统计，帮助改进产品质量：

### 收集的数据
- 包安装和使用情况
- 性能指标和错误日志
- 功能使用频率统计
- Unity版本和平台信息

### 隐私保护
- 所有数据匿名化处理
- 不收集个人信息或游戏内容
- 可通过配置完全禁用统计
- 符合GDPR和相关隐私法规

### 禁用统计收集
```csharp
// 在初始化时禁用统计
var config = WindConfig.Create()
    .DisableTelemetry()
    .DisableAnalytics();
    
await WindFramework.InitializeAsync(config);
```

## 🆘 故障排除

### 常见问题1: 包安装失败
**错误信息**: "Failed to resolve package dependencies"

**解决方案**:
1. 检查网络连接和Registry配置
2. 验证PAT权限和有效性
3. 清理Unity包缓存: `Wind -> Clear Package Cache`

### 常见问题2: 权限认证失败  
**错误信息**: "Authentication failed for private registry"

**解决方案**:
1. 检查.upmconfig.toml文件配置
2. 验证PAT权限范围包含packages:read
3. 联系技术支持获取新的访问令牌

### 常见问题3: 功能模块未启用
**错误信息**: "Module not available in current configuration"

**解决方案**:
1. 检查包依赖是否正确安装
2. 验证WindConfig中的功能开关
3. 查看Console日志中的模块加载信息

## 📞 获取帮助

遇到问题？按优先级排序：

1. **查看文档**: [Wind Unity文档中心](../README.md)
2. **检查示例**: 参考samples/目录中的示例项目  
3. **社区支持**: [GitHub Issues](https://github.com/wind-org/Wind/issues)
4. **企业支持**: support@wind.com (仅企业用户)
5. **技术论坛**: [Wind开发者社区](https://community.wind.com)

---

## 🔗 相关文档

- [Unity客户端纲领](../plans/project-management/governance/unity-纲领.md) - 完整架构和技术决策
- [包架构设计](../architecture/packages-architecture.md) - 详细包设计和依赖关系
- [技术分析报告](../plans/technical-research/current/technical-analysis.md) - 深度技术分析
- [服务端对接指南](./server-integration.md) - 与Wind服务端集成

---

*Wind Unity客户端提供了从5分钟快速体验到企业级生产部署的完整用户旅程，无论您是评估用户、企业开发者还是单机游戏制作人，都能找到适合的入手方式。*
# Orleans + MagicOnion 开发规范

**版本**: v1.0  
**日期**: 2025-08-12 (北京时间)  
**适用范围**: Wind游戏服务器框架

---

## 🎯 规范目标

本规范旨在建立统一的Orleans + MagicOnion开发标准，确保代码质量、可维护性和团队协作效率。

## 🏗️ 架构规范

### 项目结构标准

```
Wind/
├── Wind.Server/              # Orleans Silo宿主 + MagicOnion服务
├── Wind.GrainInterfaces/     # Orleans Grain接口定义
├── Wind.Grains/             # Orleans Grain实现 + MagicOnion服务实现
├── Wind.Shared/             # 共享协议和接口
├── Wind.Client/             # 客户端SDK
├── Wind.Tests/              # 测试项目
└── docs/                    # 技术文档
```

### 模块职责划分

- **Wind.Server**: 仅作为宿主，不包含业务逻辑
- **Wind.GrainInterfaces**: 只定义接口，不包含实现
- **Wind.Grains**: 包含所有业务逻辑实现
- **Wind.Shared**: 消息协议、枚举、常量等共享定义
- **Wind.Client**: 对外提供的客户端SDK

## 📝 Orleans Grain开发规范

### 1. 命名约定

#### Grain接口命名
```csharp
// ✅ 正确: I{业务名}Grain
public interface IPlayerGrain : IGrainWithStringKey
public interface IRoomGrain : IGrainWithGuidKey
public interface IChatGrain : IGrainWithStringKey

// ❌ 错误: 缺少Grain后缀或I前缀
public interface PlayerService
public interface RoomManager
```

#### Grain实现命名
```csharp
// ✅ 正确: {业务名}Grain
public class PlayerGrain : Grain, IPlayerGrain
public class RoomGrain : Grain, IRoomGrain

// ❌ 错误: 不一致的命名
public class PlayerService : Grain, IPlayerGrain
public class RoomImpl : Grain, IRoomGrain
```

#### 方法命名
```csharp
// ✅ 正确: 所有方法必须异步，使用Async后缀
Task<PlayerInfo> GetPlayerInfoAsync();
Task UpdatePositionAsync(Vector3 position);
ValueTask<bool> IsOnlineAsync();

// ❌ 错误: 同步方法或缺少Async后缀
PlayerInfo GetPlayerInfo();  // 同步方法不允许
Task<PlayerInfo> GetPlayer(); // 缺少Async后缀
```

### 2. Grain键类型选择指南

```csharp
// 🔥 性能优先: 使用Guid作为键
public interface IPlayerGrain : IGrainWithGuidKey  // 推荐
public interface IRoomGrain : IGrainWithGuidKey    // 推荐

// 🔍 可读性优先: 使用String作为键
public interface IChatGrain : IGrainWithStringKey  // 聊天频道名
public interface ILeaderboardGrain : IGrainWithStringKey  // 排行榜类型

// 📊 复合键: 使用复合键
public interface IGameSessionGrain : IGrainWithGuidCompoundKey  // Guid + 额外Long
```

### 3. 状态管理规范

#### 持久化状态定义
```csharp
// ✅ 正确: 使用record定义状态类
[Serializable]
public record PlayerState
{
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; } = 1;
    public Vector3 Position { get; init; } = Vector3.Zero;
    public DateTime LastLoginTime { get; init; } = DateTime.UtcNow;
}

// ✅ 正确: Grain中声明持久化状态
public class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IPersistentState<PlayerState> _state;

    public PlayerGrain(
        [PersistentState("player", "Default")] IPersistentState<PlayerState> state,
        ILogger<PlayerGrain> logger)
    {
        _state = state;
    }
}
```

#### 状态修改规范
```csharp
// ✅ 正确: 使用with表达式更新状态
public async Task UpdateLevelAsync(int newLevel)
{
    _state.State = _state.State with { Level = newLevel };
    await _state.WriteStateAsync();
    
    _logger.LogInformation("玩家 {PlayerId} 等级更新为 {Level}", 
        this.GetPrimaryKeyString(), newLevel);
}

// ❌ 错误: 直接修改状态属性 (record是不可变的)
public async Task UpdateLevelBad(int newLevel)
{
    _state.State.Level = newLevel;  // 编译错误
}
```

### 4. 异常处理规范

```csharp
// ✅ 正确: 使用领域特定异常
public class PlayerNotFoundException : Exception
{
    public PlayerNotFoundException(string playerId) 
        : base($"找不到玩家: {playerId}") { }
}

// ✅ 正确: Grain方法中的异常处理
public async Task<PlayerInfo> GetPlayerInfoAsync()
{
    try
    {
        if (string.IsNullOrEmpty(_state.State.Name))
        {
            throw new PlayerNotFoundException(this.GetPrimaryKeyString());
        }

        return new PlayerInfo(_state.State.Name, _state.State.Level);
    }
    catch (Exception ex) when (!(ex is PlayerNotFoundException))
    {
        _logger.LogError(ex, "获取玩家信息失败: {PlayerId}", 
            this.GetPrimaryKeyString());
        throw;
    }
}
```

## 🌐 MagicOnion服务开发规范

### 1. 服务类型选择指南

```csharp
// 🔥 Unary Service: 用于请求-响应模式
public interface IPlayerService : IService<IPlayerService>
{
    UnaryResult<PlayerInfo> GetPlayerInfoAsync(string playerId);
}

// 🌊 StreamingHub: 用于实时双向通信
public interface IChatHub : IStreamingHub<IChatHub, IChatHubReceiver>
{
    ValueTask SendMessageAsync(string message);
}

// 📥 StreamingHub接收器: 定义客户端接收的消息
public interface IChatHubReceiver
{
    void OnMessage(string message);
}
```

### 2. 服务接口定义

```csharp
// ✅ 正确: 服务接口定义 (放在Wind.Shared项目)
namespace Wind.Shared.Services;

/// <summary>
/// 玩家管理RPC服务
/// </summary>
public interface IPlayerService : IService<IPlayerService>
{
    /// <summary>
    /// 玩家登录
    /// </summary>
    /// <param name="loginRequest">登录请求</param>
    /// <returns>登录结果</returns>
    UnaryResult<LoginResponse> LoginAsync(LoginRequest loginRequest);

    /// <summary>
    /// 获取玩家信息
    /// </summary>
    /// <param name="playerId">玩家ID</param>
    /// <returns>玩家信息</returns>
    UnaryResult<PlayerInfo> GetPlayerInfoAsync(string playerId);
}
```

### 2. 服务实现规范

```csharp
// ✅ 正确: 服务实现 (放在Wind.Grains项目)
namespace Wind.Grains.Services;

public class PlayerService : ServiceBase<IPlayerService>, IPlayerService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PlayerService> _logger;

    public PlayerService(IGrainFactory grainFactory, ILogger<PlayerService> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async UnaryResult<LoginResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            // 参数验证
            if (string.IsNullOrEmpty(request.PlayerId))
                return new LoginResponse { Success = false, Message = "玩家ID不能为空" };

            // 调用Orleans Grain
            var playerGrain = _grainFactory.GetGrain<IPlayerGrain>(request.PlayerId);
            var playerInfo = await playerGrain.LoginAsync(request);

            _logger.LogInformation("玩家登录成功: {PlayerId}", request.PlayerId);
            
            return new LoginResponse 
            { 
                Success = true, 
                PlayerInfo = playerInfo 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "玩家登录失败: {PlayerId}", request.PlayerId);
            return new LoginResponse { Success = false, Message = "登录失败" };
        }
    }
}
```

### 3. StreamingHub开发规范 🌊

#### Hub接口定义
```csharp
// ✅ 正确: StreamingHub接口定义 (放在Wind.Shared项目)
namespace Wind.Shared.Services;

/// <summary>
/// 聊天StreamingHub - 提供实时聊天功能
/// </summary>
public interface IChatHub : IStreamingHub<IChatHub, IChatHubReceiver>
{
    /// <summary>
    /// 连接到聊天服务
    /// </summary>
    ValueTask ConnectAsync(string playerId, string accessToken);
    
    /// <summary>
    /// 加入房间聊天
    /// </summary>
    ValueTask JoinRoomChatAsync(string roomId, string playerId);
    
    /// <summary>
    /// 发送房间聊天消息
    /// </summary>
    ValueTask SendRoomChatAsync(string roomId, string playerId, string message);
    
    /// <summary>
    /// 离开房间聊天
    /// </summary>
    ValueTask LeaveRoomChatAsync(string roomId, string playerId);
}

/// <summary>
/// 聊天Hub接收器 - 定义客户端接收的消息
/// </summary>
public interface IChatHubReceiver
{
    /// <summary>
    /// 接收聊天连接成功通知
    /// </summary>
    void OnChatConnected(string playerId, long timestamp);
    
    /// <summary>
    /// 接收房间聊天消息
    /// </summary>
    void OnRoomChatMessage(string messageId, string roomId, string senderId, 
        string senderName, string message, string messageType, long timestamp);
    
    /// <summary>
    /// 接收聊天错误通知
    /// </summary>
    void OnChatError(string errorCode, string errorMessage);
}
```

#### Hub实现规范
```csharp
// ✅ 正确: StreamingHub实现 (放在Wind.Server项目)
namespace Wind.Server.Services;

public class ChatHub : StreamingHubBase<IChatHub, IChatHubReceiver>, IChatHub
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ChatHub> _logger;
    
    // 🔑 关键：保存Group引用以便广播
    private readonly ConcurrentDictionary<string, IGroup<IChatHubReceiver>> _roomGroups = new();

    public ChatHub(IGrainFactory grainFactory, ILogger<ChatHub> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async ValueTask JoinRoomChatAsync(string roomId, string playerId)
    {
        var roomKey = $"room_{roomId}";
        
        // ✅ 正确：加入群组并保存引用
        var roomGroup = await Group.AddAsync(roomKey);
        _roomGroups.AddOrUpdate(roomKey, roomGroup, (key, oldGroup) => roomGroup);
        
        // 通知房间内所有玩家
        roomGroup.All.OnRoomChatStatusUpdate(roomId, onlineCount, true);
    }

    public async ValueTask SendRoomChatAsync(string roomId, string playerId, string message)
    {
        var roomKey = $"room_{roomId}";
        
        // ✅ 正确：使用保存的群组引用广播
        if (_roomGroups.TryGetValue(roomKey, out var roomGroup))
        {
            roomGroup.All.OnRoomChatMessage(messageId, roomId, playerId, 
                senderName, message, "Text", timestamp);
        }
    }

    public async ValueTask LeaveRoomChatAsync(string roomId, string playerId)
    {
        var roomKey = $"room_{roomId}";
        
        // ✅ 正确：从群组中移除
        if (_roomGroups.TryGetValue(roomKey, out var roomGroup))
        {
            await roomGroup.RemoveAsync(Context);
        }
    }

    // ✅ 正确：连接断开时清理
    protected override async ValueTask OnDisconnected()
    {
        // 清理所有群组引用和广播离开事件
        foreach (var groupPair in _roomGroups.ToList())
        {
            var roomGroup = groupPair.Value;
            var roomId = groupPair.Key.Replace("room_", "");
            
            // 广播玩家离开事件
            roomGroup.All.OnPlayerLeftRoom(roomId, playerId, playerName, "连接断开");
        }
    }
}
```

#### 🚨 StreamingHub常见错误

```csharp
// ❌ 错误：不保存Group引用，无法广播
public async ValueTask JoinRoom(string roomId)
{
    await Group.AddAsync($"room_{roomId}");  // 引用丢失！
    // 后续无法广播消息到这个群组
}

// ❌ 错误：错误的广播语法
public async ValueTask SendMessage(string message)
{
    var group = await Group.AddAsync("room");
    group.All.OnMessage(message);  // 缺少await？实际上不需要await
}

// ❌ 错误：同步方法
public void SendMessage(string message)  // 应该是async ValueTask
{
    // StreamingHub方法必须是异步的
}

// ✅ 正确：Group广播和排除语法
public async ValueTask SendMessage(string message)
{
    if (_roomGroup != null)
    {
        // 广播给所有人
        _roomGroup.All.OnMessage(message);
        
        // 广播给除自己外的所有人
        _roomGroup.Except(new[] { ConnectionId }).OnMessage(message);
    }
}
```

### 4. 消息协议规范

```csharp
// ✅ 正确: 使用MessagePack序列化的消息定义
[MessagePackObject]
public record LoginRequest
{
    [Key(0)]
    public string PlayerId { get; init; } = string.Empty;
    
    [Key(1)]
    public string Token { get; init; } = string.Empty;
    
    [Key(2)]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

[MessagePackObject]
public record LoginResponse
{
    [Key(0)]
    public bool Success { get; init; }
    
    [Key(1)]
    public string Message { get; init; } = string.Empty;
    
    [Key(2)]
    public PlayerInfo? PlayerInfo { get; init; }
}
```

## 🧪 测试规范

### 1. Orleans Grain测试

```csharp
// ✅ 正确: 使用Orleans.TestingHost进行集成测试
public class PlayerGrainTests : IClassFixture<ClusterFixture>
{
    private readonly TestCluster _cluster;

    public PlayerGrainTests(ClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task GetPlayerInfoAsync_应该返回正确的玩家信息()
    {
        // Arrange
        var playerId = Guid.NewGuid().ToString();
        var playerGrain = _cluster.GrainFactory.GetGrain<IPlayerGrain>(playerId);
        
        // Act
        await playerGrain.SetPlayerInfoAsync("测试玩家", 10);
        var result = await playerGrain.GetPlayerInfoAsync();
        
        // Assert
        Assert.Equal("测试玩家", result.Name);
        Assert.Equal(10, result.Level);
    }
}
```

### 2. MagicOnion服务测试

```csharp
// ✅ 正确: MagicOnion服务集成测试
public class PlayerServiceTests : IClassFixture<MagicOnionTestFixture>
{
    private readonly IPlayerService _playerService;

    public PlayerServiceTests(MagicOnionTestFixture fixture)
    {
        _playerService = fixture.CreateClient<IPlayerService>();
    }

    [Fact]
    public async Task LoginAsync_成功登录_应该返回成功响应()
    {
        // Arrange
        var request = new LoginRequest
        {
            PlayerId = "test-player",
            Token = "valid-token"
        };

        // Act
        var response = await _playerService.LoginAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.PlayerInfo);
    }
}
```

## 📊 性能优化规范

### 1. 避免常见性能陷阱

```csharp
// ✅ 正确: 批量操作
public async Task<List<PlayerInfo>> GetPlayersInfoAsync(List<string> playerIds)
{
    // 并行调用多个Grain
    var tasks = playerIds.Select(id => 
        _grainFactory.GetGrain<IPlayerGrain>(id).GetPlayerInfoAsync());
    
    return (await Task.WhenAll(tasks)).ToList();
}

// ❌ 错误: 串行调用
public async Task<List<PlayerInfo>> GetPlayersInfoBad(List<string> playerIds)
{
    var results = new List<PlayerInfo>();
    foreach (var id in playerIds)  // 串行执行，性能差
    {
        var grain = _grainFactory.GetGrain<IPlayerGrain>(id);
        results.Add(await grain.GetPlayerInfoAsync());
    }
    return results;
}
```

### 2. 合理使用Timer

```csharp
// ✅ 正确: 使用Orleans Timer
public class RoomGrain : Grain, IRoomGrain
{
    private IDisposable? _heartbeatTimer;

    public override Task OnActivateAsync()
    {
        // 每30秒执行一次心跳
        _heartbeatTimer = this.RegisterTimer(
            callback: HeartbeatAsync,
            state: null,
            dueTime: TimeSpan.FromSeconds(30),
            period: TimeSpan.FromSeconds(30));
            
        return base.OnActivateAsync();
    }

    private Task HeartbeatAsync(object state)
    {
        _logger.LogDebug("房间心跳: {RoomId}", this.GetPrimaryKey());
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync()
    {
        _heartbeatTimer?.Dispose();
        return base.OnDeactivateAsync();
    }
}
```

## 🔍 代码质量检查清单

### 提交前检查 ✅

- [ ] 所有Grain方法都是异步的，使用Async后缀
- [ ] 所有公共接口都有完整的XML文档注释
- [ ] 异常处理适当，日志记录清晰
- [ ] 状态修改后调用了WriteStateAsync()
- [ ] MagicOnion消息使用MessagePack序列化
- [ ] 测试覆盖了核心业务逻辑
- [ ] 性能敏感代码进行了优化
- [ ] 遵循了项目命名约定

### 代码审查重点 👀

- Orleans Grain的生命周期管理
- 状态持久化的正确性
- 异步编程最佳实践
- 错误处理和日志记录
- MagicOnion服务的参数验证
- 性能潜在问题识别

---

## 📚 相关参考

- [Microsoft Orleans 官方文档](https://docs.microsoft.com/en-us/dotnet/orleans/)
- [MagicOnion 官方文档](https://github.com/Cysharp/MagicOnion)
- [MessagePack 序列化指南](https://github.com/neuecc/MessagePack-CSharp)
- [项目纲领文档](../plans/纲领.md)

---

**本规范随项目发展持续更新，团队成员有责任遵循并完善此规范。**
# Wind.Core 项目功能划分

## 项目概述
Wind.Core 是游戏服务器的核心项目，包含了游戏的核心接口、模型和服务实现。它是整个系统的基础，被多个其他项目依赖。

## 主要功能
1. 定义核心业务接口
2. 实现核心业务逻辑
3. 提供数据模型
4. 处理网络通信协议

## 关键组件

### 接口层 (Interfaces)
- `ICollisionDetectionService.cs`: 碰撞检测服务接口
- `IMessageRouter.cs`: 消息路由接口
- `IPlayerDataService.cs`: 玩家数据服务接口
- `IProtocolParser.cs`: 协议解析接口
- `IRoomService.cs`: 房间服务接口

### 模型层 (Models)
- `GameDbContext.cs`: 数据库上下文
- `GameObject.cs`: 游戏对象基类
- `PlayerCharacter.cs`: 玩家角色模型
- `PlayerData.cs`: 玩家数据模型
- `Room.cs`: 房间模型

### 网络层 (Network)
- `JsonProtocolParser.cs`: JSON协议解析器实现

### 服务层 (Services)
- `CollisionDetectionService.cs`: 碰撞检测服务实现
- `MessageRouter.cs`: 消息路由服务实现
- `PlayerDataService.cs`: 玩家数据服务实现
- `RoomService.cs`: 房间服务实现

## 依赖关系
- 无外部项目依赖，但被 Wind.Server、Wind.Tests 等项目依赖

## 设计理念
采用接口与实现分离的设计模式，便于测试和替换不同的实现。核心业务逻辑集中在此项目，确保系统的稳定性和一致性。
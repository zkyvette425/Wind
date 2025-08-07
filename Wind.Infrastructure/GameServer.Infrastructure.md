# GameServer.Infrastructure 项目功能划分

## 项目概述
GameServer.Infrastructure 项目包含基础设施层代码，实现了数据访问、外部服务集成等功能，为领域层提供技术支持。

## 主要功能
1. 实现数据访问
2. 实现仓储接口
3. 提供基础设施服务
4. 配置依赖注入

## 关键组件

### 依赖注入配置
- `DependencyInjection.cs`: 配置基础设施层服务的依赖注入

### 数据访问 (Persistence)
- `GameDbContext.cs`: 数据库上下文实现

### 仓储实现 (Repositories)
- `PlayerRepository.cs`: 玩家仓储实现
- `RoomRepository.cs`: 房间仓储实现

### 基础设施服务 (Services)
- `CollisionDetectionService.cs`: 碰撞检测服务实现

### 其他
- `Class1.cs`: 默认生成的示例类，可能未使用
- `GameServer.Infrastructure.csproj`: 项目文件

## 依赖关系
- 依赖 GameServer.Domain 项目

## 设计理念
基础设施层负责处理与具体技术相关的实现细节，如数据库访问、外部API调用等，使领域层不受具体技术的影响，保持业务逻辑的纯粹性。
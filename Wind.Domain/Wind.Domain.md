# Wind.Domain 项目功能划分

## 项目概述
Wind.Domain 项目包含领域模型、仓储接口和领域服务接口，定义了游戏的核心业务概念和规则。

## 主要功能
1. 定义领域模型
2. 定义仓储接口
3. 定义领域服务接口

## 关键组件

### 领域模型 (Entities)
- `GameObject.cs`: 游戏对象实体
- `Player.cs`: 玩家实体
- `PlayerCharacter.cs`: 玩家角色实体
- `Room.cs`: 房间实体

### 仓储接口 (Repositories)
- `IPlayerRepository.cs`: 玩家仓储接口
- `IRoomRepository.cs`: 房间仓储接口

### 领域服务接口 (Services)
- `ICollisionDetectionService.cs`: 碰撞检测领域服务接口

### 其他
- `Class1.cs`: 默认生成的示例类，可能未使用
- `Wind.Domain.csproj`: 项目文件

## 依赖关系
- 无外部项目依赖，是其他项目的依赖项

## 设计理念
采用领域驱动设计(DDD)思想，将核心业务概念和规则封装在领域层，确保业务逻辑的纯粹性和独立性。
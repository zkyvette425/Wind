# Wind.Application 项目功能划分

## 项目概述
Wind.Application 项目主要包含应用层服务和依赖注入配置，负责协调核心业务逻辑与外部接口。

## 主要功能
1. 实现应用层服务
2. 配置依赖注入
3. 协调不同组件之间的交互

## 关键组件

### 依赖注入配置
- `DependencyInjection.cs`: 配置应用层服务的依赖注入

### 服务层
- `Services/PlayerService.cs`: 玩家相关应用服务
- `Services/RoomService.cs`: 房间相关应用服务

### 其他
- `Class1.cs`: 默认生成的示例类，可能未使用
- `Wind.Application.csproj`: 项目文件

## 依赖关系
- 可能依赖 Wind.Core 等核心项目

## 设计理念
应用层作为核心业务逻辑与外部接口之间的桥梁，提供了更高级别的业务流程编排，同时通过依赖注入实现了组件的解耦。
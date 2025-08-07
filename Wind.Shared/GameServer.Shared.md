# GameServer.Shared 项目功能划分

## 项目概述
GameServer.Shared 项目包含多个项目共享的代码，如常量、协议定义等，避免代码重复。

## 主要功能
1. 定义共享常量
2. 定义通信协议
3. 提供共享工具类

## 关键组件

### 常量 (Constants)
- 包含项目间共享的常量定义

### 协议定义 (Protocols)
- `BaseMessage.cs`: 消息基类
- `ChatMessage.cs`: 聊天消息定义
- `LoginMessage.cs`: 登录消息定义
- `PositionUpdateMessage.cs`: 位置更新消息定义

### 其他
- `Class1.cs`: 默认生成的示例类，可能未使用
- `GameServer.Shared.csproj`: 项目文件

## 依赖关系
- 无外部项目依赖，被多个项目依赖

## 设计理念
通过共享项目减少代码重复，确保多个项目使用一致的常量和协议定义，提高代码的可维护性。
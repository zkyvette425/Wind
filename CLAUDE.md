# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 🚨 重要提醒 - 项目纲领系统

### 📍 纲领位置和获取方式
- **完整纲领文档**: `plans/纲领.md` - 包含所有技术决策、架构原则和开发规范
- **工作模式指导**: 本文件仅为快速引导，详细指导在纲领文档中

### 🔄 上下文恢复机制
**如果上下文发生切换，必须按以下步骤恢复项目一致性：**

1. **阅读纲领文档** (`plans/纲领.md`) - 了解完整的项目愿景和技术决策
2. **检查当前TODO** (`plans/v1.0-基础架构.md` 等) - 了解当前进展状态  
3. **查阅变更记录** (`plans/版本变更日志.md`) - 了解最新的变更情况
4. **Context7文档查阅** - 确保使用最新技术文档

### 📋 TODO列表工作模式
- **双重完成标准**: 每个任务必须标记 ✅ 已完成 + 🧪 已测试
- **变更记录要求**: 任何修改都要更新对应的变更记录文档
- **版本化管理**: 当前版本所有任务完成后才进入下一版本

## 🎯 项目概述

这是一个基于 **.NET 9 + Microsoft Orleans + MagicOnion** 的现代化分布式游戏服务器框架。项目已从传统分层架构完全重构为Actor模型，专为高并发多人在线游戏设计。

### Context7 文档查阅原则 🔍 **最高优先级**

**在编写任何代码前，必须先通过 Context7 查阅对应技术的最新官方文档！**

必须查阅的核心技术：
- Microsoft Orleans 9.2.1
- MagicOnion (gRPC + MessagePack)
- Redis Stack
- MongoDB EF Core Provider
- .NET 9 性能优化

## 🏗️ 当前架构设计 (v1.0阶段)

### 项目结构 (彻底重构完成)
```
Wind/
├── plans/                    # 📋 项目管理文档系统
├── Wind.Server/              # 🎮 Orleans Silo宿主 (已完全清理)
├── Wind.GrainInterfaces/     # 📄 Grain接口定义 (已创建)
├── Wind.Grains/             # ⚡ Orleans Grain实现 (已创建)
├── Wind.Shared/             # 📦 消息协议 (仅保留Protocols)
├── Wind.Client/             # 🔌 客户端SDK (已创建)
└── Wind.Tests/              # 🧪 测试套件 (已完全清理)
```

### 技术栈
- **运行时**: .NET 9 (最新性能优化)
- **分布式框架**: Microsoft Orleans 9.2.1 (Virtual Actor模型)
- **网络通信**: MagicOnion (gRPC + MessagePack，专为游戏设计)
- **数据存储**: Redis Stack (缓存) + MongoDB (持久化)
- **测试框架**: xUnit + Moq (集成测试优先)

## 🔧 开发命令 (当前阶段)

### 构建和运行
```bash
# 构建解决方案 (Orleans项目)
dotnet build Wind.sln

# 运行Orleans Silo (重构后)
dotnet run --project Wind.Server\Wind.Server.csproj

# 还原NuGet包 (包含Orleans/MagicOnion)
dotnet restore
```

### 测试命令
```bash
# 运行所有测试 (Actor模型测试)
dotnet test Wind.sln

# 运行集成测试 (分布式场景)
dotnet test Wind.Tests\Wind.Tests.csproj

# 性能测试 (Orleans Grain性能)
dotnet test --collect:"XPlat Code Coverage"
```

## 📈 开发进度追踪

### 当前状态: v1.0 基础架构搭建
- ✅ 完整文档体系建立
- ✅ 架构彻底清理和重构
- ✅ 纯净Orleans项目结构创建
- ⏳ Orleans包安装和Silo配置 (下一步)
- ⏳ MagicOnion网络层集成
- ⏳ 代码规范和开发环境

### 服务器运行配置
- Orleans Silo端口: 待配置
- MagicOnion gRPC端口: 待配置  
- 管理端口: 待配置

## ⚠️ 重要注意事项

### 架构迁移状态 (彻底重构完成)
1. **已彻底删除**: Wind.Application, Wind.Domain, Wind.Infrastructure, Wind.Core
2. **已完全清理**: Wind.Server (删除Controllers/Hubs，重写Program.cs)
3. **已创建完成**: Wind.Grains, Wind.GrainInterfaces, Wind.Client (纯净Orleans项目)

### 变更记录要求
任何代码修改必须同时更新：
- 对应的TODO状态 (已完成 + 已测试)
- 相关变更记录文档 (`plans/版本变更日志.md` 等)
- 影响的其他文档

### 学习和文档
- 📚 **强制要求**: 使用任何新技术前先通过Context7查阅最新文档
- 📝 **记录变更**: 每次修改都要记录原因和影响范围
- 🧪 **测试标准**: 集成测试 > 单元测试 > 性能测试的优先级

## 🎮 核心业务组件 (规划中)

### Orleans Grain架构 (v1.1目标)
- **PlayerGrain**: 玩家状态和行为管理
- **RoomGrain**: 游戏房间和匹配逻辑
- **CombatGrain**: 战斗系统处理
- **ChatGrain**: 聊天和社交功能

### 数据存储策略 (v1.2目标)
- **Redis**: 实时数据、会话、排行榜
- **MongoDB**: 玩家档案、游戏记录、配置

### 性能目标
- 并发连接: > 10,000
- 响应延迟: < 50ms  
- 内存效率: 单Grain < 1MB

---

**🚨 关键提醒**: 本文件是快速参考，完整的项目指导和决策依据请查看 `plans/纲领.md`。在任何重大决策前，务必参考纲领文档确保一致性。
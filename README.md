# Wind 游戏服务器框架

![Orleans](https://img.shields.io/badge/Orleans-9.2.1-blue)
![MagicOnion](https://img.shields.io/badge/MagicOnion-7.0.6-green)
![.NET](https://img.shields.io/badge/.NET-9.0-purple)

基于 Microsoft Orleans + MagicOnion 的现代化分布式游戏服务器框架。

## 🚀 快速开始

### 前置要求

- .NET 9.0 SDK
- Docker Desktop (用于本地开发环境)
- Visual Studio 2022 或 JetBrains Rider (推荐)

### 一键启动开发环境

```bash
# 启动完整开发环境 (包括数据库、缓存、日志等服务)
scripts\start-dev.bat

# 停止开发环境
scripts\stop-dev.bat
```

### 手动启动

```bash
# 1. 启动基础服务
docker-compose up -d

# 2. 构建项目
dotnet build Wind.sln

# 3. 运行服务器
dotnet run --project Wind.Server\Wind.Server.csproj

# 4. 运行测试客户端
dotnet run --project Wind.Client\Wind.Client.csproj
```

## 🏗️ 项目架构

```
Wind/
├── 📁 Wind.Server/              # Orleans Silo宿主 + MagicOnion服务端
├── 📁 Wind.GrainInterfaces/     # Orleans Grain接口定义
├── 📁 Wind.Grains/             # Orleans Grain实现 + MagicOnion服务实现  
├── 📁 Wind.Shared/             # 共享协议和消息定义
├── 📁 Wind.Client/             # 客户端SDK和测试程序
├── 📁 Wind.Tests/              # 单元测试和集成测试
├── 📁 docs/                    # 技术文档和开发规范
├── 📁 scripts/                 # 开发工具脚本
└── 📄 docker-compose.yml       # 开发环境服务配置
```

## 🛠️ 核心技术栈

- **🎮 分布式框架**: Microsoft Orleans 9.2.1
- **🌐 网络通信**: MagicOnion 7.0.6 (gRPC + MessagePack)
- **💾 数据存储**: Redis Stack + MongoDB  
- **📝 日志系统**: Serilog + Seq
- **🧪 测试框架**: xUnit + Orleans.TestingHost
- **🐳 开发环境**: Docker + Docker Compose

## 📊 服务端口

| 服务 | 端口 | 说明 |
|------|------|------|
| Wind Game Server | 5271 | MagicOnion gRPC服务 |
| Orleans Gateway | 30000 | Orleans客户端连接 |
| Redis | 6379 | 缓存和会话存储 |
| RedisInsight | 8001 | Redis管理界面 |
| MongoDB | 27017 | 持久化数据库 |
| Seq | 8080 | 结构化日志查看 |
| Jaeger | 16686 | 分布式跟踪界面 |

## 🧪 运行测试

```bash
# 运行所有测试
dotnet test Wind.sln

# 运行基础Orleans测试
dotnet test Wind.Tests\Wind.Tests.csproj --filter "BasicGrainTests"

# 运行特定测试类
dotnet test --filter "FullyQualifiedName~HelloGrainTests"
```

## 🎯 功能特性

### ✅ 已完成功能 (v1.3)

- ✅ Orleans分布式Actor系统
- ✅ MagicOnion高性能gRPC通信  
- ✅ 客户端SDK (支持Orleans直连+MagicOnion RPC)
- ✅ 连接重试和故障恢复机制
- ✅ 统一依赖版本管理
- ✅ Orleans测试框架集成
- ✅ Docker化开发环境
- ✅ 结构化日志系统

### 🚧 开发中功能 (v1.4)

- 🚧 玩家管理Grain
- 🚧 房间系统Grain  
- 🚧 Redis缓存层集成
- 🚧 MongoDB持久化层
- 🚧 性能监控和告警

## 📚 开发文档

- [Orleans开发规范](docs/Orleans开发规范.md) - 团队开发标准
- [项目纲领](plans/纲领.md) - 技术架构和原则  
- [版本变更日志](plans/版本变更日志.md) - 功能更新记录
- [技术研究记录](plans/技术研究记录.md) - 技术调研文档

## 🔧 开发指南

### 添加新的Orleans Grain

1. 在`Wind.GrainInterfaces`中定义接口
2. 在`Wind.Grains`中实现Grain类
3. 添加对应的单元测试
4. 遵循[开发规范](docs/Orleans开发规范.md)

### 添加新的MagicOnion服务

1. 在`Wind.Shared/Services`中定义服务接口
2. 在`Wind.Grains/Services`中实现服务类
3. 更新客户端SDK集成调用
4. 添加集成测试验证

## 🤝 贡献指南

1. Fork 项目到个人仓库
2. 创建功能分支: `git checkout -b feature/my-feature`
3. 遵循代码规范和测试要求
4. 提交变更: `git commit -m 'feat: 新功能描述'`
5. 推送分支: `git push origin feature/my-feature`  
6. 提交 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 详情请查看 [LICENSE](LICENSE) 文件。

## 📞 支持

如有问题或建议，请提交 [Issue](https://github.com/zkyvette425/Wind/issues)。

---

**🎮 Wind - 为现代多人在线游戏而生的分布式服务器框架**
# Wind Unity客户端文档中心

## 📋 文档导航

### 🎯 [项目管理](./plans/project-management/)
Unity客户端项目规划、决策记录和治理文档
- **[治理文档](./plans/project-management/governance/)**: Unity客户端纲领、架构原则
- **[路线图](./plans/project-management/roadmaps/)**: u1.0基础架构、u1.1DI容器、u1.2资源管理
- **[决策记录](./plans/project-management/decisions/)**: 技术决策、架构选型分析

### 📝 [变更记录](./plans/change-logs/)
Unity客户端版本变更历史和更新日志
- **[当前活跃](./plans/change-logs/current/)**: 最新的Unity开发变更记录
- **[历史归档](./plans/change-logs/archives/)**: 按版本组织的历史变更

### 🔬 [技术研究](./plans/technical-research/)
Unity技术调研、方案评估和最佳实践
- **[当前研究](./plans/technical-research/current/)**: HybridCLR、YooAsset、Package Manager研究
- **[研究归档](./plans/technical-research/archives/)**: 按技术领域分类的历史研究

### 🧪 [质量保证](./plans/quality-assurance/)
Unity开发测试管理、质量流程和事故处理
- **[事故管理](./plans/quality-assurance/incidents/)**: Unity开发质量事故档案
- **[质量流程](./plans/quality-assurance/processes/)**: Unity开发质量标准、检查清单

### 📖 [技术文档](./docs/)
Unity包和框架的详细技术文档
- **[架构设计](./docs/architecture/)**: 包架构、依赖关系、设计模式
- **[包文档](./docs/packages/)**: 每个com.wind.*包的详细文档
- **[用户指南](./docs/user-guides/)**: 入手流程、开发指南、最佳实践

### 📄 [文档模板](./templates/)
Unity项目标准化文档模板和规范
- 包开发文档模板
- API文档模板
- 用户指南模板

## 🔍 快速查找

### 最新重要更新
- [Unity客户端纲领](./plans/project-management/governance/unity-纲领.md) - 核心架构和技术决策
- [包架构设计](./docs/architecture/packages-architecture.md) - 完整包体系设计
- [用户入手流程](./docs/user-guides/user-onboarding.md) - 从GitHub到实际使用

### 核心文档
- [Unity客户端纲领](./plans/project-management/governance/unity-纲领.md) - 项目愿景和技术决策
- [实施路线图](./plans/project-management/roadmaps/implementation-roadmap.md) - 44-52周开发计划
- [技术分析报告](./plans/technical-research/current/technical-analysis.md) - HybridCLR+YooAsset深度分析
- [质量事故档案](./plans/quality-assurance/incidents/unity-质量事故档案.md) - Unity开发问题防范

## 📊 与服务端文档对比

| 维度 | 服务端 | Unity客户端 |
|------|--------|-------------|
| 技术栈 | .NET 9 + Orleans + MagicOnion | Unity + HybridCLR + 自研框架 |
| 架构模式 | Actor模型 + 微服务 | 包模块化 + 统一框架 |
| 部署方式 | Docker + K8s | Unity Package Manager |
| 质量保证 | 集成测试优先 | 包测试 + 示例验证 |
| 文档深度 | 生产级详细文档 | 开发级详细文档 |

## 🛠️ 文档管理工具

### 容量管理
```bash
# 检查Unity文档大小
powershell tools/unity-doc-manager/check-size.ps1 "unity-docs/文档名.md"

# Unity文档分片
powershell tools/unity-doc-manager/split-doc.ps1 "unity-docs/文档名.md"
```

### Unity专用工具
```bash
# 包文档生成
powershell tools/package-doc-generator/generate-docs.ps1

# API文档提取
powershell tools/api-doc-extractor/extract-apis.ps1
```

## ℹ️ 使用说明

1. **架构学习**: 从Unity客户端纲领开始，了解完整技术决策
2. **包开发**: 使用包文档了解每个com.wind.*包的详细设计
3. **用户指南**: 查看用户入手流程，了解从GitHub到实际使用的完整过程
4. **质量控制**: 参考质量事故档案，避免Unity开发中的常见问题
5. **技术决策**: 查阅技术研究文档，了解HybridCLR等关键技术的深度分析

---

**📧 维护信息**: Unity客户端文档体系与服务端保持同等详细程度，确保企业级Unity游戏开发的完整文档支撑。

**🔗 关联文档**: 
- [服务端文档中心](../plans/README.md)
- [服务端纲领](../plans/project-management/governance/纲领.md)
- [服务端质量事故档案](../plans/quality-assurance/incidents/质量事故档案.md)
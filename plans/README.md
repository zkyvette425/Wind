# Wind 项目文档中心

## 📋 文档导航

### 🎯 [项目管理](./project-management/)
项目规划、决策记录和治理文档
- **[路线图](./project-management/roadmaps/)**: v1.0基础架构、v1.1核心服务、v1.2数据存储
- **[决策记录](./project-management/decisions/)**: 技术决策、变更影响分析
- **[治理文档](./project-management/governance/)**: 项目纲领、文档管理规范

### 📝 [变更记录](./change-logs/)
版本变更历史和更新日志
- **[当前活跃](./change-logs/current/)**: 最新的变更记录
- **[历史归档](./change-logs/archives/)**: 按月组织的历史变更
- **[导航索引](./change-logs/版本变更日志-索引.md)**: 快速查找变更历史

### 🔬 [技术研究](./technical-research/)
技术调研、方案评估和最佳实践
- **[当前研究](./technical-research/current/)**: 正在进行的技术研究
- **[研究归档](./technical-research/archives/)**: 按技术领域分类的历史研究

### 🧪 [质量保证](./quality-assurance/)
测试管理、质量流程和事故处理
- **[测试管理](./quality-assurance/testing/)**: 测试策略、验证流程
- **[事故管理](./quality-assurance/incidents/)**: 质量事故档案、问题追踪
- **[质量流程](./quality-assurance/processes/)**: 质量标准、检查清单

### 📄 [文档模板](./templates/)
标准化文档模板和规范
- 项目规划模板
- 技术研究模板
- 变更记录模板

## 🔍 快速查找

### 最新重要更新
- [智能文档分片管理系统](./change-logs/current/版本变更日志.md) (2025-08-17)
- [3.3.2分布式事务验证](./change-logs/archives/版本变更日志-2025-08-历史.md) (2025-08-17)
- [MongoDB副本集配置](./change-logs/archives/版本变更日志-2025-08-历史.md) (2025-08-17)

### 核心文档
- [项目纲领](./project-management/governance/纲领.md) - 项目愿景和技术决策
- [v1.2数据存储](./project-management/roadmaps/v1.2-数据存储.md) - 当前开发阶段
- [技术研究记录](./technical-research/current/技术研究记录.md) - 技术方案和最佳实践
- [质量事故档案](./quality-assurance/incidents/质量事故档案.md) - 问题防范和解决方案

## 📊 文档状态监控

### 文档大小监控
- ✅ **所有文档**: 均控制在25KB以内，确保Claude Code可读
- ✅ **技术研究记录**: 已完成分片，当前文件1.2KB
- ✅ **变更记录**: 已完成分片，当前文件3.1KB

### 最近维护
- **2025-08-17**: 实施智能文档分片管理系统
- **2025-08-17**: 优化目录结构，实现分类管理
- **2025-08-17**: 建立多层级导航系统

## 🛠️ 文档管理工具

### 容量管理
```bash
# 检查文档大小
powershell tools/doc-manager/check-size-simple.ps1 "plans/technical-research/current/技术研究记录.md"

# 执行文档分片
powershell tools/doc-manager/split-timeline-doc.ps1 "plans/文档名.md"
```

### 目录结构
```
plans/
├── project-management/     # 项目管理文档
├── change-logs/           # 变更记录系统  
├── technical-research/    # 技术研究文档
├── quality-assurance/     # 质量保证文档
└── templates/            # 文档模板库
```

## ℹ️ 使用说明

1. **查找文档**: 使用上方的分类导航快速定位所需文档
2. **添加内容**: 优先添加到对应分类的`current/`目录
3. **历史查找**: 使用各分类的索引文件快速定位历史信息
4. **容量监控**: 添加大量内容前，使用工具检查文档大小
5. **模板使用**: 创建新文档时，参考`templates/`目录的标准模板

---

**📧 维护信息**: 本导航系统是智能文档分片管理系统的一部分，确保所有文档都能被Claude Code完整读取和高效管理。
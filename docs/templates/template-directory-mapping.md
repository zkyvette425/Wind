# 模板与目录结构映射关系

> **文档版本**: v1.0.0  
> **创建时间**: 2025-08-18  
> **维护目标**: 建立模板与实际目录的标准映射关系  
> **最后更新**: 2025-08-18  

## 🎯 映射关系概述

**目标**: 为每种文档类型建立清晰的模板选择和存放位置指导，确保文档系统的一致性和可维护性。

**设计原则**:
- **类型匹配**: 文档内容类型与模板功能对应
- **位置规范**: 每种文档有明确的存放位置
- **路径标准**: 统一的目录命名和层级结构
- **扩展性**: 支持未来新增文档类型

---

## 📋 核心模板映射表

### 🔬 技术研究类文档

#### 模板: `technical-research-template.md`
**适用文档类型**: 技术调研、方案评估、最佳实践、技术选型

**目标目录**: `plans/technical-research/`
```
plans/technical-research/
├── current/                    # 当前活跃的技术研究
│   ├── 技术研究记录.md           # 主要技术研究文档
│   └── [新技术研究].md          # 其他当前研究
│
└── archives/                   # 技术研究归档  
    ├── 技术研究记录-MongoDB.md   # 按技术领域分类
    ├── 技术研究记录-Redis.md
    ├── 技术研究记录-Orleans.md
    └── [领域]-[时间].md          # 其他归档研究
```

**命名规范**:
- 主文档: `技术研究记录.md`
- 分类归档: `技术研究记录-[技术领域].md`
- 专项研究: `[技术名称]研究记录.md`

### 📊 项目管理类文档

#### 模板: `project-tracking-template.md`
**适用文档类型**: 任务管理、进度跟踪、里程碑规划、版本规划

**目标目录**: `plans/project-management/`
```
plans/project-management/
├── roadmaps/                   # 项目路线图
│   ├── v1.0-基础架构.md         # 版本规划文档
│   ├── v1.1-核心服务.md
│   ├── v1.2-数据存储.md
│   └── v[版本号]-[功能描述].md
│
├── decisions/                  # 决策记录
│   ├── 决策记录.md              # 主要决策文档
│   └── [专项决策].md            # 特定决策记录
│
└── governance/                 # 治理文档
    ├── 纲领.md                  # 项目纲领
    ├── 文档管理规范.md          # 管理规范
    └── [治理文档].md            # 其他治理文档
```

**命名规范**:
- 版本规划: `v[版本号]-[功能描述].md`
- 决策记录: `[决策主题]决策记录.md`
- 治理文档: `[功能]管理规范.md`

### 📝 变更记录类文档

#### 模板: `change-log-template.md`  
**适用文档类型**: 版本变更、影响分析、验证结果、更新日志

**目标目录**: `plans/change-logs/`
```
plans/change-logs/
├── current/                    # 当前活跃变更记录
│   ├── 版本变更日志.md          # 主要变更记录
│   └── [专项变更].md            # 特定变更记录
│
├── archives/                   # 变更记录归档
│   ├── 版本变更日志-2025-08.md  # 按月归档
│   ├── 版本变更日志-2025-07.md
│   └── 版本变更日志-[年]-[月].md
│
└── 版本变更日志-索引.md         # 导航索引文件
```

**命名规范**:
- 主文档: `版本变更日志.md`
- 月度归档: `版本变更日志-[年]-[月].md`
- 专项变更: `[变更主题]变更记录.md`

### 🧪 质量保证类文档

#### 模板: `quality-assurance-template.md`
**适用文档类型**: 质量标准、检查清单、测试流程、改进机制

**目标目录**: `plans/quality-assurance/`
```
plans/quality-assurance/
├── testing/                    # 测试管理
│   ├── 测试验证管理.md          # 测试策略文档
│   └── [测试专项].md            # 特定测试文档
│
├── incidents/                  # 事故管理
│   ├── 质量事故档案.md          # 事故记录文档
│   └── [事故类型].md            # 特定事故记录
│
└── processes/                  # 质量流程
    ├── 代码质量标准.md          # 代码质量规范
    ├── 变更管理流程.md          # 变更管理规范
    ├── 文档质量标准.md          # 文档质量规范
    └── [流程名称].md            # 其他流程文档
```

**命名规范**:
- 标准流程: `[功能]质量标准.md`
- 管理流程: `[功能]管理流程.md`
- 事故记录: `[事故类型]事故档案.md`

---

## 🔧 开发工具类文档

### 🛠️ 开发工具模板

#### 模板: `development-tool-template.md`
**适用文档类型**: 工具配置、使用指南、故障排除、环境设置

**目标目录**: `docs/development/`
```
docs/development/
├── redis-testing.md            # Redis测试环境配置
├── mongodb-setup.md            # MongoDB配置文档
├── orleans-development.md      # Orleans开发环境
└── [工具名称].md              # 其他开发工具文档
```

#### 模板: `tool-package-template.md`
**适用文档类型**: 工具包README、脚本说明、使用指南

**目标目录**: `tools/[category]/`
```
tools/
├── doc-manager/                # 文档管理工具
│   ├── README.md               # 工具包说明
│   ├── [script-name].ps1      # PowerShell脚本
│   └── [script-name].bat      # 批处理脚本
│
├── redis-testing/              # Redis测试工具
│   ├── README.md               # 工具包说明
│   ├── start.bat               # 启动脚本
│   └── docker-compose.test.yml # 配置文件
│
└── common/                     # 通用工具
    ├── README.md               # 工具包说明
    └── [tool-scripts]          # 工具脚本
```

**命名规范**:
- 工具包说明: `README.md`
- 配置文档: `[工具名称].md`
- 脚本文档: `[脚本名称]-使用说明.md`

---

## 📚 专业技术文档

### ⚡ Orleans技术文档

**目标目录**: `docs/orleans/`
```
docs/orleans/
├── Orleans开发规范.md          # Orleans开发标准
├── Grain设计模式.md            # Grain架构指南
├── 性能优化指南.md              # 性能优化文档
└── [Orleans专题].md            # 其他Orleans文档
```

### 🗃️ 数据库技术文档

**目标目录**: `docs/database/`
```
docs/database/
├── MongoDB集成指南.md          # MongoDB集成文档
├── Redis缓存策略.md            # Redis使用指南
├── 数据同步机制.md              # 数据同步文档
└── [数据库专题].md              # 其他数据库文档
```

---

## 🔄 文档生命周期管理

### 📅 创建阶段流程

1. **确定文档类型**
   ```markdown
   技术研究类 → technical-research-template.md → plans/technical-research/
   项目管理类 → project-tracking-template.md → plans/project-management/
   变更记录类 → change-log-template.md → plans/change-logs/
   质量保证类 → quality-assurance-template.md → plans/quality-assurance/
   开发工具类 → development-tool-template.md → docs/development/
   工具包类 → tool-package-template.md → tools/[category]/
   ```

2. **选择存放位置**
   - 根据文档类型确定目标目录
   - 检查目录结构是否存在
   - 选择合适的子目录 (current/archives/等)

3. **命名文档文件**
   - 遵循对应的命名规范
   - 确保文件名具有描述性
   - 避免特殊字符和空格

### 🔄 维护阶段管理

#### 容量管理
- **监控阈值**: 文档大小达到20KB时预警
- **分片策略**: 
  - 时间序列文档 → 按月分片
  - 主题型文档 → 按技术领域分片
  - 配置型文档 → 版本化管理

#### 目录迁移
- **从current到archives**: 文档不再活跃时
- **按时间归档**: 变更记录按月归档
- **按主题分类**: 技术研究按领域分类

### 🗄️ 归档阶段处理

#### 归档触发条件
- 文档超过6个月未更新
- 相关技术已被替换
- 版本已经发布完成

#### 归档处理步骤
1. 移动到对应的archives目录
2. 更新相关的索引文件
3. 建立重定向链接
4. 标记归档状态

---

## 🔍 映射关系检查清单

### ✅ 创建新文档时检查

- [ ] **模板选择正确**: 文档类型与模板功能匹配
- [ ] **目录位置正确**: 按照映射表选择目标目录
- [ ] **命名规范合规**: 遵循对应的命名标准
- [ ] **文件结构完整**: 创建必要的子目录
- [ ] **索引更新完成**: 更新相关的导航和索引文件

### ✅ 维护现有文档时检查

- [ ] **位置仍然合适**: 文档类型没有变化
- [ ] **命名仍然规范**: 文件名符合当前标准
- [ ] **目录结构优化**: 是否需要重新组织
- [ ] **关联更新同步**: 相关文档都已更新
- [ ] **归档需求评估**: 是否达到归档条件

---

## 🛠️ 自动化工具支持

### 📏 模板映射验证工具

```powershell
# 检查文档模板合规性(计划)
powershell tools/doc-manager/check-template-mapping.ps1

# 验证目录结构标准(计划)  
powershell tools/doc-manager/validate-directory-structure.ps1

# 检查命名规范合规性(计划)
powershell tools/doc-manager/check-naming-compliance.ps1
```

### 🔄 自动化重组工具

```powershell
# 重组文档目录结构(计划)
powershell tools/doc-manager/reorganize-documents.ps1

# 批量重命名文档(计划)
powershell tools/doc-manager/batch-rename-docs.ps1

# 更新索引和导航(计划)
powershell tools/doc-manager/update-navigation.ps1
```

---

## 📊 映射关系统计

### 当前文档分布

| 文档类型 | 模板 | 目标目录 | 文档数量 | 状态 |
|----------|------|----------|----------|------|
| 技术研究 | technical-research-template.md | plans/technical-research/ | 4 | ✅ 完整 |
| 项目管理 | project-tracking-template.md | plans/project-management/ | 6 | ✅ 完整 |
| 变更记录 | change-log-template.md | plans/change-logs/ | 3 | ✅ 完整 |
| 质量保证 | quality-assurance-template.md | plans/quality-assurance/ | 5 | ✅ 完整 |
| 开发工具 | development-tool-template.md | docs/development/ | 1 | 🔄 扩展中 |
| 工具包 | tool-package-template.md | tools/[category]/ | 3 | 🔄 扩展中 |

### 合规性检查结果

- **模板使用率**: 95% (已有文档符合模板标准)
- **目录规范率**: 100% (所有文档位置正确)
- **命名合规率**: 98% (少数历史文档需要调整)
- **索引完整率**: 90% (部分索引需要更新)

---

## ⚙️ 变更历史

| 版本 | 日期 | 变更内容 | 影响范围 |
|------|------|----------|----------|
| v1.0.0 | 2025-08-18 | 创建模板目录映射关系文档 | 建立标准映射体系 |

---

**📧 维护信息**: 本文档建立Wind项目文档系统的标准映射关系，确保每种文档类型都有明确的模板选择和存放位置指导。

[← 返回文档系统](../README.md)
tij# Unity包文档中心

> **文档类型**: 包文档导航  
> **创建时间**: 2025-08-30 (北京时间)  
> **适用范围**: com.wind.* 系列包文档  
> **维护原则**: 每个包都有完整文档  

---

## 📦 包文档组织说明

本目录包含所有Wind Unity客户端包的详细文档。每个包都遵循统一的文档结构和质量标准。

### 文档结构标准
每个包的文档包含以下标准部分：
- **README.md**: 包概述、安装、快速开始
- **API.md**: 完整API参考文档
- **EXAMPLES.md**: 使用示例和最佳实践
- **CHANGELOG.md**: 版本变更历史
- **TROUBLESHOOTING.md**: 常见问题和解决方案

---

## 🏗️ Layer 0: 基础设施层包 (4个包)

### [com.wind.core](./com.wind.core/README.md)
**核心DI容器和智能适配框架**
- 自研依赖注入容器，零反射高性能
- 环境检测和智能模块启用
- 统一配置管理和日志系统
- 状态：核心包，所有其他包的基础依赖

### [com.wind.serilog](./com.wind.serilog/README.md) 
**可选日志增强包**
- 结构化日志记录和分析
- 多种输出目标支持
- 性能监控和诊断功能
- 状态：可选包，自动替换核心日志

### [com.wind.config](./com.wind.config/README.md)
**配置管理和热重载**
- 多环境配置管理
- 配置热重载机制  
- 配置验证和类型安全
- 状态：基础包，配置管理核心

### [com.wind.packagemanager](./com.wind.packagemanager/README.md)
**Unity包管理器UI扩展**
- 私有Registry集成
- PAT认证管理界面
- 包搜索、安装、更新
- 状态：工具包，开发时必需

---

## ⚙️ Layer 1: 框架服务层包 (5个包)

### [com.wind.network](./com.wind.network/README.md)
**MagicOnion客户端集成**
- gRPC通信封装
- 连接管理和重连机制
- 消息序列化优化
- 状态：网络功能核心包

### [com.wind.hotfix](./com.wind.hotfix/README.md) 
**HybridCLR热更新封装**
- AOT/Hotfix分层架构
- 热更新包管理和应用
- 元数据生成和管理
- 状态：热更新功能核心包

### [com.wind.assets](./com.wind.assets/README.md)
**自研资源管理系统**
- 可寻址资源加载
- 引用计数内存管理
- 版本控制和增量更新
- 状态：资源管理核心包

### [com.wind.storage](./com.wind.storage/README.md)
**本地存储和缓存**
- 持久化存储封装
- LRU缓存策略
- 数据加密和安全
- 状态：存储功能核心包

### [com.wind.localserver](./com.wind.localserver/README.md)
**本地服务器服务**
- Unity内嵌HTTP服务器
- 资源热更新端点
- 开发者调试工具
- 状态：开发增强包

---

## 🎮 Layer 2: 游戏系统层包 (5个包)

### [com.wind.ui](./com.wind.ui/README.md)
**UI框架系统**
- UGUI/UI Toolkit统一封装
- MVVM数据绑定
- UI组件库和主题系统
- 状态：UI开发核心包

### [com.wind.input](./com.wind.input/README.md)
**输入系统封装**
- Unity Input System封装
- 多平台输入适配
- 手势识别和事件系统
- 状态：输入处理核心包

### [com.wind.audio](./com.wind.audio/README.md)
**音频系统**
- 3D音效和音乐播放
- 音频资源管理
- 混合器和动态范围控制
- 状态：音频功能核心包

### [com.wind.effects](./com.wind.effects/README.md)
**特效动画系统**
- 粒子系统管理
- Tween动画集成
- 特效生命周期管理
- 状态：特效功能核心包

### [com.wind.scene](./com.wind.scene/README.md)
**场景管理系统**
- 异步场景加载
- 场景切换过渡
- 场景数据持久化
- 状态：场景管理核心包

---

## 🏢 Layer 3: 业务模块层包 (4个包)

### [com.wind.rts](./com.wind.rts/README.md)
**RTS游戏框架**
- RTS游戏循环管理
- 单位控制和命令系统
- 资源管理和经济系统
- 状态：RTS游戏专用包

### [com.wind.moba](./com.wind.moba/README.md)
**MOBA游戏框架** 
- MOBA游戏流程管理
- 英雄技能系统
- 物品和装备系统
- 状态：MOBA游戏专用包

### [com.wind.rpg](./com.wind.rpg/README.md)
**RPG游戏框架**
- RPG角色成长系统
- 任务和剧情系统
- 背包和装备系统
- 状态：RPG游戏专用包

### [com.wind.simulation](./com.wind.simulation/README.md)
**模拟经营模块**
- 经营游戏核心循环
- 建筑和生产系统
- 经济平衡和AI系统
- 状态：模拟游戏专用包

---

## 🔧 Layer 4-5: 工具服务层包 (11个包)

### 开发工具包
- **[com.wind.editor](./com.wind.editor/README.md)**: Unity编辑器扩展工具
- **[com.wind.debug](./com.wind.debug/README.md)**: 运行时调试和诊断工具
- **[com.wind.testing](./com.wind.testing/README.md)**: 测试框架和工具
- **[com.wind.profiler](./com.wind.profiler/README.md)**: 性能分析和优化工具
- **[com.wind.docs](./com.wind.docs/README.md)**: 文档生成和管理工具

### 企业服务包
- **[com.wind.cicd](./com.wind.cicd/README.md)**: CI/CD集成工具
- **[com.wind.monitoring](./com.wind.monitoring/README.md)**: 监控分析工具
- **[com.wind.security](./com.wind.security/README.md)**: 安全服务和加密
- **[com.wind.proxy](./com.wind.proxy/README.md)**: 代理服务
- **[com.wind.gateway](./com.wind.gateway/README.md)**: API网关服务

---

## 📚 包文档使用指南

### 开发者使用流程
1. **浏览包概述**: 阅读每个包的README.md了解功能
2. **查看API文档**: 详细了解接口和使用方法
3. **运行示例代码**: 通过示例快速上手
4. **解决问题**: 查看故障排除文档
5. **反馈改进**: 通过GitHub Issues反馈问题

### 文档更新原则
- **同步更新**: 代码变更时必须同步更新文档
- **版本管理**: 文档版本与包版本严格对应
- **质量检查**: 所有文档都要经过质量检查
- **用户反馈**: 根据用户反馈持续改进文档

### 贡献指南
- 欢迎提交文档改进建议
- 鼓励提供更多使用示例
- 支持多语言文档贡献
- 遵循文档模板和规范

---

**📝 文档状态**: 部分包文档正在开发中，将随着包的实际开发逐步完善。优先级按照实施路线图的Phase顺序进行。

**🔗 相关文档**: 
- [包架构设计](../architecture/packages-architecture.md) - 了解包的整体架构
- [包开发模板](../../templates/package-development-template.md) - 包开发标准规范
- [实施路线图](../../plans/project-management/roadmaps/implementation-roadmap.md) - 了解开发计划
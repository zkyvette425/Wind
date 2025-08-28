# 网页内容获取工具使用指南

## 版本信息
- **版本**: v1.0.0
- **创建时间**: 2025-08-28 22:50
- **关联工具**: `tools/web-content-fetcher/`
- **关联配置**: 无

---

## 工具概述

### 创建背景
由于Claude Code无法直接访问外部网页（企业网络限制或安全策略），在修复MagicOnion API时无法获取官方文档，导致基于猜测的修复引入质量问题。

### 解决方案
创建本地网页内容获取工具，通过curl或PowerShell获取外部网页内容，保存为本地文件供Claude Code读取和分析。

### 核心价值
- ✅ 解决文档访问限制问题
- ✅ 确保基于准确信息进行开发
- ✅ 提升代码修复质量
- ✅ 建立可重复的文档获取流程

---

## 快速开始

### 1. 工具位置
```
C:\work\Wind\tools\web-content-fetcher\
├── fetch-web-content.bat     # Windows批处理版本（推荐）
├── fetch-web-content.ps1     # PowerShell版本（功能丰富）
├── README.md                 # 详细说明文档
└── examples\
    └── magiconion-docs-urls.txt  # MagicOnion文档链接集合
```

### 2. 基本使用
```batch
# 切换到工具目录
cd C:\work\Wind\tools\web-content-fetcher

# 获取MagicOnion StreamingHub文档
fetch-web-content.bat "https://cysharp.github.io/MagicOnion/streaminghub/streaminghub" --clean -o "streaminghub-docs.txt"

# 在Claude Code中读取结果
# 使用Read工具: Read C:\work\Wind\tools\web-content-fetcher\streaminghub-docs.txt
```

---

## 与Claude Code集成工作流

### 标准工作流程

#### 第1步：确定需要获取的文档
根据当前开发需求，从`examples/magiconion-docs-urls.txt`中选择相关URL。

#### 第2步：获取网页内容
```batch
cd C:\work\Wind\tools\web-content-fetcher
fetch-web-content.bat "<URL>" --clean -o "descriptive-filename.txt"
```

#### 第3步：Claude Code读取和分析
在Claude Code中执行：
```
Read C:\work\Wind\tools\web-content-fetcher\descriptive-filename.txt
```

#### 第4步：提取关键信息
让Claude分析文档内容，提取与当前问题相关的技术要点。

#### 第5步：应用到代码修复
基于准确的文档信息进行代码修复。

#### 第6步：更新项目文档
将重要信息整理到项目技术文档中，避免重复获取。

### MagicOnion API修复专用工作流

#### 核心文档优先级
1. **StreamingHub Group管理** - 解决Group.AddAsync/Remove问题
2. **StreamingHub 广播机制** - 解决BroadcastToGroup问题
3. **错误处理模式** - 解决异步调用问题

#### 具体操作步骤
```batch
# 1. 获取Group管理文档
fetch-web-content.bat "https://cysharp.github.io/MagicOnion/streaminghub/group" --clean -o "group-management.txt"

# 2. 获取广播机制文档  
fetch-web-content.bat "https://cysharp.github.io/MagicOnion/streaminghub/broadcasting" --clean -o "broadcasting-mechanism.txt"

# 3. 获取服务器端实现指南
fetch-web-content.bat "https://cysharp.github.io/MagicOnion/streaminghub/server" --clean -o "server-implementation.txt"
```

然后在Claude Code中依次读取和分析这些文件。

---

## 高级使用技巧

### 1. 批量获取
```batch
@echo off
cd C:\work\Wind\tools\web-content-fetcher

REM 获取所有StreamingHub相关文档
fetch-web-content.bat "https://cysharp.github.io/MagicOnion/streaminghub/streaminghub" --clean -o "01-streaminghub-overview.txt"
fetch-web-content.bat "https://cysharp.github.io/MagicOnion/streaminghub/server" --clean -o "02-streaminghub-server.txt"
fetch-web-content.bat "https://cysharp.github.io/MagicOnion/streaminghub/group" --clean -o "03-streaminghub-group.txt"
fetch-web-content.bat "https://cysharp.github.io/MagicOnion/streaminghub/broadcasting" --clean -o "04-streaminghub-broadcasting.txt"

echo 所有文档获取完成！
pause
```

### 2. 获取示例代码
```batch
# 获取GitHub上的示例代码
fetch-web-content.bat "https://raw.githubusercontent.com/Cysharp/MagicOnion/master/samples/ChatApp/ChatApp.Server/ChatHub.cs" -o "chatapp-example.cs"
```

### 3. 使用PowerShell版本获取更好的文本清理
```powershell
.\fetch-web-content.ps1 "https://cysharp.github.io/MagicOnion/streaminghub/group" -OutputFile "group-management-clean.txt" -CleanHtml -TimeoutSeconds 60
```

---

## 故障排查

### 常见问题

#### 1. curl不可用
```
错误: curl不可用，请确保curl已安装且在PATH中
```
**解决方案**: 
- Windows 10 1803+通常已内置curl
- 或从 https://curl.se/download.html 下载安装
- 使用PowerShell版本作为替代

#### 2. 网络连接问题
```
错误: 获取网页内容失败 (curl返回代码: 28)
```
**解决方案**:
- 检查网络连接
- 增加超时时间：`--timeout 60`
- 检查企业防火墙设置
- 尝试使用代理（如需要）

#### 3. 权限问题
```
错误: 无法写入输出文件
```
**解决方案**:
- 确保当前目录有写入权限
- 尝试指定其他输出路径：`-o "C:\temp\output.txt"`

### 调试技巧

#### 1. 验证工具可用性
```batch
# 测试curl可用性
curl --version

# 测试基本网络连接
curl -I https://www.baidu.com
```

#### 2. 测试获取简单页面
```batch
# 获取简单页面测试
fetch-web-content.bat "https://httpbin.org/get" -o "test-output.txt"
```

#### 3. 查看详细错误信息
PowerShell版本提供更详细的错误信息，有助于调试。

---

## 文档维护

### 版本变更记录

| 版本 | 日期 | 变更内容 | 变更原因 |
|------|------|----------|----------|
| v1.0.0 | 2025-08-28 | 初始版本创建 | 解决MagicOnion文档访问问题 |

### 相关配置文件
- 无直接配置文件
- 依赖系统curl工具
- 依赖PowerShell 5.0+（PowerShell版本）

### 更新检查机制
建议每月检查工具功能是否正常：
```batch
# 执行基本功能测试
fetch-web-content.bat "https://httpbin.org/get" --clean -o "health-check.txt"
```

---

## 最佳实践

### 1. 文件命名规范
- 使用描述性文件名：`streaminghub-group-management.txt`
- 按功能分类：`01-overview.txt`, `02-server.txt`
- 包含获取日期：`magiconion-docs-20250828.txt`

### 2. 内容组织
- 优先获取官方文档而非第三方博客
- 获取示例代码作为参考
- 保存关键API参考页面

### 3. 与团队协作
- 将获取的重要文档整理到`docs/`目录
- 在技术研究记录中引用获取的内容
- 分享有价值的文档链接

### 4. 定期维护
- 定期更新获取的文档（技术更新频繁）
- 清理过时的文档文件
- 更新`examples/`中的URL列表

---

## 未来改进计划

- [ ] 集成自动化脚本，批量获取文档
- [ ] 添加文档版本对比功能
- [ ] 集成到CI/CD流程中
- [ ] 支持更多内容清理选项
- [ ] 添加文档缓存机制

---

## 相关文档
- [网页内容获取工具README](../../tools/web-content-fetcher/README.md) - 详细技术文档
- [MagicOnion文档链接集合](../../tools/web-content-fetcher/examples/magiconion-docs-urls.txt) - URL参考
- [技术研究记录](../../plans/technical-research/current/技术研究记录.md) - 研究成果整理
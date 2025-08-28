# 网页内容获取工具

## 概述
该工具用于获取指定URL的网页内容，主要解决Claude Code无法直接访问外部网页的问题。特别适用于获取技术文档、API参考等内容供AI分析使用。

## 版本信息
- **版本**: v1.0.0
- **创建时间**: 2025-08-28
- **作者**: Wind项目开发组

## 文件说明

### 核心工具
- `fetch-web-content.bat` - Windows批处理版本（推荐）
- `fetch-web-content.ps1` - PowerShell版本（功能更丰富）

### 配套文件
- `README.md` - 本说明文档
- `examples/` - 使用示例目录

## 快速使用

### 基本用法
```bash
# 获取MagicOnion GitHub页面
fetch-web-content.bat https://github.com/Cysharp/MagicOnion

# 获取文档并清理HTML标签
fetch-web-content.bat "https://magiconion.com/docs" --clean -o "magiconion-docs.txt"
```

### PowerShell版本
```powershell
# 基本获取
.\fetch-web-content.ps1 "https://github.com/Cysharp/MagicOnion"

# 高级选项
.\fetch-web-content.ps1 "https://magiconion.com/docs" -OutputFile "docs.txt" -CleanHtml -TimeoutSeconds 60
```

## 命令参数

### 批处理版本 (fetch-web-content.bat)
| 参数 | 说明 | 示例 |
|------|------|------|
| `<URL>` | 要获取的网页URL（必需） | `https://example.com` |
| `-u, --url` | 明确指定URL | `-u "https://example.com"` |
| `-o, --output` | 指定输出文件名 | `-o "output.txt"` |
| `--clean` | 清理HTML标签，输出纯文本 | `--clean` |
| `--timeout` | 设置超时时间（秒） | `--timeout 60` |
| `-h, --help` | 显示帮助信息 | `-h` |

### PowerShell版本 (fetch-web-content.ps1)
| 参数 | 说明 | 示例 |
|------|------|------|
| `Url` | 要获取的网页URL（位置参数） | `"https://example.com"` |
| `-OutputFile` | 指定输出文件名 | `-OutputFile "docs.txt"` |
| `-CleanHtml` | 清理HTML标签 | `-CleanHtml` |
| `-TimeoutSeconds` | 超时时间（秒） | `-TimeoutSeconds 60` |
| `-Help` | 显示帮助信息 | `-Help` |

## 输出格式

工具会在输出文件中包含以下信息：
- URL地址
- 获取时间
- 内容大小
- 处理方式说明
- 实际网页内容

示例输出头部：
```
================================================================
网页内容获取结果
================================================================
URL: https://github.com/Cysharp/MagicOnion
获取时间: 2025-08-28 22:50:15
原始大小: 87234 字节
内容处理: HTML标签已清理
================================================================

[实际内容...]
```

## 使用场景

### 1. 获取技术文档
```bash
# 获取MagicOnion官方文档
fetch-web-content.bat "https://cysharp.github.io/MagicOnion/" --clean -o "magiconion-official-docs.txt"

# 获取Orleans文档
fetch-web-content.bat "https://docs.microsoft.com/en-us/dotnet/orleans/" --clean -o "orleans-docs.txt"
```

### 2. 获取GitHub README
```bash
# 获取项目README
fetch-web-content.bat "https://raw.githubusercontent.com/Cysharp/MagicOnion/master/README.md" -o "magiconion-readme.txt"
```

### 3. 获取API参考
```bash
# 获取API文档（保持HTML格式）
fetch-web-content.bat "https://api-docs.example.com" -o "api-reference.html"
```

## 技术特性

### 批处理版本特性
- 使用curl进行HTTP请求
- 支持重定向和压缩
- 自动添加常见HTTP头部
- 简单的HTML标签清理
- 错误处理和状态报告

### PowerShell版本特性
- 使用.NET HttpClient
- 更好的字符编码处理
- 更精确的HTML清理
- 更详细的错误信息
- 支持更多HTTP特性

### 共同特性
- 自动处理gzip压缩
- 设置合适的User-Agent
- 支持自定义超时时间
- 输出文件自动命名
- 完整的错误处理

## 限制和注意事项

### 网络限制
- 需要能够访问目标网站
- 某些网站可能有反爬虫措施
- 可能受到企业防火墙限制

### 内容处理
- HTML清理功能相对简单
- 不处理JavaScript动态内容
- 对于复杂页面结构可能效果有限

### 使用建议
- 优先使用官方API或RSS源
- 对于大文件建议增加超时时间
- 定期更新获取的文档内容
- 遵守网站的robots.txt规则

## 错误排查

### 常见错误
1. **curl不可用**: 确保curl已安装且在PATH中
2. **连接超时**: 检查网络连接或增加超时时间
3. **权限错误**: 确保有写入当前目录的权限
4. **URL格式错误**: 检查URL是否完整和正确

### 调试步骤
1. 检查网络连接
2. 验证URL是否可访问
3. 尝试增加超时时间
4. 检查输出目录权限
5. 查看详细错误信息

## 与Claude Code集成

### 获取文档后的使用流程
1. 使用工具获取目标网页内容
2. 通过Claude Code的Read工具读取输出文件
3. 让Claude分析和提取有用信息
4. 根据分析结果调整代码实现

### 示例工作流
```bash
# 1. 获取MagicOnion文档
fetch-web-content.bat "https://cysharp.github.io/MagicOnion/api/streaming-hub" --clean -o "streaming-hub-docs.txt"

# 2. 在Claude Code中使用Read工具
# Read streaming-hub-docs.txt

# 3. 让Claude分析并应用到代码修复中
```

## 未来改进计划

- [ ] 添加JavaScript内容处理支持
- [ ] 集成更多网页清理选项
- [ ] 添加批量URL处理功能
- [ ] 支持配置文件
- [ ] 添加缓存机制
- [ ] 集成代理支持

## 许可证
本工具遵循Wind项目的许可证条款。
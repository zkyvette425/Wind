# Redis测试工具包

> **快速参考**: Wind项目Redis测试环境工具集

## 🚀 快速使用

### 三步完成Redis测试：
1. **双击 `start.bat`** → 启动Redis容器
2. **双击 `run-tests.bat`** → 运行Redis测试 
3. **双击 `stop.bat`** → 清理环境（可选）

## 📁 文件说明

| 文件 | 功能 | 适用场景 |
|------|------|----------|
| `start.bat` | 启动Redis测试环境 | 开始测试前 |
| `run-tests.bat` | 运行Redis相关测试 | 执行测试 |
| `stop.bat` | 停止和清理环境 | 测试完成后 |
| `test-docker.bat` | Docker状态诊断工具 | 排查Docker问题 |
| `diagnose-redis.bat` | Redis容器详细诊断 | 排查Redis启动问题 |
| `fix-redis.bat` | Redis问题一键修复 | Redis启动失败时 |
| `docker-compose.test.yml` | Docker容器配置 | 自动化配置 |

## ⚠️ 重要提示

- **执行位置**: 在 `tools/redis-testing/` 目录下执行脚本
- **前提条件**: 需要安装并启动Docker Desktop
- **网络要求**: Redis容器使用端口6379，确保无冲突

## 📖 详细文档

完整使用说明和故障排除请查看: `docs/development/redis-testing.md`

## 🔄 版本信息

- **创建时间**: 2025-08-15
- **适用版本**: Wind v1.2+  
- **Docker镜像**: redis:7.2-alpine
- **依赖**: Docker Desktop, .NET 9

---
*此工具包专为Redis新手设计，无需学习Redis知识即可使用。*
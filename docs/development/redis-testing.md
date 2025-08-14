# Redis测试环境使用指南

> **文档版本**: v1.0.0  
> **创建时间**: 2025-08-15  
> **适用项目版本**: Wind v1.2+  
> **关联配置**: `tools/redis-testing/docker-compose.test.yml`, `Wind.Server/appsettings.json`  
> **最后更新**: 2025-08-15  

---

## 📋 版本变更历史

| 版本 | 日期 | 变更内容 | 影响范围 |
|------|------|----------|----------|
| v1.0.0 | 2025-08-15 | 创建Redis测试环境文档和工具包 | 新增功能 |

---

> **适用人群**: Redis新手，需要运行Wind项目中的Redis相关测试  
> **前提条件**: 已安装Docker Desktop  
> **预计用时**: 5分钟设置，2分钟日常使用  

## 🚀 快速开始（3步完成）

### 第1步：启动Redis环境
```bash
# 进入工具目录并执行：
cd tools/redis-testing
.\start.bat
```
**预期结果**: 看到"✅ Redis测试环境启动成功！"

### 第2步：运行Redis测试
```bash
# 在tools/redis-testing目录执行：
run-tests.bat
```
**预期结果**: 所有Redis测试通过，显示绿色✅标记

### 第3步：停止环境（可选）
```bash
# 测试完成后清理：
stop.bat
```

## 📋 详细操作指南

### 🎯 测试类型说明

我们的项目包含3类Redis相关测试：

#### 1. **集成测试** (需要真实Redis)
- `RedisStorageValidationTests` - 验证Orleans的Redis存储功能
- `RedisCacheStrategyTests` - 验证Redis缓存和TTL策略
- **标记**: `[Trait("Category", "Integration")]`
- **运行命令**: `dotnet test --filter Category=Integration`

#### 2. **单元测试** (无需Redis)
- `RedisCacheStrategyMockTests` - Mock测试，验证业务逻辑
- **标记**: `[Trait("Category", "Unit")]`
- **运行命令**: `dotnet test --filter Category=Unit`

#### 3. **完整测试套件**
- 包含所有测试（集成+单元+其他）
- **运行命令**: `dotnet test Wind.sln`

### 🔧 环境配置详情

#### Docker容器配置
- **Redis版本**: 7.2-alpine (轻量级，快速启动)
- **端口映射**: 6379:6379 (标准Redis端口)
- **密码**: windgame123 (与项目配置一致)
- **数据库**: 支持0-15，测试使用0,1,2
- **内存限制**: 256MB (测试够用)
- **数据持久化**: 启用AOF和RDB双重保障

#### 健康检查
- **检查间隔**: 30秒
- **超时时间**: 10秒
- **重试次数**: 3次
- **启动等待**: 30秒

## 🧪 测试选项详解

运行`run-tests.bat`时会看到5个选项：

### 选项1: 运行所有Redis集成测试 ⭐ 推荐
```bash
dotnet test Wind.sln --filter "Category=Integration"
```
- **包含**: 所有需要Redis的测试
- **用途**: 验证Redis功能是否正常
- **时间**: 约30-60秒

### 选项2: 运行Redis存储验证测试
```bash
dotnet test --filter "FullyQualifiedName~RedisStorageValidationTests"
```
- **包含**: Orleans Redis存储功能
- **测试**: 玩家登录、状态持久化、跨Grain生命周期
- **用途**: 验证Orleans配置是否使用Redis存储

### 选项3: 运行Redis缓存策略测试
```bash
dotnet test --filter "FullyQualifiedName~RedisCacheStrategyTests"
```
- **包含**: TTL策略、缓存性能、批量操作
- **测试**: 不同数据类型的过期时间、缓存统计
- **用途**: 验证缓存功能和性能

### 选项4: 运行Redis Mock测试 ⚡ 最快
```bash
dotnet test --filter "FullyQualifiedName~RedisCacheStrategyMockTests"
```
- **包含**: 纯逻辑测试，无Redis依赖
- **测试**: TTL映射、配置验证、序列化
- **用途**: 快速验证业务逻辑正确性
- **优势**: 无需启动Redis，秒级完成

### 选项5: 运行完整测试套件
```bash
dotnet test Wind.sln
```
- **包含**: 项目中所有测试
- **用途**: 完整回归测试
- **时间**: 约2-5分钟

## 🔍 故障排除

### 常见问题1: Docker未启动
**错误信息**: "❌ 错误: 未检测到Docker Desktop" 或 "Docker Desktop未完全启动"

**解决方案**:
1. 启动Docker Desktop应用程序
2. 等待Docker完全启动（系统托盘图标变为绿色）
3. 如果持续出现问题，运行 `test-docker.bat` 进行详细检查
4. 重新运行`start.bat`

**诊断工具**:
```bash
# 运行Docker状态测试
cd tools/redis-testing
test-docker.bat
```

### 常见问题2: 端口被占用
**错误信息**: "端口6379被占用" 或 容器启动失败

**解决方案**:
```bash
# 1. 检查端口占用
netstat -an | findstr :6379

# 2. 停止占用进程，或修改配置使用其他端口
# 3. 重新启动Redis容器
stop.bat
start.bat
```

### 常见问题3: Redis连接失败/启动超时
**错误信息**: "❌ 错误: Redis服务启动超时" 或 "无法连接到Redis服务"

**一键修复方案**:
```bash
# 运行自动修复工具
fix-redis.bat
```

**手动诊断方案**:
```bash
# 1. 详细诊断Redis状态
diagnose-redis.bat

# 2. 检查容器状态
docker ps | findstr wind-redis

# 3. 查看Redis日志
docker logs wind-redis-test

# 4. 手动测试连接
docker exec -it wind-redis-test redis-cli -a windgame123 ping
```

**预期结果**: 返回 `PONG`

**常见原因和解决方案**:
- **端口占用**: 检查端口6379是否被占用
- **权限问题**: 以管理员身份运行脚本
- **资源不足**: 确保Docker有足够内存和CPU
- **配置冲突**: 运行fix-redis.bat清理并重建

### 常见问题4: 测试超时
**错误信息**: 测试运行时间过长或超时

**解决方案**:
1. **网络问题**: 检查Docker网络连接
2. **资源不足**: 关闭其他占用资源的应用
3. **容器状态**: 重启Redis容器
```bash
stop.bat
start.bat
```

### 常见问题5: 权限问题
**错误信息**: "访问被拒绝" 或 权限相关错误

**解决方案**:
1. **以管理员身份运行**: 右键脚本 → "以管理员身份运行"
2. **Docker权限**: 确保当前用户在Docker用户组中
3. **防火墙设置**: 确保Docker可以访问网络

## 💡 高级使用技巧

### 🎨 Redis管理界面（可选）
如需图形化管理Redis数据：

```bash
# 启动Redis Commander管理界面
docker-compose -f docker-compose.test.yml --profile debug up -d

# 访问地址
http://localhost:8081
```

**功能**: 
- 可视化查看Redis数据
- 实时监控内存使用
- 手动操作键值对

### 📊 实时监控Redis状态
```bash
# 查看Redis信息
docker exec wind-redis-test redis-cli -a windgame123 info

# 监控实时命令
docker exec wind-redis-test redis-cli -a windgame123 monitor

# 查看内存使用
docker exec wind-redis-test redis-cli -a windgame123 info memory
```

### 🧹 存储管理
```bash
# 查看所有键
docker exec wind-redis-test redis-cli -a windgame123 keys "*"

# 清空测试数据（小心！）
docker exec wind-redis-test redis-cli -a windgame123 flushall

# 检查特定数据库
docker exec wind-redis-test redis-cli -a windgame123 -n 0 dbsize
```

### ⚙️ 自定义配置

如需修改Redis配置，编辑`docker-compose.test.yml`:

```yaml
# 常用配置项
command: >
  redis-server 
  --requirepass windgame123     # 密码
  --maxmemory 512mb            # 内存限制
  --port 6379                  # 端口
  --databases 16               # 数据库数量
```

修改后重启环境：
```bash
stop.bat
start.bat
```

## 📈 性能基准

### 预期测试性能
- **Mock测试**: < 1秒（无Redis依赖）
- **集成测试**: 30-60秒（包含容器启动）
- **完整套件**: 2-5分钟（所有测试）

### 容器资源使用
- **内存**: ~50MB（Redis） + ~20MB（容器开销）
- **磁盘**: ~100MB（镜像） + ~10MB（数据）
- **启动时间**: 15-30秒（首次较慢）

### 优化建议
1. **保持容器运行**: 避免频繁启停容器
2. **选择性测试**: 日常开发使用Mock测试
3. **定期清理**: 使用完全清理选项释放空间

## 🚀 CI/CD集成

### GitHub Actions示例
```yaml
- name: Start Redis for tests
  run: docker-compose -f docker-compose.test.yml up -d redis-test
  
- name: Wait for Redis
  run: |
    timeout 30 bash -c 'until docker exec wind-redis-test redis-cli -a windgame123 ping; do sleep 1; done'
    
- name: Run Redis tests
  run: dotnet test --filter Category=Integration
  
- name: Cleanup
  run: docker-compose -f docker-compose.test.yml down --volumes
```

## ❓ 常见疑问解答

**Q: 我需要学习Redis知识吗？**
A: 不需要！这套环境专门为新手设计，双击脚本即可使用。

**Q: Docker容器会占用很多资源吗？**
A: 不会。Redis容器仅使用约70MB内存，对系统影响很小。

**Q: 可以同时运行多个测试吗？**
A: 可以。每个容器使用独立的端口和数据库，互不干扰。

**Q: 测试数据会影响生产环境吗？**
A: 不会。测试使用独立的Docker容器和数据库，完全隔离。

**Q: 如何确认Redis确实在工作？**
A: 运行集成测试，检查容器日志，或使用Redis Commander管理界面。

## 🔄 版本兼容性

- **Docker Desktop**: 要求 4.0+ 
- **Redis**: 使用 7.2-alpine（LTS版本）
- **.NET**: 兼容 .NET 9
- **Orleans**: 兼容 9.2.1
- **Windows**: 支持 Windows 10/11

## 📞 获取帮助

遇到问题？按优先级排序：

1. **查看本文档** 的故障排除部分
2. **检查容器日志**: `docker logs wind-redis-test`
3. **重启环境**: 运行`stop.bat`再运行`start.bat`
4. **完全重置**: 在停止脚本中选择"完全清理"
5. **检查系统**: 确保Docker Desktop正常运行

---

**✨ 现在你已经掌握了完整的Redis测试环境！开始你的测试之旅吧！**

> 💡 **小贴士**: 将本文档加入收藏夹，以便随时查阅参考。
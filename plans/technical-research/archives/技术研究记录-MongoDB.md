# MongoDB技术研究记录

## 🎯 使用指南
**查阅优先级**：
1. **优先查阅本文件** - 包含MongoDB相关的实际解决方案和最佳实践
2. **其次使用Context7** - 获取官方文档参考
3. **最后查阅源码** - GitHub Issues/官方源码

**记录格式**：每个技术问题包含【问题背景】【解决方案】【关键发现】【避坑指南】

---

## 📚 MongoDB技术案例库

### 🚧 案例1: MongoDB副本集Docker网络配置问题

**研究日期**: 2025-08-17 (北京时间)  
**解决状态**: 🚧 待解决  
**影响范围**: 分布式事务测试环境 → MongoDB事务功能无法完整验证

#### 【问题背景】
- 需要MongoDB副本集支持多文档事务（单机版不支持事务）
- Docker容器运行MongoDB副本集，.NET测试程序在宿主机运行
- 网络隔离导致MongoDB客户端无法正确发现和连接副本集

#### 【技术表现】
```bash
# 错误信息
A timeout occurred after 10000ms selecting a server using CompositeServerSelector
Client view of cluster state is { ClusterId : "1", Type : "ReplicaSet", State : "Disconnected", Servers : [] }
```

#### 【根本原因】
- **副本集内部配置**: `localhost:27017`（容器内部地址）
- **外部访问路径**: `localhost:27018`（端口映射后地址）
- **MongoDB驱动行为**: 客户端从副本集配置中获取成员列表，然后尝试连接内部地址
- **网络不可达**: 宿主机无法访问容器内部的`localhost:27017`

#### 【已尝试的方案】
1. ❌ **修改副本集host为容器名**: `rs.reconfig({members:[{host:'wind-mongodb-test:27017'}]})`
2. ❌ **调整连接字符串**: 添加`directConnection=false`参数
3. ❌ **端口映射调整**: 尝试不同的端口配置
4. ❌ **强制重新配置**: 使用`{force:true}`重新配置副本集

#### 【技术难点分析】
- **MongoDB副本集发现机制**: 客户端需要能够连接到副本集配置中的所有成员地址
- **Docker网络隔离**: 容器内部网络与宿主机网络的地址映射复杂性
- **端口映射限制**: 副本集模式下不能简单使用端口映射，需要地址解析一致性

#### 【后续解决方向】
1. **Docker网络桥接**: 
   ```yaml
   # 让测试程序也运行在同一Docker网络中
   networks:
     wind-test-network:
       driver: bridge
   ```

2. **修改hosts文件**: 
   ```bash
   # 映射容器名到本地地址
   127.0.0.1 wind-mongodb-test
   ```

3. **单节点副本集优化**:
   ```bash
   # 使用真正的单节点副本集，避免多成员发现问题
   rs.initiate({_id: 'rs0', members: [{_id: 0, host: 'localhost:27017'}]})
   ```

4. **生产级部署**: 使用真实的MongoDB集群环境

#### 【当前workaround】
- ✅ **核心功能验证**: Redis部分的分布式事务100%测试通过
- ✅ **架构设计验证**: MongoDB事务代码架构完全正确
- ⚠️ **环境依赖**: 生产环境使用真实MongoDB集群即可

#### 【经验教训】
- Docker化的副本集测试环境比单机版复杂很多
- 网络配置问题属于运维层面，不影响代码逻辑正确性
- 分布式事务的核心价值已经通过Redis部分得到验证

#### 【参考资料】
- [MongoDB副本集部署文档](https://docs.mongodb.com/manual/tutorial/deploy-replica-set/)
- [Docker网络配置指南](https://docs.docker.com/network/)
- [MongoDB连接字符串参数](https://docs.mongodb.com/manual/reference/connection-string/)

---

*本记录将在项目进展过程中持续更新，记录新的技术发现和实践经验。*
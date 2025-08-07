# 游戏服务器项目

这是一个基于.NET 9和ASP.NET Core的游戏服务器项目，使用SignalR实现实时通信。

## 项目结构
- `Program.cs`: 应用程序入口，配置服务和中间件
- `GameHub.cs`: SignalR hub，处理实时游戏通信逻辑

## 推荐库
以下是一些推荐的库，可以根据需要添加到项目中：

1. **数据库访问**
   - `MongoDB.Driver`: 适用于MongoDB数据库
   - `Microsoft.EntityFrameworkCore`: 适用于关系型数据库

2. **身份验证与授权**
   - `Microsoft.AspNetCore.Authentication.JwtBearer`: JWT身份验证

3. **日志记录**
   - `Serilog.AspNetCore`: 结构化日志记录
   - `NLog.Web.AspNetCore`: 灵活的日志记录框架

4. **数据验证**
   - `FluentValidation.AspNetCore`: 流畅的验证API

5. **对象映射**
   - `AutoMapper.Extensions.Microsoft.DependencyInjection`: 对象映射工具

6. **消息传递**
   - `MediatR`: 实现 mediator 模式，解耦应用程序组件

7. **性能监控**
   - `Prometheus.Client.AspNetCore`: Prometheus监控集成

## 如何运行
1. 进入项目目录: `cd GameServer`
2. 运行服务器: `dotnet run`
3. 服务器将在默认端口(通常是5000/5001)启动

## 扩展建议
- 添加游戏状态管理
- 实现房间/匹配系统
- 添加碰撞检测逻辑
- 实现持久化存储
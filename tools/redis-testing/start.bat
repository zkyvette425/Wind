@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
title Wind游戏服务器 - 启动Redis测试环境

echo.
echo ==========================================
echo   Wind游戏服务器 - Redis测试环境启动
echo ==========================================
echo.

rem 检查Docker是否运行
echo [1/5] 检查Docker Desktop状态...
docker --version >nul 2>&1
if errorlevel 1 (
    echo ❌ 错误: 未检测到Docker Desktop，请先启动Docker Desktop
    echo 💡 提示: 确保Docker Desktop已启动并正在运行
    pause
    exit /b 1
)

docker info >nul 2>&1
if errorlevel 1 (
    echo ❌ 错误: Docker Desktop未完全启动
    echo 💡 提示: 请等待Docker Desktop完全启动后再试
    echo.
    echo 🔧 可能的解决方案:
    echo    1. 等待Docker Desktop系统托盘图标变绿
    echo    2. 重启Docker Desktop应用程序
    echo    3. 检查Windows虚拟化是否启用
    pause
    exit /b 1
)
echo ✅ Docker Desktop运行正常

rem 检查docker-compose文件
echo.
echo [2/5] 检查配置文件...
if not exist "docker-compose.test.yml" (
    echo ❌ 错误: 找不到docker-compose.test.yml配置文件
    echo 💡 提示: 请确保在tools/redis-testing目录执行此脚本
    pause
    exit /b 1
)
echo ✅ 配置文件检查通过

rem 清理可能存在的旧容器
echo.
echo [3/5] 清理旧的测试容器...
docker-compose -f docker-compose.test.yml down --volumes --remove-orphans >nul 2>&1
echo ✅ 旧容器清理完成

rem 启动Redis测试容器
echo.
echo [4/5] 启动Redis测试容器...
echo 📦 正在拉取Redis镜像并启动容器...
docker-compose -f docker-compose.test.yml up -d redis-test
if errorlevel 1 (
    echo ❌ 错误: Redis容器启动失败
    echo 💡 请检查Docker日志: docker-compose -f docker-compose.test.yml logs redis-test
    pause
    exit /b 1
)

rem 等待Redis容器健康检查通过
echo.
echo [5/5] 等待Redis服务就绪...
echo 🕐 正在检查Redis连接状态...

set /a count=0
:wait_redis
docker exec wind-redis-test redis-cli -a windgame123 ping 2>nul | findstr "PONG" >nul
if errorlevel 1 (
    set /a count+=1
    if !count! gtr 60 (
        echo ❌ 错误: Redis服务启动超时（60秒）
        echo.
        echo 🔍 正在显示容器状态和日志...
        docker ps --filter "name=wind-redis-test"
        echo.
        echo --- Redis容器日志 ---
        docker logs --tail 10 wind-redis-test
        echo.
        echo 💡 排查建议:
        echo    1. 运行 diagnose-redis.bat 进行详细诊断
        echo    2. 检查端口6379是否被占用: netstat -an ^| findstr :6379
        echo    3. 重启容器: docker-compose -f docker-compose.test.yml restart redis-test
        pause
        exit /b 1
    )
    if !count! leq 10 (
        echo    等待Redis启动... (!count!/60秒^) - 容器初始化中
    ) else if !count! leq 30 (
        echo    等待Redis启动... (!count!/60秒^) - Redis服务启动中
    ) else (
        echo    等待Redis启动... (!count!/60秒^) - 检查是否存在问题
    )
    timeout /t 1 /nobreak >nul
    goto wait_redis
)

echo.
echo ==========================================
echo ✅ Redis测试环境启动成功！
echo ==========================================
echo.
echo 📋 环境信息:
echo    Redis地址: localhost:6379
echo    密码: windgame123
echo    支持数据库: 0-15 (测试使用 0,1,2)
echo    容器名称: wind-redis-test
echo.
echo 📊 管理界面 (可选):
echo    如需图形管理界面，运行: docker-compose -f docker-compose.test.yml --profile debug up -d
echo    管理地址: http://localhost:8081
echo.
echo 🧪 下一步操作:
echo    1. 双击运行 run-redis-tests.bat 执行Redis测试
echo    2. 或手动运行: dotnet test Wind.sln --filter Category=Integration
echo.
echo ⚠️  注意: 测试完成后请运行 stop-redis-test.bat 清理容器
echo.

rem 显示容器状态
echo 🐳 容器状态:
docker-compose -f docker-compose.test.yml ps

echo.
echo ✨ Redis测试环境已就绪，可以开始测试了！
echo.
pause
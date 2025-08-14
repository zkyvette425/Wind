@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
title Wind游戏服务器 - 停止Redis测试环境

echo.
echo ==========================================
echo   Wind游戏服务器 - 停止Redis测试环境
echo ==========================================
echo.

rem 检查Docker是否运行
echo [1/4] 检查Docker状态...
docker --version >nul 2>&1
if errorlevel 1 (
    echo ❌ 错误: 未检测到Docker Desktop
    echo 💡 如果Docker已关闭，Redis容器已自动停止
    pause
    exit /b 0
)
echo ✅ Docker运行正常

rem 检查Redis测试容器状态
echo.
echo [2/4] 检查Redis测试容器状态...
docker ps -a --filter "name=wind-redis-test" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo.

rem 停止Redis测试服务
echo [3/4] 停止Redis测试服务...
docker-compose -f docker-compose.test.yml down
if errorlevel 1 (
    echo ⚠️  警告: 停止服务时遇到问题，尝试强制停止...
    docker stop wind-redis-test >nul 2>&1
    docker stop wind-redis-commander >nul 2>&1
) else (
    echo ✅ Redis测试服务已停止
)

rem 清理资源（提供选项）
echo.
echo [4/4] 清理测试资源...
echo.
echo 🗑️ 清理选项:
echo    1. 仅停止容器 (保留数据和镜像)
echo    2. 停止并删除容器 (保留数据卷和镜像)
echo    3. 完全清理 (删除容器、数据卷、镜像)
echo    4. 跳过清理
echo.
set /p cleanup_choice="请选择清理级别 (1-4): "

if "%cleanup_choice%"=="1" goto cleanup_done
if "%cleanup_choice%"=="2" goto cleanup_containers
if "%cleanup_choice%"=="3" goto cleanup_all
if "%cleanup_choice%"=="4" goto cleanup_done
echo ❌ 无效选择，执行默认清理
goto cleanup_containers

:cleanup_containers
echo 🔄 删除Redis测试容器...
docker-compose -f docker-compose.test.yml down --remove-orphans
docker rm -f wind-redis-test >nul 2>&1
docker rm -f wind-redis-commander >nul 2>&1
echo ✅ 容器已删除
goto cleanup_done

:cleanup_all
echo 🔄 执行完全清理...
echo    - 停止并删除容器
docker-compose -f docker-compose.test.yml down --volumes --remove-orphans --rmi local
echo    - 删除相关Docker网络
docker network rm wind-test-network >nul 2>&1
echo    - 清理未使用的Docker资源
docker system prune -f >nul 2>&1
echo ✅ 完全清理完成

rem 显示清理后的状态
echo.
echo 📊 清理结果:
docker images | findstr redis | findstr wind >nul 2>&1 && echo "    Redis镜像: 已保留" || echo "    Redis镜像: 已删除"
docker volume ls | findstr wind-redis >nul 2>&1 && echo "    数据卷: 已保留" || echo "    数据卷: 已删除"
docker network ls | findstr wind-test >nul 2>&1 && echo "    测试网络: 已保留" || echo "    测试网络: 已删除"
goto cleanup_done

:cleanup_done
echo.
echo ==========================================
echo ✅ Redis测试环境已停止！
echo ==========================================
echo.

rem 验证清理结果
echo 🔍 验证清理结果:
docker ps --filter "name=wind-redis" --format "table {{.Names}}\t{{.Status}}" 2>nul | findstr wind-redis >nul 2>&1
if errorlevel 1 (
    echo ✅ 所有Redis测试容器已停止
) else (
    echo ⚠️  警告: 仍有Redis容器在运行
    docker ps --filter "name=wind-redis"
)

echo.
echo 💾 数据保护说明:
if "%cleanup_choice%"=="3" (
    echo    ❌ 测试数据已完全清理
    echo    💡 下次测试将使用全新的Redis环境
) else (
    echo    ✅ 测试数据已保留
    echo    💡 下次启动将恢复之前的测试数据
)

echo.
echo 📋 后续操作:
echo    - 重新开始测试: 双击 start-redis-test.bat
echo    - 查看测试脚本: 双击 run-redis-tests.bat  
echo    - 完全重置环境: 重新运行此脚本并选择"完全清理"
echo.

rem 显示系统资源状态
echo 💻 系统资源状态:
docker system df 2>nul | findstr "RECLAIMABLE" >nul 2>&1
if not errorlevel 1 (
    echo.
    echo 🧹 Docker磁盘使用情况:
    docker system df
    echo.
    echo 💡 提示: 如需释放更多空间，可运行: docker system prune -a
)

echo.
echo ✨ Redis测试环境清理完成！
echo.
pause
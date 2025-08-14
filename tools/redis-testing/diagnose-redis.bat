@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
title Redis容器诊断工具

echo.
echo ==========================================
echo   Redis容器诊断工具
echo ==========================================
echo.

echo [1/6] 检查容器状态...
docker ps -a --filter "name=wind-redis-test" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

echo.
echo [2/6] 检查容器日志...
echo --- 最近20行日志 ---
docker logs --tail 20 wind-redis-test 2>&1

echo.
echo [3/6] 尝试进入容器...
docker exec wind-redis-test echo "容器可访问" 2>&1
if errorlevel 1 (
    echo ❌ 无法进入容器
) else (
    echo ✅ 容器可访问
)

echo.
echo [4/6] 测试Redis进程...
docker exec wind-redis-test ps aux | findstr redis 2>&1

echo.
echo [5/6] 测试Redis端口...
docker exec wind-redis-test netstat -ln | findstr 6379 2>&1

echo.
echo [6/6] 测试Redis连接（无密码）...
docker exec wind-redis-test redis-cli ping 2>&1

echo.
echo [6/6] 测试Redis连接（有密码）...
docker exec wind-redis-test redis-cli -a windgame123 ping 2>&1

echo.
echo ==========================================
echo 诊断完成
echo ==========================================

pause
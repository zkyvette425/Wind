@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
title Redis问题修复工具

echo.
echo ==========================================
echo   Redis问题修复工具
echo ==========================================
echo.

echo [1/4] 停止现有Redis容器...
docker-compose -f docker-compose.test.yml down --volumes

echo.
echo [2/4] 清理Docker网络和卷...
docker network prune -f >nul 2>&1
docker volume prune -f >nul 2>&1

echo.
echo [3/4] 重新启动Redis容器...
docker-compose -f docker-compose.test.yml up -d redis-test

echo.
echo [4/4] 等待Redis就绪（最多30秒）...
set /a count=0
:wait_fix
docker exec wind-redis-test redis-cli -a windgame123 ping 2>nul | findstr "PONG" >nul
if errorlevel 1 (
    set /a count+=1
    if !count! gtr 30 (
        echo ❌ 修复失败，请手动检查
        echo.
        echo 💡 手动诊断步骤:
        echo    1. 运行 diagnose-redis.bat
        echo    2. 检查系统资源是否充足
        echo    3. 重启Docker Desktop
        pause
        exit /b 1
    )
    echo    等待中... (!count!/30秒)
    timeout /t 1 /nobreak >nul
    goto wait_fix
)

echo.
echo ✅ Redis修复成功！现在可以运行测试了。
echo.
pause
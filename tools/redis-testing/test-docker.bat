@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
title Docker状态测试

echo.
echo ==========================================
echo   Docker状态测试工具
echo ==========================================
echo.

echo [1/3] 检查Docker命令...
docker --version
if errorlevel 1 (
    echo ❌ Docker命令不可用
    pause
    exit /b 1
) else (
    echo ✅ Docker命令可用
)

echo.
echo [2/3] 检查Docker守护进程...
docker info >nul 2>&1
if errorlevel 1 (
    echo ❌ Docker守护进程未运行
    echo.
    echo 💡 请启动Docker Desktop并等待完全启动
    echo    - 检查系统托盘Docker图标是否为绿色
    echo    - 如果图标为橙色或红色，请等待启动完成
    echo    - 可以重启Docker Desktop尝试解决
) else (
    echo ✅ Docker守护进程正常运行
)

echo.
echo [3/3] 检查Docker Compose...
docker-compose --version
if errorlevel 1 (
    echo ❌ Docker Compose不可用
) else (
    echo ✅ Docker Compose可用
)

echo.
echo ==========================================
echo Docker状态检查完成
echo ==========================================
echo.

pause
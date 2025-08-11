@echo off
echo 🚀 Orleans 端到端测试脚本
echo.

echo 📋 第一步: 清理环境
taskkill /F /IM Wind.Server.exe 2>nul
timeout /T 2 /NOBREAK >nul

echo 🔨 第二步: 构建解决方案
cd /d "%~dp0"
dotnet build Wind.sln
if %ERRORLEVEL% neq 0 (
    echo ❌ 构建失败
    pause
    exit /b 1
)

echo 🎯 第三步: 启动Orleans Silo (后台)
start "Orleans Silo" /MIN cmd /c "cd Wind.Server && dotnet run"
echo ⏳ 等待Silo启动...
timeout /T 8 /NOBREAK >nul

echo 🧪 第四步: 运行客户端测试
cd Wind.Client
dotnet run SimpleOrleansTest
set TEST_RESULT=%ERRORLEVEL%

echo.
echo 🧹 第五步: 清理环境
taskkill /F /IM Wind.Server.exe 2>nul

if %TEST_RESULT% equ 0 (
    echo.
    echo ✅ Orleans 端到端测试成功!
) else (
    echo.
    echo ❌ Orleans 端到端测试失败!
)

pause
exit /b %TEST_RESULT%
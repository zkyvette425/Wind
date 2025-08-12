@echo off
REM Wind游戏服务器 - 一键启动开发环境脚本

echo ==========================================
echo Wind游戏服务器 v1.3 - 开发环境启动脚本
echo ==========================================
echo.

REM 检查Docker是否运行
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ 错误: Docker未运行，请先启动Docker Desktop
    echo.
    pause
    exit /b 1
)

echo ✅ Docker运行状态正常

REM 启动基础服务
echo.
echo 🚀 启动基础服务 (Redis, MongoDB, Seq, Jaeger)...
docker-compose up -d

REM 等待服务启动
echo.
echo ⏳ 等待服务启动完成...
timeout /t 10 /nobreak >nul

REM 检查服务状态
echo.
echo 📊 检查服务状态:
docker-compose ps

REM 构建解决方案
echo.
echo 🔨 构建Wind解决方案...
dotnet build Wind.sln
if %errorlevel% neq 0 (
    echo ❌ 解决方案构建失败
    pause
    exit /b 1
)

echo ✅ 解决方案构建成功

REM 运行基础测试
echo.
echo 🧪 运行基础Orleans测试...
dotnet test "Wind.Tests\Wind.Tests.csproj" --filter "FullyQualifiedName~BasicGrainTests" --logger console --verbosity quiet
if %errorlevel% neq 0 (
    echo ⚠️  基础测试失败，但继续启动服务器
) else (
    echo ✅ 基础测试通过
)

echo.
echo 🎮 启动Wind游戏服务器...
echo 按 Ctrl+C 可以停止服务器
echo.

REM 启动Orleans服务器
dotnet run --project "Wind.Server\Wind.Server.csproj"

echo.
echo 服务器已停止
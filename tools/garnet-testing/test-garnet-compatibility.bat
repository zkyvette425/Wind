@echo off
echo Testing Garnet and Redis compatibility...
echo.

REM 进入项目目录
cd /d "%~dp0..\.."

REM 检查服务是否运行
echo Checking Redis connection...
docker exec wind-redis-test redis-cli -a windgame123 ping >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Redis is not running. Please start services first.
    echo Run: tools\garnet-testing\start-garnet-test.bat
    pause
    exit /b 1
)

echo Checking Garnet connection...
docker run --rm --network wind-test-network redis:7.2-alpine redis-cli -h garnet-test -p 6379 -a windgame123 ping >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Garnet is not running. Please start services first.
    echo Run: tools\garnet-testing\start-garnet-test.bat
    pause
    exit /b 1
)

echo [SUCCESS] Both services are running
echo.

REM 运行兼容性测试
echo Running Garnet compatibility tests...
dotnet test Wind.Tests\Wind.Tests.csproj --filter "Category=GarnetCompatibility" --logger "console;verbosity=normal"

if %errorlevel% equ 0 (
    echo.
    echo [SUCCESS] All compatibility tests passed!
) else (
    echo.
    echo [WARNING] Some compatibility tests failed. Check output above.
)

echo.
pause
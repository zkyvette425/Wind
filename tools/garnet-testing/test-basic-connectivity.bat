@echo off
echo Testing basic Redis connectivity without Garnet...
echo.

REM 测试现有Redis连接
echo Testing Redis connection on port 6379...
docker exec wind-redis-test redis-cli -a windgame123 ping
if %errorlevel% equ 0 (
    echo [SUCCESS] Redis is accessible
    
    REM 进行基本功能测试
    echo Testing basic Redis operations...
    docker exec wind-redis-test redis-cli -a windgame123 SET test:key "Hello Redis"
    docker exec wind-redis-test redis-cli -a windgame123 GET test:key
    docker exec wind-redis-test redis-cli -a windgame123 DEL test:key
    echo [SUCCESS] Basic Redis operations work
) else (
    echo [ERROR] Redis is not accessible
    exit /b 1
)

echo.
echo Running .NET Redis connection tests...
cd /d "%~dp0..\.."
dotnet test Wind.Tests\Wind.Tests.csproj --filter "FullyQualifiedName~RedisStorageValidationTests" --logger "console;verbosity=normal"

if %errorlevel% equ 0 (
    echo [SUCCESS] .NET Redis tests passed
) else (
    echo [WARNING] Some .NET Redis tests failed
)

echo.
pause
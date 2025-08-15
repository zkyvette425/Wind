@echo off
echo Starting Garnet and Redis test environments...
echo.

REM 进入正确的目录
cd /d "%~dp0"

REM 检查Docker是否运行
docker version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Docker is not running or not installed
    echo Please start Docker Desktop first
    pause
    exit /b 1
)

REM 停止可能存在的容器
echo Stopping existing containers...
docker-compose -f docker-compose.garnet.yml down >nul 2>&1

REM 启动服务
echo Starting Redis on port 6379...
echo Starting Garnet on port 6380...
docker-compose -f docker-compose.garnet.yml up -d

REM 等待服务启动
echo.
echo Waiting for services to start...
timeout /t 10 /nobreak >nul

REM 检查服务状态
echo.
echo Checking service status...
docker-compose -f docker-compose.garnet.yml ps

REM 测试连接
echo.
echo Testing Redis connection (port 6379)...
docker exec wind-redis-test redis-cli -a windgame123 ping
if %errorlevel% equ 0 (
    echo [SUCCESS] Redis is running and accessible
) else (
    echo [WARNING] Redis connection test failed
)

echo.
echo Testing Garnet connection (port 6380)...
docker run --rm --network wind-test-network redis:7.2-alpine redis-cli -h garnet-test -p 6379 -a windgame123 ping
if %errorlevel% equ 0 (
    echo [SUCCESS] Garnet is running and accessible
) else (
    echo [WARNING] Garnet connection test failed
)

echo.
echo Test environment is ready!
echo - Redis: localhost:6379 (password: windgame123)
echo - Garnet: localhost:6380 (password: windgame123)
echo.
pause
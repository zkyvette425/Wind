@echo off
echo Stopping Garnet and Redis test environments...
echo.

REM 进入正确的目录
cd /d "%~dp0"

REM 停止服务
echo Stopping all services...
docker-compose -f docker-compose.garnet.yml down

REM 显示状态
echo.
echo Services stopped. Containers and networks removed.
echo Data volumes are preserved for next test.
echo.
pause
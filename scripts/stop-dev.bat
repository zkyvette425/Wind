@echo off
REM Wind游戏服务器 - 停止开发环境脚本

echo ==========================================
echo Wind游戏服务器 - 停止开发环境
echo ==========================================
echo.

echo 🛑 停止所有Docker服务...
docker-compose down

echo.
echo ✅ 开发环境已完全停止

echo.
echo 💾 如需清理所有数据，请运行:
echo    docker-compose down -v
echo    docker system prune -f

pause
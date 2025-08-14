@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
title Wind游戏服务器 - 运行Redis测试

echo.
echo ==========================================
echo   Wind游戏服务器 - Redis功能测试
echo ==========================================
echo.

rem 检查Redis容器是否运行
echo [1/4] 检查Redis测试环境...
docker ps --filter "name=wind-redis-test" --filter "status=running" | findstr "wind-redis-test" >nul
if errorlevel 1 (
    echo ❌ 错误: Redis测试容器未运行
    echo 💡 请先运行 start.bat 启动Redis环境
    echo.
    set /p choice="是否现在启动Redis环境？(Y/N): "
    if /i "!choice!"=="Y" (
        echo 🚀 正在启动Redis环境...
        call start.bat
        if errorlevel 1 (
            echo ❌ Redis环境启动失败
            pause
            exit /b 1
        )
    ) else (
        echo ⏹️ 用户取消，退出测试
        pause
        exit /b 1
    )
)
echo ✅ Redis环境运行正常

rem 检查Redis连接
echo.
echo [2/4] 验证Redis连接...
docker exec wind-redis-test redis-cli -a windgame123 ping 2>nul | findstr "PONG" >nul
if errorlevel 1 (
    echo ❌ 错误: 无法连接到Redis服务
    echo 💡 Redis容器可能正在启动中，请等待几秒后重试
    pause
    exit /b 1
)
echo ✅ Redis连接测试通过

rem 检查.NET项目
echo.
echo [3/4] 检查测试项目...
if not exist "../../Wind.sln" (
    echo ❌ 错误: 找不到Wind.sln解决方案文件
    echo 💡 请确保项目结构完整
    pause
    exit /b 1
)
echo ✅ 测试项目检查通过

rem 运行测试
echo.
echo [4/4] 运行Redis相关测试...
echo ==========================================
echo.

rem 显示测试选项菜单
echo 🧪 测试选项:
echo    1. 运行所有Redis集成测试 (推荐)
echo    2. 运行Redis存储验证测试
echo    3. 运行Redis缓存策略测试
echo    4. 运行Redis Mock测试 (无需Redis)
echo    5. 运行完整测试套件
echo.
set /p test_choice="请选择测试类型 (1-5): "

rem 根据选择运行相应测试
if "%test_choice%"=="1" goto run_integration_tests
if "%test_choice%"=="2" goto run_storage_tests
if "%test_choice%"=="3" goto run_cache_tests
if "%test_choice%"=="4" goto run_mock_tests
if "%test_choice%"=="5" goto run_all_tests
echo ❌ 无效选择，运行默认测试
goto run_integration_tests

:run_integration_tests
echo.
echo 🔄 运行Redis集成测试...
cd ../.. && dotnet test Wind.sln --filter "Category=Integration" --logger "console;verbosity=normal" --collect:"XPlat Code Coverage"
goto test_complete

:run_storage_tests
echo.
echo 🔄 运行Redis存储验证测试...
cd ../.. && dotnet test Wind.Tests\Wind.Tests.csproj --filter "FullyQualifiedName~RedisStorageValidationTests" --logger "console;verbosity=normal"
goto test_complete

:run_cache_tests
echo.
echo 🔄 运行Redis缓存策略测试...
cd ../.. && dotnet test Wind.Tests\Wind.Tests.csproj --filter "FullyQualifiedName~RedisCacheStrategyTests" --logger "console;verbosity=normal"
goto test_complete

:run_mock_tests
echo.
echo 🔄 运行Redis Mock测试 (无需Redis连接)...
cd ../.. && dotnet test Wind.Tests\Wind.Tests.csproj --filter "FullyQualifiedName~RedisCacheStrategyMockTests" --logger "console;verbosity=normal"
goto test_complete

:run_all_tests
echo.
echo 🔄 运行完整测试套件...
cd ../.. && dotnet test Wind.sln --logger "console;verbosity=normal" --collect:"XPlat Code Coverage"
goto test_complete

:test_complete
set test_result=%errorlevel%

echo.
echo ==========================================
if %test_result%==0 (
    echo ✅ 测试执行完成！
    echo.
    echo 📊 测试结果总结:
    echo    - Redis连接: 正常
    echo    - 测试状态: 通过
    echo    - 环境状态: 健康
) else (
    echo ❌ 测试执行遇到问题
    echo.
    echo 🔍 问题排查建议:
    echo    1. 检查Redis连接配置是否正确
    echo    2. 确认Redis密码: windgame123
    echo    3. 检查端口6379是否被占用
    echo    4. 查看详细错误日志
)

echo ==========================================
echo.

rem 显示Redis统计信息
echo 📈 Redis服务状态:
echo.
docker exec wind-redis-test redis-cli -a windgame123 info server 2>nul | findstr "redis_version"
docker exec wind-redis-test redis-cli -a windgame123 info memory 2>nul | findstr "used_memory_human"
docker exec wind-redis-test redis-cli -a windgame123 info stats 2>nul | findstr "total_commands_processed"

echo.
echo 🐳 容器状态:
docker-compose -f docker-compose.test.yml ps

echo.
echo 💡 后续操作:
echo    - 如需重复测试: 再次运行此脚本
echo    - 停止Redis环境: 双击 stop.bat
echo    - 查看Redis数据: 运行 docker exec -it wind-redis-test redis-cli -a windgame123
echo    - 图形管理界面: docker-compose -f docker-compose.test.yml --profile debug up -d
echo.
pause
exit /b %test_result%
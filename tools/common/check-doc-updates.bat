@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
title Wind项目 - 文档更新检查工具

echo.
echo ==========================================
echo   Wind项目 - 文档更新检查工具
echo ==========================================
echo.

rem 设置关键配置文件路径
set "REDIS_CONFIG=Wind.Server\appsettings.json"
set "REDIS_COMPOSE=tools\redis-testing\docker-compose.test.yml"
set "REDIS_DOC=docs\development\redis-testing.md"

rem 检查是否在项目根目录
if not exist "Wind.sln" (
    echo ❌ 错误: 请在项目根目录执行此脚本
    pause
    exit /b 1
)

echo [1/3] 检查Redis配置文件更新...

rem 检查Redis配置文件的最后修改时间
for %%F in ("%REDIS_CONFIG%") do (
    set CONFIG_DATE=%%~tF
)

for %%F in ("%REDIS_COMPOSE%") do (
    set COMPOSE_DATE=%%~tF
)

for %%F in ("%REDIS_DOC%") do (
    set DOC_DATE=%%~tF
)

echo.
echo 📋 文件修改时间对比:
echo    配置文件: %CONFIG_DATE%
echo    Docker配置: %COMPOSE_DATE%  
echo    文档文件: %DOC_DATE%
echo.

rem 简单的时间比较提示（Windows批处理限制，仅做提示）
echo [2/3] 分析文档更新需要...

echo.
echo 🔍 检查项目:
echo    ✓ Redis端口配置 (6379)
echo    ✓ Redis密码配置 (windgame123)
echo    ✓ Docker镜像版本 (redis:7.2-alpine)
echo    ✓ 文档版本标记

rem 检查关键配置项
findstr /c:"6379" "%REDIS_CONFIG%" >nul
if errorlevel 1 (
    echo    ⚠️  警告: Redis端口配置可能已变更
    set UPDATE_NEEDED=1
)

findstr /c:"windgame123" "%REDIS_CONFIG%" >nul
if errorlevel 1 (
    echo    ⚠️  警告: Redis密码配置可能已变更
    set UPDATE_NEEDED=1
)

findstr /c:"redis:7.2-alpine" "%REDIS_COMPOSE%" >nul
if errorlevel 1 (
    echo    ⚠️  警告: Redis Docker镜像版本可能已变更
    set UPDATE_NEEDED=1
)

echo.
echo [3/3] 生成更新建议...

if defined UPDATE_NEEDED (
    echo.
    echo ❌ 发现配置变更，需要更新文档！
    echo.
    echo 📝 需要更新的文档:
    echo    - docs/development/redis-testing.md
    echo    - tools/redis-testing/README.md
    echo.
    echo 🔧 建议更新内容:
    echo    1. 检查端口、密码等配置信息
    echo    2. 更新文档中的版本号
    echo    3. 记录变更历史
    echo    4. 验证脚本是否需要修改
    echo.
    echo 💡 提示: 请手动检查配置差异并更新相应文档
) else (
    echo.
    echo ✅ 未发现明显的配置变更
    echo.
    echo 💡 建议:
    echo    - 定期运行此检查工具
    echo    - 配置变更时主动更新文档
    echo    - 保持文档版本信息最新
)

echo.
echo ==========================================
echo 📊 文档健康度检查完成
echo ==========================================
echo.

rem 显示相关文档路径
echo 📂 相关文档路径:
echo    完整指南: %REDIS_DOC%
echo    工具说明: tools/redis-testing/README.md
echo    配置文件: %REDIS_CONFIG%
echo    Docker配置: %REDIS_COMPOSE%

echo.
echo 💡 下次检查建议: 
echo    - 代码配置变更后
echo    - 每周定期检查
echo    - 版本发布前验证

echo.
pause
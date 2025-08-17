@echo off
:: 文档质量检查工具 - 批处理包装器
:: Wind项目文档质量保证工具
:: 版本: v1.0.0

setlocal EnableDelayedExpansion

:: 设置默认参数
set "CHECK_PATH=plans\"
set "DETAILED=false"
set "AUTO_FIX=false"
set "LOG_FILE=doc-quality-check.log"

:: 解析命令行参数
:parse_args
if "%1"=="" goto :start_check
if "%1"=="--path" (
    set "CHECK_PATH=%2"
    shift
    shift
    goto :parse_args
)
if "%1"=="--detailed" (
    set "DETAILED=true"
    shift
    goto :parse_args
)
if "%1"=="--fix" (
    set "AUTO_FIX=true"
    shift
    goto :parse_args
)
if "%1"=="--log" (
    set "LOG_FILE=%2"
    shift
    shift
    goto :parse_args
)
if "%1"=="--help" goto :show_help

:: 显示使用方法后退出
echo Unknown parameter: %1
goto :show_help

:start_check
echo.
echo ============================================
echo Wind项目文档质量检查工具
echo ============================================
echo.
echo 检查路径: %CHECK_PATH%
echo 详细模式: %DETAILED%
echo 自动修复: %AUTO_FIX%
echo 日志文件: %LOG_FILE%
echo.

:: 检查PowerShell是否可用
powershell -Command "Write-Host 'PowerShell available'" >nul 2>&1
if errorlevel 1 (
    echo ERROR: PowerShell is not available or accessible
    echo This tool requires PowerShell to run
    exit /b 1
)

:: 检查脚本文件是否存在
if not exist "tools\doc-manager\check-doc-quality.ps1" (
    echo ERROR: PowerShell script not found: tools\doc-manager\check-doc-quality.ps1
    echo Please ensure you are running this from the project root directory
    exit /b 1
)

:: 构建PowerShell命令
set "PS_COMMAND=powershell -ExecutionPolicy Bypass -File tools\doc-manager\check-doc-quality.ps1"
set "PS_COMMAND=%PS_COMMAND% -Path '%CHECK_PATH%'"
set "PS_COMMAND=%PS_COMMAND% -LogFile '%LOG_FILE%'"

if "%DETAILED%"=="true" (
    set "PS_COMMAND=%PS_COMMAND% -Detailed"
)

if "%AUTO_FIX%"=="true" (
    set "PS_COMMAND=%PS_COMMAND% -FixIssues"
)

:: 执行PowerShell脚本
echo Executing quality check...
echo.
%PS_COMMAND%

set "EXIT_CODE=%ERRORLEVEL%"

echo.
echo ============================================
echo 质量检查完成
echo ============================================

:: 检查是否生成了报告
if exist "doc-quality-report.md" (
    echo.
    echo 质量报告已生成: doc-quality-report.md
    echo 检查日志已保存: %LOG_FILE%
)

:: 根据检查结果显示相应信息
if %EXIT_CODE% EQU 0 (
    echo.
    echo 检查完成，请查看报告了解详细结果。
) else (
    echo.
    echo 检查过程中遇到错误，请查看日志文件: %LOG_FILE%
)

exit /b %EXIT_CODE%

:show_help
echo.
echo Wind项目文档质量检查工具
echo.
echo 用法:
echo   check-doc-quality.bat [选项]
echo.
echo 选项:
echo   --path PATH     指定检查路径 (默认: plans\)
echo   --detailed      启用详细模式，显示更多信息
echo   --fix           启用自动修复模式
echo   --log FILE      指定日志文件名 (默认: doc-quality-check.log)
echo   --help          显示此帮助信息
echo.
echo 示例:
echo   check-doc-quality.bat
echo   check-doc-quality.bat --path docs\ --detailed
echo   check-doc-quality.bat --fix --detailed
echo   check-doc-quality.bat --path plans\technical-research\ --log tech-check.log
echo.
echo 说明:
echo   - 默认检查 plans\ 目录下的所有 .md 文件
echo   - 检查项包括: 文件大小、模板合规性、链接有效性、编码格式、内容质量
echo   - 自动修复模式可以修复部分简单问题，如移除BOM、减少过多空行等
echo   - 检查结果会生成详细的质量报告: doc-quality-report.md
echo.
exit /b 0
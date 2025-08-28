@echo off
REM ================================================================
REM 网页内容获取工具 - v1.0.0
REM 
REM 用途: 获取指定URL的网页内容，供Claude Code分析使用
REM 作者: Wind项目开发组
REM 创建时间: 2025-08-28
REM ================================================================

setlocal enabledelayedexpansion

REM 设置默认参数
set "URL="
set "OUTPUT_FILE="
set "CLEAN_HTML=false"
set "USER_AGENT=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
set "TIMEOUT=30"

REM 显示帮助信息
if "%1"=="" goto show_help
if "%1"=="-h" goto show_help
if "%1"=="--help" goto show_help

REM 解析参数
:parse_args
if "%1"=="" goto main
if "%1"=="-u" (
    set "URL=%2"
    shift
    shift
    goto parse_args
)
if "%1"=="--url" (
    set "URL=%2"
    shift
    shift
    goto parse_args
)
if "%1"=="-o" (
    set "OUTPUT_FILE=%2"
    shift
    shift
    goto parse_args
)
if "%1"=="--output" (
    set "OUTPUT_FILE=%2"
    shift
    shift
    goto parse_args
)
if "%1"=="--clean" (
    set "CLEAN_HTML=true"
    shift
    goto parse_args
)
if "%1"=="--timeout" (
    set "TIMEOUT=%2"
    shift
    shift
    goto parse_args
)
REM 如果没有选项标志，将第一个参数作为URL
set "URL=%1"
shift
goto parse_args

:main
REM 验证URL参数
if "%URL%"=="" (
    echo 错误: 需要指定URL参数
    goto show_help
)

REM 设置默认输出文件名
if "%OUTPUT_FILE%"=="" (
    for /f "tokens=2-4 delims=/ " %%a in ('date /t') do set "mydate=%%c-%%a-%%b"
    for /f "tokens=1-2 delims=: " %%a in ('time /t') do set "mytime=%%a-%%b"
    set "OUTPUT_FILE=web-content-!mydate!-!mytime!.txt"
)

echo ================================================================
echo 网页内容获取工具
echo ================================================================
echo URL: %URL%
echo 输出文件: %OUTPUT_FILE%
echo 清理HTML: %CLEAN_HTML%
echo 超时时间: %TIMEOUT%秒
echo ================================================================

REM 检查curl是否可用
curl --version >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: curl不可用，请确保curl已安装且在PATH中
    exit /b 1
)

REM 创建临时文件
set "TEMP_FILE=%TEMP%\web_content_temp_%RANDOM%.html"

REM 使用curl获取网页内容
echo 正在获取网页内容...
curl -L -s --max-time %TIMEOUT% ^
     --user-agent "%USER_AGENT%" ^
     --header "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" ^
     --header "Accept-Language: zh-CN,zh;q=0.9,en;q=0.8" ^
     --header "Accept-Encoding: gzip, deflate" ^
     --compressed ^
     --connect-timeout 10 ^
     --max-redirs 5 ^
     "%URL%" > "%TEMP_FILE%"

if %errorlevel% neq 0 (
    echo 错误: 获取网页内容失败 (curl返回代码: %errorlevel%)
    if exist "%TEMP_FILE%" del "%TEMP_FILE%"
    exit /b 1
)

REM 检查临时文件是否存在且不为空
if not exist "%TEMP_FILE%" (
    echo 错误: 临时文件未创建
    exit /b 1
)

for %%A in ("%TEMP_FILE%") do set "FILE_SIZE=%%~zA"
if %FILE_SIZE%==0 (
    echo 错误: 获取的内容为空
    del "%TEMP_FILE%"
    exit /b 1
)

REM 处理内容
if "%CLEAN_HTML%"=="true" (
    echo 正在清理HTML标签...
    
    REM 创建输出文件头部
    echo ================================================================ > "%OUTPUT_FILE%"
    echo 网页内容获取结果 >> "%OUTPUT_FILE%"
    echo ================================================================ >> "%OUTPUT_FILE%"
    echo URL: %URL% >> "%OUTPUT_FILE%"
    echo 获取时间: %DATE% %TIME% >> "%OUTPUT_FILE%"
    echo 原始大小: %FILE_SIZE% 字节 >> "%OUTPUT_FILE%"
    echo 内容处理: HTML标签已清理 >> "%OUTPUT_FILE%"
    echo ================================================================ >> "%OUTPUT_FILE%"
    echo. >> "%OUTPUT_FILE%"
    
    REM 简单的HTML标签清理 (使用PowerShell)
    powershell -Command "(Get-Content '%TEMP_FILE%' -Raw) -replace '<[^>]*>', '' -replace '&nbsp;', ' ' -replace '&lt;', '<' -replace '&gt;', '>' -replace '&amp;', '&' -replace '\s+', ' ' -replace '^\s+|\s+$', ''" >> "%OUTPUT_FILE%"
) else (
    REM 直接复制原始内容
    echo ================================================================ > "%OUTPUT_FILE%"
    echo 网页内容获取结果 >> "%OUTPUT_FILE%"
    echo ================================================================ >> "%OUTPUT_FILE%"
    echo URL: %URL% >> "%OUTPUT_FILE%"
    echo 获取时间: %DATE% %TIME% >> "%OUTPUT_FILE%"
    echo 原始大小: %FILE_SIZE% 字节 >> "%OUTPUT_FILE%"
    echo 内容处理: 保持原始HTML格式 >> "%OUTPUT_FILE%"
    echo ================================================================ >> "%OUTPUT_FILE%"
    echo. >> "%OUTPUT_FILE%"
    
    type "%TEMP_FILE%" >> "%OUTPUT_FILE%"
)

REM 清理临时文件
del "%TEMP_FILE%"

REM 显示结果
for %%A in ("%OUTPUT_FILE%") do set "OUTPUT_SIZE=%%~zA"
echo.
echo ================================================================
echo 获取完成！
echo ================================================================
echo 输出文件: %OUTPUT_FILE%
echo 文件大小: %OUTPUT_SIZE% 字节
echo.
echo 建议后续操作:
echo 1. 使用Read工具查看获取的内容
echo 2. 根据需要调整清理选项重新获取
echo 3. 将有用信息整理到项目文档中
echo ================================================================

exit /b 0

:show_help
echo.
echo ================================================================
echo 网页内容获取工具 - 使用说明
echo ================================================================
echo.
echo 用法: 
echo   %~n0 ^<URL^> [选项]
echo   %~n0 -u ^<URL^> [选项]
echo.
echo 参数:
echo   ^<URL^>                    要获取的网页URL (必需)
echo.
echo 选项:
echo   -u, --url ^<URL^>          指定要获取的URL
echo   -o, --output ^<file^>      指定输出文件名
echo   --clean                  清理HTML标签，输出纯文本
echo   --timeout ^<seconds^>     设置超时时间 (默认: 30秒)
echo   -h, --help               显示此帮助信息
echo.
echo 示例:
echo   %~n0 https://github.com/Cysharp/MagicOnion
echo   %~n0 -u "https://magiconion.com/docs" -o "magiconion-docs.txt" --clean
echo   %~n0 "https://example.com" --timeout 60
echo.
echo 注意事项:
echo   - 需要curl工具支持
echo   - 输出文件保存在当前目录
echo   - 支持重定向和gzip压缩
echo   - 自动添加常见HTTP头部
echo.
echo ================================================================
exit /b 0
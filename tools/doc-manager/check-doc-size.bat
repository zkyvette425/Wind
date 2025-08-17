@echo off
REM 文档大小检查和容量预估工具
REM 使用方法: check-doc-size.bat <文档路径> [新增内容文件]

setlocal enabledelayedexpansion

if "%1"=="" (
    echo 用法: check-doc-size.bat ^<文档路径^> [新增内容文件]
    echo 示例: check-doc-size.bat "plans\版本变更日志.md"
    echo 示例: check-doc-size.bat "plans\版本变更日志.md" "temp\new-content.txt"
    exit /b 1
)

set "doc_file=%1"
set "new_content_file=%2"

REM 检查文档是否存在
if not exist "%doc_file%" (
    echo 错误: 文档文件不存在: %doc_file%
    exit /b 1
)

REM 获取当前文档大小（字节）
for %%F in ("%doc_file%") do set current_size=%%~zF

REM 设置安全阈值（22KB = 22528字节）
set safe_threshold=22528
set max_limit=25000

echo =====================================
echo 文档大小检查报告
echo =====================================
echo 文档路径: %doc_file%
echo 当前大小: %current_size% 字节

REM 计算状态
if %current_size% GTR %max_limit% (
    set status=CRITICAL
    set color=31
    echo 状态: [91mCRITICAL - 已超过Read工具限制！[0m
) else if %current_size% GTR %safe_threshold% (
    set status=WARNING
    set color=33
    echo 状态: [93mWARNING - 接近容量限制[0m
) else (
    set status=OK
    set color=32
    echo 状态: [92mOK - 容量充足[0m
)

REM 计算剩余容量
set /a remaining=%safe_threshold% - %current_size%
echo 剩余安全容量: %remaining% 字节

REM 如果提供了新增内容文件，计算添加后的大小
if not "%new_content_file%"=="" (
    if exist "%new_content_file%" (
        for %%F in ("%new_content_file%") do set new_content_size=%%~zF
        set /a total_size=%current_size% + %new_content_size%
        
        echo -------------------------------------
        echo 新增内容大小: %new_content_size% 字节
        echo 添加后总大小: %total_size% 字节
        
        if !total_size! GTR %safe_threshold% (
            echo 建议: [91m需要分片！添加后将超过安全阈值[0m
            set recommendation=SPLIT_REQUIRED
        ) else (
            echo 建议: [92m可以安全添加[0m
            set recommendation=OK
        )
    ) else (
        echo 错误: 新增内容文件不存在: %new_content_file%
    )
)

echo =====================================

REM 输出机器可读的结果
if "%recommendation%"=="SPLIT_REQUIRED" (
    echo RESULT:SPLIT_REQUIRED
    exit /b 2
) else if "%status%"=="CRITICAL" (
    echo RESULT:CRITICAL
    exit /b 3
) else if "%status%"=="WARNING" (
    echo RESULT:WARNING
    exit /b 1
) else (
    echo RESULT:OK
    exit /b 0
)
@echo off
REM Simplified web content fetcher
REM Usage: simple-fetch.bat <URL> <output-file>

set "URL=%1"
set "OUTPUT=%2"

if "%URL%"=="" (
    echo Error: Please provide URL
    exit /b 1
)

if "%OUTPUT%"=="" (
    echo Error: Please provide output filename
    exit /b 1
)

echo Fetching: %URL%
echo Output: %OUTPUT%

curl -L -s --max-time 30 --user-agent "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" "%URL%" > "%OUTPUT%"

if %errorlevel% neq 0 (
    echo Error: Failed to fetch content
    exit /b 1
)

echo Success: Content saved to %OUTPUT%
exit /b 0
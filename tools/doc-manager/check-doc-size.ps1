# Document Size Check and Capacity Estimation Tool - PowerShell Version
param(
    [Parameter(Mandatory=$true)]
    [string]$DocumentPath,
    
    [Parameter(Mandatory=$false)]
    [string]$NewContentFile = ""
)

# Safety threshold settings
$SafeThreshold = 22000  # 22KB
$MaxLimit = 25000       # 25KB Read tool limit

function Write-ColorText {
    param($Text, $Color)
    Write-Host $Text -ForegroundColor $Color
}

function Get-DocumentStatus {
    param($Size)
    
    if ($Size -gt $MaxLimit) {
        return @{Status="CRITICAL"; Color="Red"}
    } elseif ($Size -gt $SafeThreshold) {
        return @{Status="WARNING"; Color="Yellow"}
    } else {
        return @{Status="OK"; Color="Green"}
    }
}

# Check if document exists
if (-not (Test-Path $DocumentPath)) {
    Write-ColorText "Error: Document file not found: $DocumentPath" "Red"
    exit 1
}

# 获取当前文档大小
$CurrentSize = (Get-Item $DocumentPath).Length

# 输出检查报告
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "文档大小检查报告" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "文档路径: $DocumentPath"
Write-Host "当前大小: $CurrentSize 字节"

# 获取状态
$StatusInfo = Get-DocumentStatus $CurrentSize
Write-ColorText "状态: [$($StatusInfo.Status)]" $StatusInfo.Color

# 计算剩余容量
$Remaining = $SafeThreshold - $CurrentSize
Write-Host "剩余安全容量: $Remaining 字节"

# 如果提供了新增内容文件，计算添加后的大小
$Recommendation = "OK"
if ($NewContentFile -ne "" -and (Test-Path $NewContentFile)) {
    $NewContentSize = (Get-Item $NewContentFile).Length
    $TotalSize = $CurrentSize + $NewContentSize
    
    Write-Host "-------------------------------------"
    Write-Host "新增内容大小: $NewContentSize 字节"
    Write-Host "添加后总大小: $TotalSize 字节"
    
    if ($TotalSize -gt $SafeThreshold) {
        Write-ColorText "建议: [需要分片！添加后将超过安全阈值]" "Red"
        $Recommendation = "SPLIT_REQUIRED"
    } else {
        Write-ColorText "建议: [可以安全添加]" "Green"
    }
} elseif ($NewContentFile -ne "" -and (-not (Test-Path $NewContentFile))) {
    Write-ColorText "错误: 新增内容文件不存在: $NewContentFile" "Red"
}

Write-Host "=====================================" -ForegroundColor Cyan

# 输出机器可读的结果
if ($Recommendation -eq "SPLIT_REQUIRED") {
    Write-Host "RESULT:SPLIT_REQUIRED"
    exit 2
} elseif ($StatusInfo.Status -eq "CRITICAL") {
    Write-Host "RESULT:CRITICAL"
    exit 3
} elseif ($StatusInfo.Status -eq "WARNING") {
    Write-Host "RESULT:WARNING"
    exit 1
} else {
    Write-Host "RESULT:OK"
    exit 0
}
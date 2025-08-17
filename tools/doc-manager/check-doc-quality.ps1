# 文档质量检查自动化脚本
# Wind项目文档质量保证工具
# 版本: v1.0.0
# 创建时间: 2025-08-18

param(
    [string]$Path = "plans/",           # 检查路径，默认为plans目录
    [switch]$Detailed = $false,         # 详细模式，显示更多信息
    [switch]$FixIssues = $false,        # 自动修复模式
    [string]$LogFile = "doc-quality-check.log"  # 日志文件
)

# 初始化
$ErrorActionPreference = "Continue"
$StartTime = Get-Date
$IssueCount = 0
$CheckCount = 0
$FixCount = 0

# 颜色输出函数
function Write-ColorText {
    param([string]$Text, [string]$Color = "White")
    switch ($Color) {
        "Red" { Write-Host $Text -ForegroundColor Red }
        "Green" { Write-Host $Text -ForegroundColor Green }
        "Yellow" { Write-Host $Text -ForegroundColor Yellow }
        "Blue" { Write-Host $Text -ForegroundColor Blue }
        "Cyan" { Write-Host $Text -ForegroundColor Cyan }
        default { Write-Host $Text }
    }
}

# 日志记录函数
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $TimeStamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $LogEntry = "[$TimeStamp] [$Level] $Message"
    Add-Content -Path $LogFile -Value $LogEntry -Encoding UTF8
    
    if ($Level -eq "ERROR") {
        Write-ColorText $Message "Red"
    } elseif ($Level -eq "WARN") {
        Write-ColorText $Message "Yellow"
    } elseif ($Level -eq "SUCCESS") {
        Write-ColorText $Message "Green"
    } else {
        Write-ColorText $Message "White"
    }
}

# 文件大小检查
function Test-FileSize {
    param([string]$FilePath)
    
    $MaxSize = 25600  # 25KB limit
    $WarnSize = 22000 # 22KB warning
    
    if (Test-Path $FilePath) {
        $FileSize = (Get-Item $FilePath).Length
        
        if ($FileSize -gt $MaxSize) {
            Write-Log "CRITICAL: File exceeds 25KB limit: $FilePath ($FileSize bytes)" "ERROR"
            return @{ Status = "CRITICAL"; Size = $FileSize; Issue = "Exceeds size limit" }
        } elseif ($FileSize -gt $WarnSize) {
            Write-Log "WARNING: File approaching size limit: $FilePath ($FileSize bytes)" "WARN"
            return @{ Status = "WARNING"; Size = $FileSize; Issue = "Approaching size limit" }
        } else {
            if ($Detailed) {
                Write-Log "OK: File size acceptable: $FilePath ($FileSize bytes)" "SUCCESS"
            }
            return @{ Status = "OK"; Size = $FileSize; Issue = $null }
        }
    }
    return @{ Status = "ERROR"; Size = 0; Issue = "File not found" }
}

# 模板合规性检查
function Test-TemplateCompliance {
    param([string]$FilePath)
    
    $Content = Get-Content $FilePath -Encoding UTF8 -Raw
    $Issues = @()
    
    # 检查版本头部信息
    if ($Content -notmatch '> \*\*文档版本\*\*:') {
        $Issues += "Missing version information"
    }
    
    if ($Content -notmatch '> \*\*创建时间\*\*:') {
        $Issues += "Missing creation time"
    }
    
    if ($Content -notmatch '> \*\*最后更新\*\*:') {
        $Issues += "Missing last update time"
    }
    
    # 检查标题结构
    $Headers = [regex]::Matches($Content, '^#+\s+(.+)$', [System.Text.RegularExpressions.RegexOptions]::Multiline)
    if ($Headers.Count -lt 3) {
        $Issues += "Insufficient header structure (less than 3 headers)"
    }
    
    # 检查代码块格式
    $CodeBlocks = [regex]::Matches($Content, '```(\w+)?', [System.Text.RegularExpressions.RegexOptions]::Multiline)
    $UnlabeledBlocks = 0
    foreach ($Block in $CodeBlocks) {
        if ($Block.Groups[1].Value -eq "") {
            $UnlabeledBlocks++
        }
    }
    if ($UnlabeledBlocks -gt 0) {
        $Issues += "Found $UnlabeledBlocks unlabeled code blocks"
    }
    
    return $Issues
}

# 链接有效性检查
function Test-LinkValidity {
    param([string]$FilePath, [string]$BaseDir)
    
    $Content = Get-Content $FilePath -Encoding UTF8 -Raw
    $Issues = @()
    
    # 查找Markdown链接
    $Links = [regex]::Matches($Content, '\[([^\]]*)\]\(([^)]+)\)')
    
    foreach ($Link in $Links) {
        $LinkText = $Link.Groups[1].Value
        $LinkPath = $Link.Groups[2].Value
        
        # 跳过外部链接
        if ($LinkPath -match '^https?://') {
            continue
        }
        
        # 处理相对路径
        $FullPath = ""
        if ($LinkPath.StartsWith("../")) {
            $FullPath = Join-Path (Split-Path $FilePath -Parent) $LinkPath
        } elseif ($LinkPath.StartsWith("./")) {
            $FullPath = Join-Path (Split-Path $FilePath -Parent) $LinkPath.Substring(2)
        } else {
            $FullPath = Join-Path $BaseDir $LinkPath
        }
        
        # 解析路径
        try {
            $ResolvedPath = Resolve-Path $FullPath -ErrorAction SilentlyContinue
            if (-not $ResolvedPath) {
                $Issues += "Broken link: '$LinkText' -> '$LinkPath'"
            }
        } catch {
            $Issues += "Invalid link path: '$LinkText' -> '$LinkPath'"
        }
    }
    
    return $Issues
}

# 编码格式检查
function Test-FileEncoding {
    param([string]$FilePath)
    
    try {
        $Bytes = [System.IO.File]::ReadAllBytes($FilePath)
        
        # 检查BOM
        if ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) {
            return @{ Status = "WARNING"; Issue = "UTF-8 with BOM detected (should be UTF-8 without BOM)" }
        }
        
        # 尝试读取为UTF-8
        $Content = [System.Text.Encoding]::UTF8.GetString($Bytes)
        if ($Content.Contains([char]0xFFFD)) {
            return @{ Status = "ERROR"; Issue = "Invalid UTF-8 encoding detected" }
        }
        
        return @{ Status = "OK"; Issue = $null }
    } catch {
        return @{ Status = "ERROR"; Issue = "Unable to check encoding: $($_.Exception.Message)" }
    }
}

# 内容质量检查
function Test-ContentQuality {
    param([string]$FilePath)
    
    $Content = Get-Content $FilePath -Encoding UTF8 -Raw
    $Issues = @()
    
    # 检查TODO标记
    $TodoMatches = [regex]::Matches($Content, '(?i)(todo|fixme|hack|xxx)')
    if ($TodoMatches.Count -gt 0) {
        $Issues += "Found $($TodoMatches.Count) TODO/FIXME markers"
    }
    
    # 检查空行过多
    $EmptyLineMatches = [regex]::Matches($Content, '\n\s*\n\s*\n\s*\n')
    if ($EmptyLineMatches.Count -gt 0) {
        $Issues += "Found $($EmptyLineMatches.Count) sections with excessive empty lines"
    }
    
    # 检查行长度
    $Lines = $Content -split '\n'
    $LongLines = 0
    foreach ($Line in $Lines) {
        if ($Line.Length -gt 120) {
            $LongLines++
        }
    }
    if ($LongLines -gt 0) {
        $Issues += "Found $LongLines lines exceeding 120 characters"
    }
    
    return $Issues
}

# 自动修复功能
function Invoke-AutoFix {
    param([string]$FilePath, [array]$Issues)
    
    $FixedIssues = @()
    $Content = Get-Content $FilePath -Encoding UTF8 -Raw
    $Modified = $false
    
    foreach ($Issue in $Issues) {
        if ($Issue -match "UTF-8 with BOM") {
            # 移除BOM
            $Bytes = [System.IO.File]::ReadAllBytes($FilePath)
            if ($Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) {
                $NewBytes = $Bytes[3..($Bytes.Length-1)]
                [System.IO.File]::WriteAllBytes($FilePath, $NewBytes)
                $FixedIssues += "Removed UTF-8 BOM"
                $Modified = $true
            }
        } elseif ($Issue -match "excessive empty lines") {
            # 减少过多的空行
            $NewContent = [regex]::Replace($Content, '\n\s*\n\s*\n\s*\n+', "`n`n`n")
            if ($NewContent -ne $Content) {
                [System.IO.File]::WriteAllText($FilePath, $NewContent, [System.Text.Encoding]::UTF8)
                $FixedIssues += "Reduced excessive empty lines"
                $Modified = $true
            }
        }
    }
    
    return $FixedIssues
}

# 生成质量报告
function New-QualityReport {
    param([array]$Results)
    
    $ReportPath = "doc-quality-report.md"
    $ReportTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    
    $Report = @"
# 文档质量检查报告

> **检查时间**: $ReportTime  
> **检查范围**: $Path  
> **检查工具**: Wind项目文档质量检查脚本 v1.0.0  

## 📊 检查统计

- **文档总数**: $CheckCount
- **发现问题**: $IssueCount
- **自动修复**: $FixCount
- **检查用时**: $((Get-Date) - $StartTime)

## 📋 检查结果

| 文档 | 大小 | 模板合规 | 链接检查 | 编码格式 | 内容质量 | 状态 |
|------|------|----------|----------|----------|----------|------|
"@

    foreach ($Result in $Results) {
        $SizeStatus = if ($Result.SizeCheck.Status -eq "OK") { "✅" } elseif ($Result.SizeCheck.Status -eq "WARNING") { "⚠️" } else { "❌" }
        $TemplateStatus = if ($Result.TemplateIssues.Count -eq 0) { "✅" } else { "❌" }
        $LinkStatus = if ($Result.LinkIssues.Count -eq 0) { "✅" } else { "❌" }
        $EncodingStatus = if ($Result.EncodingCheck.Status -eq "OK") { "✅" } else { "❌" }
        $ContentStatus = if ($Result.ContentIssues.Count -eq 0) { "✅" } else { "⚠️" }
        $OverallStatus = if ($Result.OverallStatus -eq "OK") { "✅ 通过" } elseif ($Result.OverallStatus -eq "WARNING") { "⚠️ 警告" } else { "❌ 失败" }
        
        $SizeKB = [math]::Round($Result.SizeCheck.Size / 1024, 1)
        
        $Report += "`n| $($Result.File) | ${SizeKB}KB | $TemplateStatus | $LinkStatus | $EncodingStatus | $ContentStatus | $OverallStatus |"
    }

    # 添加详细问题列表
    $Report += "`n`n## 🔍 详细问题列表`n"
    
    foreach ($Result in $Results) {
        if ($Result.OverallStatus -ne "OK") {
            $Report += "`n### $($Result.File)`n"
            
            if ($Result.SizeCheck.Issue) {
                $Report += "- **文件大小**: $($Result.SizeCheck.Issue)`n"
            }
            
            if ($Result.TemplateIssues.Count -gt 0) {
                $Report += "- **模板合规性**:`n"
                foreach ($Issue in $Result.TemplateIssues) {
                    $Report += "  - $Issue`n"
                }
            }
            
            if ($Result.LinkIssues.Count -gt 0) {
                $Report += "- **链接问题**:`n"
                foreach ($Issue in $Result.LinkIssues) {
                    $Report += "  - $Issue`n"
                }
            }
            
            if ($Result.EncodingCheck.Issue) {
                $Report += "- **编码格式**: $($Result.EncodingCheck.Issue)`n"
            }
            
            if ($Result.ContentIssues.Count -gt 0) {
                $Report += "- **内容质量**:`n"
                foreach ($Issue in $Result.ContentIssues) {
                    $Report += "  - $Issue`n"
                }
            }
        }
    }

    $Report += "`n`n## 🛠️ 修复建议`n`n"
    $Report += "1. **文件大小问题**: 使用 ``tools/doc-manager/split-timeline-doc.ps1`` 或 ``split-topic-doc.ps1`` 进行分片`n"
    $Report += "2. **模板合规性**: 参考 ``docs/templates/`` 中的标准模板`n"
    $Report += "3. **链接问题**: 检查并修复文档路径，更新失效链接`n"
    $Report += "4. **编码格式**: 确保文件使用UTF-8无BOM编码保存`n"
    $Report += "5. **内容质量**: 完善文档内容，规范格式标准`n"

    [System.IO.File]::WriteAllText($ReportPath, $Report, [System.Text.Encoding]::UTF8)
    Write-Log "Quality report generated: $ReportPath" "SUCCESS"
}

# 主检查流程
function Start-QualityCheck {
    Write-ColorText "=== Wind项目文档质量检查 ===" "Cyan"
    Write-ColorText "检查路径: $Path" "Blue"
    Write-ColorText "详细模式: $Detailed" "Blue"
    Write-ColorText "自动修复: $FixIssues" "Blue"
    Write-ColorText "" "White"
    
    Write-Log "Starting document quality check for: $Path"
    
    if (-not (Test-Path $Path)) {
        Write-Log "Path not found: $Path" "ERROR"
        return
    }
    
    $MarkdownFiles = Get-ChildItem -Path $Path -Recurse -Filter "*.md" | Where-Object { $_.Name -ne "README.md" -or $Detailed }
    $Results = @()
    
    foreach ($File in $MarkdownFiles) {
        $script:CheckCount++
        $RelativePath = $File.FullName.Replace((Get-Location).Path + "\", "")
        
        Write-ColorText "Checking: $RelativePath" "Blue"
        
        # 执行各项检查
        $SizeCheck = Test-FileSize $File.FullName
        $TemplateIssues = Test-TemplateCompliance $File.FullName
        $LinkIssues = Test-LinkValidity $File.FullName (Get-Location).Path
        $EncodingCheck = Test-FileEncoding $File.FullName
        $ContentIssues = Test-ContentQuality $File.FullName
        
        # 计算总体状态
        $OverallStatus = "OK"
        if ($SizeCheck.Status -eq "CRITICAL" -or $EncodingCheck.Status -eq "ERROR" -or $TemplateIssues.Count -gt 2) {
            $OverallStatus = "ERROR"
            $script:IssueCount++
        } elseif ($SizeCheck.Status -eq "WARNING" -or $EncodingCheck.Status -eq "WARNING" -or $TemplateIssues.Count -gt 0 -or $LinkIssues.Count -gt 0 -or $ContentIssues.Count -gt 0) {
            $OverallStatus = "WARNING"
            $script:IssueCount++
        }
        
        # 自动修复
        $FixedIssues = @()
        if ($FixIssues -and $OverallStatus -ne "OK") {
            $AllIssues = @()
            if ($EncodingCheck.Issue) { $AllIssues += $EncodingCheck.Issue }
            $AllIssues += $ContentIssues
            
            $FixedIssues = Invoke-AutoFix $File.FullName $AllIssues
            if ($FixedIssues.Count -gt 0) {
                $script:FixCount += $FixedIssues.Count
                Write-Log "Auto-fixed $($FixedIssues.Count) issues in: $RelativePath" "SUCCESS"
            }
        }
        
        $Results += @{
            File = $RelativePath
            SizeCheck = $SizeCheck
            TemplateIssues = $TemplateIssues
            LinkIssues = $LinkIssues
            EncodingCheck = $EncodingCheck
            ContentIssues = $ContentIssues
            FixedIssues = $FixedIssues
            OverallStatus = $OverallStatus
        }
    }
    
    Write-ColorText "" "White"
    Write-ColorText "=== 检查完成 ===" "Cyan"
    Write-ColorText "文档总数: $CheckCount" "White"
    Write-ColorText "发现问题: $IssueCount" "Yellow"
    Write-ColorText "自动修复: $FixCount" "Green"
    Write-ColorText "检查用时: $((Get-Date) - $StartTime)" "White"
    
    # 生成报告
    New-QualityReport $Results
    
    Write-Log "Document quality check completed. Issues found: $IssueCount, Auto-fixed: $FixCount"
    
    return $Results
}

# 执行检查
try {
    Start-QualityCheck
} catch {
    Write-Log "Script execution failed: $($_.Exception.Message)" "ERROR"
    exit 1
}
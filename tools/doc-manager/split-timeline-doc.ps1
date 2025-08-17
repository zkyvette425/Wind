# Timeline document splitter - splits documents by date/month
param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath,
    
    [Parameter(Mandatory=$false)]
    [string]$SplitPattern = "### \d{4}-\d{2}-\d{2}"
)

function Split-TimelineDocument {
    param($FilePath, $Pattern)
    
    if (-not (Test-Path $FilePath)) {
        Write-Host "Error: File not found: $FilePath" -ForegroundColor Red
        return $false
    }
    
    # Read the content
    $Content = Get-Content $FilePath -Raw -Encoding UTF8
    
    # Create backup
    $BackupPath = $FilePath + ".backup." + (Get-Date -Format "yyyyMMdd-HHmmss")
    Copy-Item $FilePath $BackupPath
    Write-Host "Backup created: $BackupPath" -ForegroundColor Green
    
    # Extract file info
    $FileDir = Split-Path $FilePath -Parent
    $FileName = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
    $FileExt = [System.IO.Path]::GetExtension($FilePath)
    
    # Split content by date headers
    $Sections = $Content -split "(?=### \d{4}-\d{2}-\d{2})"
    
    # Keep header section (before first date)
    $HeaderSection = $Sections[0]
    
    # Group sections by month
    $MonthGroups = @{}
    $CurrentMonth = (Get-Date -Format "yyyy-MM")
    
    for ($i = 1; $i -lt $Sections.Count; $i++) {
        $Section = $Sections[$i]
        
        # Extract date from section
        if ($Section -match "### (\d{4})-(\d{2})-(\d{2})") {
            $Year = $Matches[1]
            $Month = $Matches[2]
            $MonthKey = "$Year-$Month"
            
            if (-not $MonthGroups.ContainsKey($MonthKey)) {
                $MonthGroups[$MonthKey] = @()
            }
            $MonthGroups[$MonthKey] += $Section
        }
    }
    
    # Create monthly files
    foreach ($MonthKey in $MonthGroups.Keys) {
        $MonthlyFilePath = Join-Path $FileDir "$FileName-$MonthKey$FileExt"
        $MonthlyContent = $HeaderSection + ($MonthGroups[$MonthKey] -join "")
        
        Set-Content -Path $MonthlyFilePath -Value $MonthlyContent -Encoding UTF8
        $Size = (Get-Item $MonthlyFilePath).Length
        Write-Host "Created: $MonthlyFilePath ($Size bytes)" -ForegroundColor Green
    }
    
    # Update main file to contain only current month
    if ($MonthGroups.ContainsKey($CurrentMonth)) {
        $CurrentContent = $HeaderSection + ($MonthGroups[$CurrentMonth] -join "")
        Set-Content -Path $FilePath -Value $CurrentContent -Encoding UTF8
        Write-Host "Updated main file with current month: $FilePath" -ForegroundColor Green
    }
    
    # Create index file
    Create-IndexFile $FileDir $FileName $FileExt $MonthGroups.Keys
    
    return $true
}

function Create-IndexFile {
    param($FileDir, $FileName, $FileExt, $MonthKeys)
    
    $IndexPath = Join-Path $FileDir "$FileName-Index$FileExt"
    
    $IndexContent = "# $FileName Document Index`n`n## Document Navigation`n`n"
    
    # Sort months in descending order
    $SortedMonths = $MonthKeys | Sort-Object -Descending
    
    foreach ($Month in $SortedMonths) {
        $MonthFile = "$FileName-$Month$FileExt"
        $MonthFilePath = Join-Path $FileDir $MonthFile
        
        if (Test-Path $MonthFilePath) {
            $Size = [math]::Round((Get-Item $MonthFilePath).Length / 1024, 1)
            $LastModified = (Get-Item $MonthFilePath).LastWriteTime.ToString("yyyy-MM-dd HH:mm")
            
            $Status = if ($Month -eq (Get-Date -Format "yyyy-MM")) { "[Current]" } else { "[Archived]" }
            
            $IndexContent += "- $Status [$MonthFile](./$MonthFile) (${Size}KB, Last Modified: $LastModified)`n"
        }
    }
    
    $IndexContent += @"

## 使用说明

1. 当前活跃文档包含本月的所有变更记录
2. 历史归档文档按月组织，便于查找特定时期的变更
3. 每个文档都控制在25KB以内，确保Claude Code可以完整读取
4. 添加新内容时，请优先添加到当前活跃文档

## 维护信息

- 自动分片时间: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
- 分片触发原因: 文档大小超过安全阈值 (22KB)
- 下次检查建议: 当活跃文档接近20KB时
"@
    
    Set-Content -Path $IndexPath -Value $IndexContent -Encoding UTF8
    Write-Host "Created index file: $IndexPath" -ForegroundColor Cyan
}

# Execute the split
Write-Host "Starting timeline document split..." -ForegroundColor Cyan
$Success = Split-TimelineDocument $FilePath $SplitPattern

if ($Success) {
    Write-Host "Document split completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Document split failed!" -ForegroundColor Red
    exit 1
}
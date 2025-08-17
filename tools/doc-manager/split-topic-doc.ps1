# Topic-based document splitter - splits documents by technical topics
param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath,
    
    [Parameter(Mandatory=$false)]
    [string[]]$Topics = @("MongoDB", "Orleans", "Docker", "网络配置", "性能优化")
)

function Split-TopicDocument {
    param($FilePath, $Topics)
    
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
    
    # Split content by topic headers (### [icon] 案例X: TopicName)
    $Sections = $Content -split "(?=### [🚧🚨🔍] 案例\d+:)"
    
    # Keep header section (before first topic)
    $HeaderSection = $Sections[0]
    
    # Group sections by topic
    $TopicGroups = @{}
    $GeneralSections = @()
    
    for ($i = 1; $i -lt $Sections.Count; $i++) {
        $Section = $Sections[$i]
        
        # Find which topic this section belongs to
        $AssignedTopic = "General"
        foreach ($Topic in $Topics) {
            if ($Section -match $Topic) {
                $AssignedTopic = $Topic
                break
            }
        }
        
        if ($AssignedTopic -eq "General") {
            $GeneralSections += $Section
        } else {
            if (-not $TopicGroups.ContainsKey($AssignedTopic)) {
                $TopicGroups[$AssignedTopic] = @()
            }
            $TopicGroups[$AssignedTopic] += $Section
        }
    }
    
    # Create topic-based files
    foreach ($Topic in $TopicGroups.Keys) {
        $TopicFileName = $Topic -replace '[^\w\-]', ''  # Remove special characters
        $TopicFilePath = Join-Path $FileDir "archives" "$FileName-$TopicFileName$FileExt"
        
        # Ensure archives directory exists
        $ArchivesDir = Join-Path $FileDir "archives"
        if (-not (Test-Path $ArchivesDir)) {
            New-Item -ItemType Directory -Path $ArchivesDir -Force | Out-Null
        }
        
        $TopicContent = $HeaderSection + ($TopicGroups[$Topic] -join "")
        
        Set-Content -Path $TopicFilePath -Value $TopicContent -Encoding UTF8
        $Size = (Get-Item $TopicFilePath).Length
        Write-Host "Created: $TopicFilePath ($Size bytes)" -ForegroundColor Green
    }
    
    # Update main file to contain only header + general sections + recent topics
    $RecentContent = $HeaderSection
    if ($GeneralSections.Count -gt 0) {
        $RecentContent += ($GeneralSections -join "")
    }
    
    # Keep the most recent topic case for reference
    if ($TopicGroups.Count -gt 0) {
        $MostRecentTopic = $TopicGroups.Keys | Select-Object -First 1
        $RecentContent += ($TopicGroups[$MostRecentTopic] | Select-Object -Last 1)
    }
    
    Set-Content -Path $FilePath -Value $RecentContent -Encoding UTF8
    Write-Host "Updated main file with header and recent content: $FilePath" -ForegroundColor Green
    
    # Create index file
    Create-TopicIndexFile $FileDir $FileName $FileExt $TopicGroups.Keys
    
    return $true
}

function Create-TopicIndexFile {
    param($FileDir, $FileName, $FileExt, $TopicKeys)
    
    $IndexPath = Join-Path $FileDir "$FileName-Index$FileExt"
    
    $IndexContent = "# $FileName Document Index`n`n## Topic Navigation`n`n"
    
    # Add main file
    $MainFile = "$FileName$FileExt"
    $MainFilePath = Join-Path $FileDir $MainFile
    if (Test-Path $MainFilePath) {
        $Size = [math]::Round((Get-Item $MainFilePath).Length / 1024, 1)
        $LastModified = (Get-Item $MainFilePath).LastWriteTime.ToString("yyyy-MM-dd HH:mm")
        $IndexContent += "- [Current] [$MainFile](./$MainFile) (${Size}KB, Last Modified: $LastModified)`n"
    }
    
    # Add topic files
    foreach ($Topic in $TopicKeys) {
        $TopicFileName = $Topic -replace '[^\w\-]', ''
        $TopicFile = "$FileName-$TopicFileName$FileExt"
        $TopicFilePath = Join-Path $FileDir "archives" $TopicFile
        
        if (Test-Path $TopicFilePath) {
            $Size = [math]::Round((Get-Item $TopicFilePath).Length / 1024, 1)
            $LastModified = (Get-Item $TopicFilePath).LastWriteTime.ToString("yyyy-MM-dd HH:mm")
            
            $IndexContent += "- [Topic: $Topic] [archives/$TopicFile](./archives/$TopicFile) (${Size}KB, Last Modified: $LastModified)`n"
        }
    }
    
    $IndexContent += "`n## Usage Instructions`n`n"
    $IndexContent += "1. Current file contains recent research and general topics`n"
    $IndexContent += "2. Topic archives are organized by technical domain for easy reference`n"
    $IndexContent += "3. Each file is kept under 25KB for Claude Code compatibility`n"
    $IndexContent += "4. Add new research to the current file or appropriate topic archive`n`n"
    
    $IndexContent += "## Maintenance Info`n`n"
    $IndexContent += "- Auto-split time: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")`n"
    $IndexContent += "- Split trigger: Document size exceeded safety threshold (22KB)`n"
    $IndexContent += "- Next check: When current file approaches 20KB`n"
    
    Set-Content -Path $IndexPath -Value $IndexContent -Encoding UTF8
    Write-Host "Created index file: $IndexPath" -ForegroundColor Cyan
}

# Execute the split
Write-Host "Starting topic-based document split..." -ForegroundColor Cyan
$Success = Split-TopicDocument $FilePath $Topics

if ($Success) {
    Write-Host "Document split completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Document split failed!" -ForegroundColor Red
    exit 1
}
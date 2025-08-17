param([string]$FilePath)

$SafeThreshold = 22000
$MaxLimit = 25000

if (-not (Test-Path $FilePath)) {
    Write-Host "File not found: $FilePath" -ForegroundColor Red
    exit 1
}

$Size = (Get-Item $FilePath).Length
$Remaining = $SafeThreshold - $Size

Write-Host "=================================="
Write-Host "Document Size Check Report"
Write-Host "=================================="
Write-Host "File: $FilePath"
Write-Host "Current size: $Size bytes"

if ($Size -gt $MaxLimit) {
    Write-Host "Status: CRITICAL - Exceeds Read tool limit!" -ForegroundColor Red
    $Status = "CRITICAL"
} elseif ($Size -gt $SafeThreshold) {
    Write-Host "Status: WARNING - Approaching capacity limit" -ForegroundColor Yellow
    $Status = "WARNING"
} else {
    Write-Host "Status: OK - Sufficient capacity" -ForegroundColor Green
    $Status = "OK"
}

Write-Host "Remaining safe capacity: $Remaining bytes"
Write-Host "=================================="
Write-Host "RESULT:$Status"

switch ($Status) {
    "CRITICAL" { exit 3 }
    "WARNING" { exit 1 }
    "OK" { exit 0 }
}
# Wind v1.3 MagicOnion验证脚本
param(
    [string]$ServerUrl = "http://localhost:5271"
)

Write-Host "=== Wind v1.3 MagicOnion验证 ===" -ForegroundColor Green
Write-Host "服务器地址: $ServerUrl" -ForegroundColor Yellow

# 检查服务器是否响应
try {
    Write-Host "`n1. 检查服务器根路径..." -ForegroundColor Cyan
    $response = Invoke-RestMethod -Uri $ServerUrl -Method GET -TimeoutSec 5
    Write-Host "✅ 服务器响应正常:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 3
    
    Write-Host "`n2. 检查健康检查端点..." -ForegroundColor Cyan
    $healthResponse = Invoke-RestMethod -Uri "$ServerUrl/health" -Method GET -TimeoutSec 5
    Write-Host "✅ 健康检查: $healthResponse" -ForegroundColor Green
    
    Write-Host "`n✅ MagicOnion服务验证通过!" -ForegroundColor Green
    return $true
}
catch {
    Write-Host "❌ 验证失败: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   请确保服务器正在运行: dotnet run --project Wind.Server\Wind.Server.csproj" -ForegroundColor Yellow
    return $false
}
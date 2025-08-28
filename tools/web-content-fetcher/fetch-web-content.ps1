# ================================================================
# 网页内容获取工具 - PowerShell版本 v1.0.0
#
# 用途: 获取指定URL的网页内容，供Claude Code分析使用
# 作者: Wind项目开发组  
# 创建时间: 2025-08-28
# ================================================================

param(
    [Parameter(Position=0, Mandatory=$true)]
    [string]$Url,
    
    [Parameter()]
    [string]$OutputFile = "",
    
    [Parameter()]
    [switch]$CleanHtml,
    
    [Parameter()]
    [int]$TimeoutSeconds = 30,
    
    [Parameter()]
    [switch]$Help
)

function Show-Help {
    Write-Host @"
================================================================
网页内容获取工具 - PowerShell版本
================================================================

用法: 
  .\fetch-web-content.ps1 <URL> [选项]

参数:
  <URL>                    要获取的网页URL (必需)

选项:
  -OutputFile <file>       指定输出文件名
  -CleanHtml               清理HTML标签，输出纯文本
  -TimeoutSeconds <sec>    设置超时时间 (默认: 30秒)
  -Help                    显示此帮助信息

示例:
  .\fetch-web-content.ps1 "https://github.com/Cysharp/MagicOnion"
  .\fetch-web-content.ps1 "https://magiconion.com/docs" -OutputFile "magiconion-docs.txt" -CleanHtml
  .\fetch-web-content.ps1 "https://example.com" -TimeoutSeconds 60

注意事项:
  - 使用.NET HttpClient进行网络请求
  - 输出文件保存在当前目录
  - 支持重定向和内容解压缩
  - 自动处理字符编码

================================================================
"@
    exit 0
}

function Clean-Html {
    param([string]$HtmlContent)
    
    # 移除HTML标签
    $cleaned = $HtmlContent -replace '<[^>]*>', ''
    
    # 解码HTML实体
    $cleaned = $cleaned -replace '&nbsp;', ' '
    $cleaned = $cleaned -replace '&lt;', '<'
    $cleaned = $cleaned -replace '&gt;', '>'
    $cleaned = $cleaned -replace '&amp;', '&'
    $cleaned = $cleaned -replace '&quot;', '"'
    $cleaned = $cleaned -replace '&#39;', "'"
    
    # 清理多余的空白字符
    $cleaned = $cleaned -replace '\s+', ' '
    $cleaned = $cleaned.Trim()
    
    return $cleaned
}

function Get-WebContent {
    param([string]$Url, [int]$TimeoutSeconds)
    
    try {
        # 创建HttpClient
        $httpClient = New-Object System.Net.Http.HttpClient
        $httpClient.Timeout = New-TimeSpan -Seconds $TimeoutSeconds
        
        # 设置请求头
        $httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
        $httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8")
        $httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8")
        $httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate")
        
        # 发送请求
        Write-Host "正在获取网页内容..." -ForegroundColor Yellow
        $response = $httpClient.GetAsync($Url).Result
        
        if ($response.IsSuccessStatusCode) {
            $content = $response.Content.ReadAsStringAsync().Result
            return $content
        } else {
            throw "HTTP请求失败: $($response.StatusCode) - $($response.ReasonPhrase)"
        }
    }
    catch {
        throw "获取网页内容失败: $($_.Exception.Message)"
    }
    finally {
        if ($httpClient) {
            $httpClient.Dispose()
        }
    }
}

# 主程序逻辑
if ($Help) {
    Show-Help
}

if (-not $Url) {
    Write-Host "错误: 需要指定URL参数" -ForegroundColor Red
    Show-Help
}

# 设置默认输出文件名
if (-not $OutputFile) {
    $timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm"
    $OutputFile = "web-content-$timestamp.txt"
}

Write-Host @"
================================================================
网页内容获取工具
================================================================
URL: $Url
输出文件: $OutputFile
清理HTML: $CleanHtml
超时时间: ${TimeoutSeconds}秒
================================================================
"@ -ForegroundColor Cyan

try {
    # 获取网页内容
    $webContent = Get-WebContent -Url $Url -TimeoutSeconds $TimeoutSeconds
    
    # 创建输出文件头部
    $header = @"
================================================================
网页内容获取结果
================================================================
URL: $Url
获取时间: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
原始大小: $($webContent.Length) 字符
内容处理: $(if ($CleanHtml) { "HTML标签已清理" } else { "保持原始HTML格式" })
================================================================

"@
    
    # 处理内容
    if ($CleanHtml) {
        Write-Host "正在清理HTML标签..." -ForegroundColor Yellow
        $processedContent = Clean-Html -HtmlContent $webContent
    } else {
        $processedContent = $webContent
    }
    
    # 写入文件
    $fullContent = $header + $processedContent
    $fullContent | Out-File -FilePath $OutputFile -Encoding UTF8
    
    # 显示结果
    $outputSize = (Get-Item $OutputFile).Length
    Write-Host @"

================================================================
获取完成！
================================================================
输出文件: $OutputFile
文件大小: $outputSize 字节

建议后续操作:
1. 使用Read工具查看获取的内容
2. 根据需要调整清理选项重新获取
3. 将有用信息整理到项目文档中
================================================================
"@ -ForegroundColor Green
}
catch {
    Write-Host "错误: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
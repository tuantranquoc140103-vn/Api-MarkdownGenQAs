# ========= SET UTF-8 =========
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Ép console sang UTF-8 (giúp hiển thị icon và tiếng Việt)
try { chcp 65001 > $null } catch {}

# ========= CONFIG =========
$baseUrl = "https://integrate.api.nvidia.com/v1"
$model   = "mistralai/ministral-14b-instruct-2512"
$apiKey  = "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" # Thay API Key thật của bạn

# ========= HEADERS =========
$headers = @{
    "Authorization" = "Bearer $apiKey"
    "Accept"        = "application/json"
}

# ========= PAYLOAD (CÁCH B - NVEXT STRUCTURE) =========
$choices = @("Hà Nội", "Paris", "New York")

$payloadObj = @{
    model = $model
    messages = @(
        @{
            role    = "user"
            content = "đâu là thủ đô của việt nam ? Only return item in the format (Hà nội, Paris, New York)"
        }
    )
    # Đây là phần "extra_body" được chuyển sang PowerShell Hashtable
    nvext = @{
        guided_choice = $choices
    }
    temperature = 0.15 # Để 0 để test tính chuẩn xác của Guided Choice
    max_tokens  = 50
}

# Chuyển thành JSON (Sử dụng -Compress để tránh lỗi ký tự lạ)
$payloadJson = $payloadObj | ConvertTo-Json -Depth 10 -Compress

# ========= SEND =========
Write-Host "🚀 Đang gửi request (Cấu trúc B - nvext)..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod `
        -Method Post `
        -Uri "$baseUrl/chat/completions" `
        -Headers $headers `
        -ContentType "application/json; charset=utf-8" `
        -Body ([Text.Encoding]::UTF8.GetBytes($payloadJson))

    # ========= READ =========
    $content = $response.choices[0].message.content
    
    Write-Host "✅ Kết quả trả về:" -ForegroundColor Green
    Write-Host "--------------------"
    Write-Host $content
    Write-Host "--------------------"
}
catch {
    Write-Host "❌ Lỗi thực thi:" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        Write-Host "Chi tiết lỗi từ Server: $errorBody" -ForegroundColor Yellow
    } else {
        $_.Exception.Message
    }
}
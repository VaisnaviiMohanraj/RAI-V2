# RR Realty AI - Quick Deploy Script
# Version 1.0 - Production Ready
# Deploy to: rrai-test slot

Write-Host "🚀 Deploying RR Realty AI v1.0..."

$ZipPath = "rr-realty-ai-v1.0.zip"

if (!(Test-Path $ZipPath)) {
    Write-Host " Deployment package not found: $ZipPath"
    exit 1
}

Write-Host "Deploying to rrai-test slot..."
$creds = az webapp deployment list-publishing-credentials -g rg-innovation -n site-net --slot rrai-test | ConvertFrom-Json
$pair = "$($creds.publishingUserName):$($creds.publishingPassword)"
$basic = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($pair))
$kudu = "https://site-net-rrai-test-ambjbdbvdwcffhat.scm.centralus-01.azurewebsites.net"

az webapp stop -g rg-innovation -n site-net --slot rrai-test
Start-Sleep -Seconds 5

Invoke-RestMethod -Uri "$kudu/api/zip/site/wwwroot/" `
  -Headers @{Authorization="Basic $basic";"Content-Type"="application/zip"} `
  -Method PUT `
  -InFile (Resolve-Path $ZipPath) `
  -TimeoutSec 300

az webapp start -g rg-innovation -n site-net --slot rrai-test

Write-Host ""
Write-Host " DEPLOYMENT COMPLETE!"
Write-Host ""
Write-Host "Test at: https://testing.rrrealty.ai/"
Write-Host ""
Write-Host "Verify:"
Write-Host "  - Health check: https://testing.rrrealty.ai/api/health"
Write-Host "  - Send test message"
Write-Host "  - Check streaming response"
Write-Host "  - Verify conversation save"

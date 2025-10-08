# RR Realty AI - Production Deployment Guide

## Working Deployment Method (As of 2025-09-30)

### Problem
- Standard OneDeploy fails with ChangeSetId errors
- az webapp deploy returns 500 errors
- Deployment system state is corrupted

### Solution: Direct Kudu ZIP Upload

```powershell
cd c:\local\chat\rai-realty-ai

# 1. Clean rebuild
Remove-Item -Recurse -Force Frontend\dist,Backend\bin,Backend\obj,Backend\publish,*.zip -ErrorAction SilentlyContinue
cd Frontend && npm run build && cd ..
cd Backend && dotnet publish -c Release -o publish --nologo && cd ..
New-Item -ItemType Directory -Path Backend\publish\wwwroot -Force | Out-Null
Copy-Item -Recurse Frontend\dist\* Backend\publish\wwwroot\ -Force
Compress-Archive -Path Backend\publish\* -DestinationPath deploy.zip -Force

# 2. Get Kudu credentials
$creds = az webapp deployment list-publishing-credentials -g rg-innovation -n site-net | ConvertFrom-Json
$pair = "$($creds.publishingUserName):$($creds.publishingPassword)"
$basic = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($pair))
$kudu = "https://site-net-ajdhc8cngbgrfbhe.scm.centralus-01.azurewebsites.net"

# 3. Stop app and deploy
az webapp stop -g rg-innovation -n site-net
Start-Sleep -Seconds 5

Invoke-RestMethod -Uri "$kudu/api/zip/site/wwwroot/" `
  -Headers @{Authorization="Basic $basic";"Content-Type"="application/zip"} `
  -Method PUT `
  -InFile "deploy.zip" `
  -TimeoutSec 300

az webapp start -g rg-innovation -n site-net
```

## Critical Configuration

### Frontend - authConfig.ts
- MUST use window.location.origin (dynamic)
- NO hardcoded testing.rrrealty.ai

### Backend - Program.cs CORS
- Production: ONLY allow https://www.rrrealty.ai
- Test slot: Can include testing.rrrealty.ai

### Easy Auth
- MUST be disabled on production slot
- App uses MSAL (client-side auth), not Easy Auth

## Resources
- Production: https://www.rrrealty.ai/
- App Service: site-net (rg-innovation)
- Kudu: https://site-net-ajdhc8cngbgrfbhe.scm.centralus-01.azurewebsites.net

# 🚨 QUICK FIX: Enable Azure Function Conversation Save

## The Problem
**Conversations are not being saved** because the `AZURE_FUNCTION_URL` environment variable is missing in Azure App Service.

## The Fix (5 Minutes)

### Step 1: Get Function Code
```powershell
# Option A: Azure Portal
1. Go to: https://portal.azure.com
2. Navigate to: fn-conversationsave Function App
3. Click: Functions → [your function] → Function Keys
4. Copy the "default" key

# Option B: Azure CLI
az functionapp keys list --name fn-conversationsave --resource-group rg-innovate --query functionKeys.default -o tsv
```

### Step 2: Set Environment Variable
```powershell
# Azure CLI (Fastest)
az webapp config appsettings set `
  --name site-net `
  --resource-group rg-innovation `
  --slot rrai-blue `
  --settings AZURE_FUNCTION_URL="https://fn-conversationsave.azurewebsites.net/api/conversations/update?code=YOUR_FUNCTION_CODE_HERE"

# Or via Azure Portal:
1. Go to: site-net App Service
2. Navigate to: Configuration → Application settings
3. Click: + New application setting
4. Name: AZURE_FUNCTION_URL
5. Value: https://fn-conversationsave.azurewebsites.net/api/conversations/update?code=YOUR_CODE
6. Click: OK → Save
7. Restart the App Service
```

### Step 3: Verify
```powershell
# Check logs
az webapp log tail --name site-net --resource-group rg-innovation --slot rrai-blue

# Look for:
# ✅ "Azure Function URL configured (Save endpoint source): https://fn-conversationsave..."
# ✅ "Calling Azure Function: https://fn-conversationsave..."
# ✅ "Azure Function responded with status: 200"
```

## What This Does
- Enables conversation persistence to SQL database
- Allows users to resume conversations across sessions
- Provides audit logging for all chat interactions

## If You Don't Have the Function Code
Contact the Azure admin or check:
- Azure Key Vault (if keys are stored there)
- Previous deployment documentation
- FINALSUMMARY.md (may have redacted version)

## Alternative: Use Local Development Default
If you just want to test without persistence:
- The app will work fine without this variable
- Conversations will only persist in memory (lost on restart)
- No SQL audit logging will occur

## Deployment Slots
Remember to set this variable in **ALL** slots:
- ✅ `site-net-rrai-blue` (development)
- ✅ `site-net-rrai-stage` (staging)
- ✅ Any other slots

Each slot needs its own configuration!

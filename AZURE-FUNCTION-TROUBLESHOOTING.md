# Azure Function Conversation Save - Troubleshooting Guide

## Problem: Conversations Not Being Saved

### Root Cause
The backend API is not calling the Azure Function because the `AZURE_FUNCTION_URL` environment variable is **not configured** in Azure App Service.

### Current Behavior
- Backend uses hardcoded fallback: `https://fn-conversationsave.azurewebsites.net/api/SaveConversation`
- This endpoint **does not exist** on the deployed Azure Function
- Actual endpoint is: `https://fn-conversationsave.azurewebsites.net/api/conversations/update`

## Solution: Configure Environment Variable in Azure

### Step 1: Get the Function Code
1. Go to Azure Portal → `fn-conversationsave` Function App
2. Navigate to **Functions** → Select the conversation save function
3. Click **Function Keys** → Copy the **default** or **master** key

### Step 2: Set Environment Variable in App Service
1. Go to Azure Portal → `site-net` App Service
2. Navigate to **Configuration** → **Application settings**
3. Click **+ New application setting**
4. Add:
   - **Name**: `AZURE_FUNCTION_URL`
   - **Value**: `https://fn-conversationsave.azurewebsites.net/api/conversations/update?code=YOUR_FUNCTION_CODE`
5. Click **OK** → **Save**
6. **Restart** the App Service

### Step 3: Verify in Logs
1. Go to App Service → **Log stream**
2. Send a chat message
3. Look for log entries:
   ```
   Azure Function URL configured (Save endpoint source): https://fn-conversationsave.azurewebsites.net/api/conversations/update?code=***
   Calling Azure Function: https://fn-conversationsave.azurewebsites.net/api/conversations/update?code=***, MessageCount: X
   Azure Function responded with status: 200
   ```

## Alternative: Update Backend Code

If the Azure Function endpoint structure is different, you can update `AzureFunctionService.cs`:

### Current Default (Line 24):
```csharp
_azureFunctionUrl = Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL") ?? "https://fn-conversationsave.azurewebsites.net/api/SaveConversation";
```

### Update to Correct Endpoint:
```csharp
_azureFunctionUrl = Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL") ?? "https://fn-conversationsave.azurewebsites.net/api/conversations/update";
```

## Verification Checklist

- [ ] `AZURE_FUNCTION_URL` environment variable is set in Azure App Service
- [ ] Function code/key is included in the URL (if required)
- [ ] App Service has been restarted after configuration change
- [ ] Logs show "Azure Function responded with status: 200"
- [ ] Conversations are being saved (check SQL database or Function logs)

## Common Issues

### Issue 1: 401 Unauthorized
- **Cause**: Missing or invalid function code in URL
- **Fix**: Verify function key is correct and included in `AZURE_FUNCTION_URL`

### Issue 2: 404 Not Found
- **Cause**: Wrong endpoint path
- **Fix**: Verify the exact function endpoint path in Azure Portal

### Issue 3: Timeout
- **Cause**: Function cold start or network issues
- **Fix**: Backend already has 30-second timeout configured; check Function App logs

### Issue 4: No Logs Showing Function Calls
- **Cause**: Environment variable not set, using wrong fallback
- **Fix**: Set `AZURE_FUNCTION_URL` in App Service configuration

## Testing Locally

To test the Azure Function integration locally:

1. Get the production function URL with code
2. Create `.env` file in `Backend/` directory:
   ```
   AZURE_FUNCTION_URL=https://fn-conversationsave.azurewebsites.net/api/conversations/update?code=YOUR_CODE
   ```
3. Run backend: `dotnet run`
4. Send chat messages and check console logs

## Production Deployment Slots

Remember to set `AZURE_FUNCTION_URL` in **all deployment slots**:
- `site-net-rrai-blue` (development slot)
- `site-net-rrai-stage` (staging slot)
- Any other slots you're using

Each slot needs its own configuration!

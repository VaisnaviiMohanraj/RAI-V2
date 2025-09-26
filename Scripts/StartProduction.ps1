# Production Testing Script for RR Realty AI
# This script starts both backend and frontend in production mode

Write-Host "Starting RR Realty AI in Production Mode..." -ForegroundColor Green

# Check if .env file exists
$envFile = "c:\Build\RAI Realty AI\Backend\.env"
if (-not (Test-Path $envFile)) {
    Write-Host "ERROR: .env file not found at $envFile" -ForegroundColor Red
    Write-Host "Please ensure all environment variables are configured." -ForegroundColor Yellow
    exit 1
}

# Start Backend in Production Mode
Write-Host "`nStarting Backend (Production Mode)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'c:\Build\RAI Realty AI\Backend'; dotnet run --environment Production"

# Wait for backend to start
Write-Host "Waiting for backend to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Start Frontend in Production Mode
Write-Host "`nStarting Frontend (Production Mode)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'c:\Build\RAI Realty AI\Frontend'; npm run dev -- --config vite.config.production.ts"

Write-Host "`n=== Production Testing Setup Complete ===" -ForegroundColor Green
Write-Host "Backend: https://site-net-rrai-blue-fsgabaardkdhhnhf.centralus-01.azurewebsites.net" -ForegroundColor White
Write-Host "Frontend: https://testing.rrrealty.ai" -ForegroundColor White
Write-Host "Swagger: https://site-net-rrai-blue-fsgabaardkdhhnhf.centralus-01.azurewebsites.net/swagger" -ForegroundColor White
Write-Host "`nAuthentication: Microsoft Identity (MSAL)" -ForegroundColor Yellow
Write-Host "Environment: Production" -ForegroundColor Yellow
Write-Host "`nPress any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

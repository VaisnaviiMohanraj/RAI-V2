# Setup Production Environment Variables
# This script sets up the required environment variables for RR Realty AI production deployment
# Run this script before starting the production server

param(
    [switch]$Persistent,
    [switch]$Verify
)

Write-Host "RR Realty AI Production Environment Setup" -ForegroundColor Magenta
Write-Host "=========================================" -ForegroundColor Magenta

# Check if .env.production.template exists
if (-not (Test-Path ".env.production.template")) {
    Write-Host "Error: .env.production.template not found!" -ForegroundColor Red
    Write-Host "Please ensure the template file exists in the project root." -ForegroundColor Red
    exit 1
}

# Read environment variables from template
$envVars = @{}
Get-Content ".env.production.template" | ForEach-Object {
    if ($_ -match "^([^#][^=]+)=(.*)$") {
        $envVars[$matches[1]] = $matches[2]
    }
}

Write-Host "`nSetting up environment variables..." -ForegroundColor Cyan

# Set environment variables for current session
foreach ($key in $envVars.Keys) {
    $value = $envVars[$key]
    if ($value) {
        [Environment]::SetEnvironmentVariable($key, $value, "Process")
        
        if ($Persistent) {
            [Environment]::SetEnvironmentVariable($key, $value, "User")
            Write-Host "[PERSISTENT] $key = $($value.Substring(0, [Math]::Min(10, $value.Length)))..." -ForegroundColor Green
        } else {
            Write-Host "[SESSION] $key = $($value.Substring(0, [Math]::Min(10, $value.Length)))..." -ForegroundColor Yellow
        }
    }
}

if ($Verify) {
    Write-Host "`nVerifying environment variables..." -ForegroundColor Cyan
    
    $requiredVars = @(
        "AZURE_OPENAI_ENDPOINT",
        "AZURE_OPENAI_API_KEY", 
        "AZURE_OPENAI_DEPLOYMENT",
        "AZURE_TENANT_ID",
        "AZURE_CLIENT_ID",
        "AZURE_CLIENT_SECRET",
        "AZURE_DOMAIN"
    )
    
    $allSet = $true
    foreach ($var in $requiredVars) {
        $value = [Environment]::GetEnvironmentVariable($var)
        if ($value) {
            Write-Host "[`u{2713}] $var is set" -ForegroundColor Green
        } else {
            Write-Host "[`u{2717}] $var is missing" -ForegroundColor Red
            $allSet = $false
        }
    }
    
    if ($allSet) {
        Write-Host "`nAll required environment variables are configured!" -ForegroundColor Green
        Write-Host "You can now run the production checklist: .\Scripts\ProductionChecklist.ps1" -ForegroundColor Cyan
    } else {
        Write-Host "`nSome environment variables are missing. Please check the configuration." -ForegroundColor Red
    }
}

Write-Host "`nEnvironment setup complete!" -ForegroundColor Green
Write-Host "Use -Persistent flag to save variables permanently" -ForegroundColor Gray
Write-Host "Use -Verify flag to check all required variables" -ForegroundColor Gray

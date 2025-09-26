# RR Realty AI Deploy Script
# Deploys application to Azure App Service from Git source

param(
    [string]$ResourceGroup = "",
    [string]$AppName = "",
    [string]$GitUrl = "",
    [string]$Branch = "main",
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== RR Realty AI Deploy Script ===" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Yellow
Write-Host "App Name: $AppName" -ForegroundColor Yellow
Write-Host "Git URL: $GitUrl" -ForegroundColor Yellow
Write-Host "Branch: $Branch" -ForegroundColor Yellow

# Validate parameters
if ([string]::IsNullOrEmpty($ResourceGroup) -or [string]::IsNullOrEmpty($AppName) -or [string]::IsNullOrEmpty($GitUrl)) {
    Write-Host "ERROR: ResourceGroup, AppName, and GitUrl parameters are required" -ForegroundColor Red
    Write-Host "Usage: .\Deploy.ps1 -ResourceGroup 'rg-rrrealty-ai' -AppName 'app-rrrealty-ai' -GitUrl 'https://github.com/user/repo.git'" -ForegroundColor Yellow
    exit 1
}

try {
    # Check Azure CLI
    Write-Host "`n--- Checking Prerequisites ---" -ForegroundColor Cyan
    az --version | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Azure CLI not found. Please install Azure CLI." }
    
    # Check login status
    $account = az account show 2>$null | ConvertFrom-Json
    if (-not $account) {
        Write-Host "Please login to Azure CLI first:" -ForegroundColor Yellow
        Write-Host "az login" -ForegroundColor White
        exit 1
    }
    Write-Host "Logged in as: $($account.user.name)" -ForegroundColor White
    
    # Configure Git deployment
    Write-Host "`n--- Configuring Git Deployment ---" -ForegroundColor Cyan
    Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
    Write-Host "App Service: $AppName" -ForegroundColor White
    Write-Host "Git Repository: $GitUrl" -ForegroundColor White
    Write-Host "Branch: $Branch" -ForegroundColor White
    
    # Configure deployment source
    Write-Host "Setting up Git deployment source..." -ForegroundColor White
    az webapp deployment source config --resource-group $ResourceGroup --name $AppName --repo-url $GitUrl --branch $Branch --manual-integration
    if ($LASTEXITCODE -ne 0) { throw "Git deployment configuration failed" }
    
    # Trigger deployment
    Write-Host "Triggering deployment from Git..." -ForegroundColor White
    az webapp deployment source sync --resource-group $ResourceGroup --name $AppName
    if ($LASTEXITCODE -ne 0) { throw "Git deployment sync failed" }
    
    # Get app URL
    $appUrl = az webapp show --resource-group $ResourceGroup --name $AppName --query "defaultHostName" --output tsv
    
    Write-Host "`n=== Deployment Completed Successfully ===" -ForegroundColor Green
    Write-Host "Application URL: https://$appUrl" -ForegroundColor White
    Write-Host "Git Repository: $GitUrl" -ForegroundColor White
    Write-Host "Branch: $Branch" -ForegroundColor White
    Write-Host "`nNote: Azure will build the application from source using .deployment configuration" -ForegroundColor Yellow
    
} catch {
    Write-Host "`n=== Deployment Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Set-Location $ProjectRoot
}

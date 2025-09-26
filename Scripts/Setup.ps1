# RR Realty AI Setup Script
# Sets up development environment and dependencies

param(
    [switch]$SkipDotNet,
    [switch]$SkipNode,
    [switch]$SkipAzureCLI,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== RR Realty AI Setup Script ===" -ForegroundColor Green
Write-Host "Project Root: $ProjectRoot" -ForegroundColor Yellow

try {
    # Check .NET SDK
    if (-not $SkipDotNet) {
        Write-Host "`n--- Checking .NET SDK ---" -ForegroundColor Cyan
        try {
            $dotnetVersion = dotnet --version
            Write-Host ".NET SDK version: $dotnetVersion" -ForegroundColor White
            
            if ($dotnetVersion -lt "9.0") {
                Write-Warning ".NET 9.0 or higher is required. Please update your .NET SDK."
            }
        } catch {
            Write-Host "ERROR: .NET SDK not found. Please install .NET 9.0 SDK from https://dotnet.microsoft.com/download" -ForegroundColor Red
            exit 1
        }
    }
    
    # Check Node.js
    if (-not $SkipNode) {
        Write-Host "`n--- Checking Node.js ---" -ForegroundColor Cyan
        try {
            $nodeVersion = node --version
            $npmVersion = npm --version
            Write-Host "Node.js version: $nodeVersion" -ForegroundColor White
            Write-Host "npm version: $npmVersion" -ForegroundColor White
        } catch {
            Write-Host "ERROR: Node.js not found. Please install Node.js from https://nodejs.org/" -ForegroundColor Red
            exit 1
        }
    }
    
    # Check Azure CLI
    if (-not $SkipAzureCLI) {
        Write-Host "`n--- Checking Azure CLI ---" -ForegroundColor Cyan
        try {
            $azVersion = az --version | Select-Object -First 1
            Write-Host "Azure CLI: $azVersion" -ForegroundColor White
        } catch {
            Write-Host "WARNING: Azure CLI not found. Install from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Yellow
        }
    }
    
    # Create development configuration
    Write-Host "`n--- Setting up Development Configuration ---" -ForegroundColor Cyan
    $devConfigPath = "$ProjectRoot\Backend\appsettings.Development.json"
    
    if (-not (Test-Path $devConfigPath)) {
        $devConfig = @{
            "Logging" = @{
                "LogLevel" = @{
                    "Default" = "Information"
                    "Microsoft.AspNetCore" = "Warning"
                }
            }
            "AllowedHosts" = "*"
            "OpenAI" = @{
                "Endpoint" = "CONTACT_BRIAN_FOR_DETAILS"
                "ApiKey" = "CONTACT_BRIAN_FOR_DETAILS"
                "DeploymentName" = "gpt-4"
                "ApiVersion" = "2024-02-15-preview"
            }
            "EntraId" = @{
                "TenantId" = "CONTACT_BRIAN_FOR_DETAILS"
                "ClientId" = "CONTACT_BRIAN_FOR_DETAILS"
                "Instance" = "https://login.microsoftonline.com/"
            }
        }
        
        $devConfig | ConvertTo-Json -Depth 10 | Set-Content $devConfigPath
        Write-Host "Created development configuration: $devConfigPath" -ForegroundColor White
    }
    
    # Create secure upload directory
    Write-Host "`n--- Creating Secure Upload Directory ---" -ForegroundColor Cyan
    $uploadDir = Join-Path $env:APPDATA "RRRealtyAI\SecureUploads"
    if (-not (Test-Path $uploadDir)) {
        New-Item -Path $uploadDir -ItemType Directory -Force | Out-Null
        Write-Host "Created secure upload directory: $uploadDir" -ForegroundColor White
    }
    
    # Set permissions on upload directory (Windows)
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        try {
            icacls $uploadDir /inheritance:r /grant:r "$env:USERNAME:(OI)(CI)F" | Out-Null
            Write-Host "Set secure permissions on upload directory" -ForegroundColor White
        } catch {
            Write-Warning "Could not set secure permissions on upload directory"
        }
    }
    
    Write-Host "`n=== Setup Completed Successfully ===" -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "1. Contact Brian for Azure OpenAI and Entra ID credentials" -ForegroundColor White
    Write-Host "2. Update appsettings.Development.json with actual credentials" -ForegroundColor White
    Write-Host "3. Run .\Scripts\Build.ps1 to build the application" -ForegroundColor White
    Write-Host "4. Run .\Scripts\Test.ps1 to run tests" -ForegroundColor White
    
} catch {
    Write-Host "`n=== Setup Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Set-Location $ProjectRoot
}

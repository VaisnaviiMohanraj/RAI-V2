# RR Realty AI Environment Setup Script
# Sets environment variables for secure credential management

param(
    [Parameter(Mandatory=$true)]
    [string]$ApiKey,
    [string]$TenantId = "",
    [string]$ClientId = "",
    [switch]$Persistent
)

$ErrorActionPreference = "Stop"

Write-Host "=== RR Realty AI Environment Setup ===" -ForegroundColor Green

try {
    # Set Azure OpenAI API Key
    if ($Persistent) {
        [Environment]::SetEnvironmentVariable("AZURE_OPENAI_API_KEY", $ApiKey, "User")
        Write-Host "Set AZURE_OPENAI_API_KEY as persistent user environment variable" -ForegroundColor White
    } else {
        $env:AZURE_OPENAI_API_KEY = $ApiKey
        Write-Host "Set AZURE_OPENAI_API_KEY for current session" -ForegroundColor White
    }
    
    # Set optional Entra ID credentials
    if (-not [string]::IsNullOrEmpty($TenantId)) {
        if ($Persistent) {
            [Environment]::SetEnvironmentVariable("AZURE_TENANT_ID", $TenantId, "User")
            Write-Host "Set AZURE_TENANT_ID as persistent user environment variable" -ForegroundColor White
        } else {
            $env:AZURE_TENANT_ID = $TenantId
            Write-Host "Set AZURE_TENANT_ID for current session" -ForegroundColor White
        }
    }
    
    if (-not [string]::IsNullOrEmpty($ClientId)) {
        if ($Persistent) {
            [Environment]::SetEnvironmentVariable("AZURE_CLIENT_ID", $ClientId, "User")
            Write-Host "Set AZURE_CLIENT_ID as persistent user environment variable" -ForegroundColor White
        } else {
            $env:AZURE_CLIENT_ID = $ClientId
            Write-Host "Set AZURE_CLIENT_ID for current session" -ForegroundColor White
        }
    }
    
    Write-Host "`n=== Environment Variables Set Successfully ===" -ForegroundColor Green
    
    if ($Persistent) {
        Write-Host "Variables are set persistently. Restart your terminal or IDE to use them." -ForegroundColor Yellow
    } else {
        Write-Host "Variables are set for current session only." -ForegroundColor Yellow
        Write-Host "Use -Persistent flag to set permanently." -ForegroundColor Yellow
    }
    
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "1. Run: .\Scripts\Build.ps1" -ForegroundColor White
    Write-Host "2. Run: cd Backend && dotnet run" -ForegroundColor White
    
} catch {
    Write-Host "`n=== Environment Setup Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

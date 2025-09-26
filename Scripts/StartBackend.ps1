# Start Backend Server Script
# Starts the .NET Backend API server

param(
    [string]$Configuration = "Development",
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== Starting RR Realty AI Backend Server ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Project Root: $ProjectRoot" -ForegroundColor Yellow

try {
    # Navigate to Backend directory
    Set-Location "$ProjectRoot\Backend"
    
    Write-Host "`n--- Starting Backend Server ---" -ForegroundColor Cyan
    Write-Host "Backend will be available at: http://localhost:5000" -ForegroundColor White
    Write-Host "Swagger UI will be available at: http://localhost:5000/swagger" -ForegroundColor White
    Write-Host "`nPress Ctrl+C to stop the server" -ForegroundColor Yellow
    
    # Start the backend server
    if ($Verbose) {
        dotnet run --configuration $Configuration --verbosity detailed
    } else {
        dotnet run --configuration $Configuration
    }
    
} catch {
    Write-Host "`n=== Backend Server Failed to Start ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Set-Location $ProjectRoot
}

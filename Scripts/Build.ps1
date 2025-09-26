# RR Realty AI Build Script
# Builds both .NET Backend and React Frontend

param(
    [string]$Configuration = "Release",
    [switch]$SkipRestore,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== RR Realty AI Build Script ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Project Root: $ProjectRoot" -ForegroundColor Yellow

try {
    # Build .NET Backend
    Write-Host "`n--- Building .NET Backend ---" -ForegroundColor Cyan
    Set-Location "$ProjectRoot\Backend"
    
    if (-not $SkipRestore) {
        Write-Host "Restoring NuGet packages..." -ForegroundColor White
        dotnet restore
        if ($LASTEXITCODE -ne 0) { throw "Backend restore failed" }
    }
    
    Write-Host "Building backend project..." -ForegroundColor White
    if ($Verbose) {
        dotnet build --configuration $Configuration --verbosity detailed
    } else {
        dotnet build --configuration $Configuration
    }
    if ($LASTEXITCODE -ne 0) { throw "Backend build failed" }
    
    # Build React Frontend
    Write-Host "`n--- Building React Frontend ---" -ForegroundColor Cyan
    Set-Location "$ProjectRoot\Frontend"
    
    if (-not $SkipRestore) {
        Write-Host "Installing npm packages..." -ForegroundColor White
        npm install
        if ($LASTEXITCODE -ne 0) { throw "Frontend package installation failed" }
    }
    
    Write-Host "Building frontend project..." -ForegroundColor White
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Frontend build failed" }
    
    Write-Host "`n=== Build Completed Successfully ===" -ForegroundColor Green
    Write-Host "Backend: $ProjectRoot\Backend\bin\$Configuration" -ForegroundColor White
    Write-Host "Frontend: $ProjectRoot\Frontend\dist" -ForegroundColor White
    
} catch {
    Write-Host "`n=== Build Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Set-Location $ProjectRoot
}

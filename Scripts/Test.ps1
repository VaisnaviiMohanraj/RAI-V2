# RR Realty AI Test Script
# Runs comprehensive tests for both .NET Backend and React Frontend

param(
    [string]$Configuration = "Release",
    [switch]$Coverage,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== RR Realty AI Test Script ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Coverage: $Coverage" -ForegroundColor Yellow

try {
    # Test .NET Backend
    Write-Host "`n--- Running .NET Tests ---" -ForegroundColor Cyan
    Set-Location "$ProjectRoot\Backend"
    
    $testParams = @(
        "--configuration", $Configuration
        "--logger", "console;verbosity=detailed"
        "--no-build"
    )
    
    if ($Coverage) {
        $testParams += "--collect:XPlat Code Coverage"
    }
    
    if ($Verbose) {
        $testParams += "--verbosity", "diagnostic"
    }
    
    Write-Host "Running backend tests..." -ForegroundColor White
    dotnet test @testParams
    if ($LASTEXITCODE -ne 0) { throw "Backend tests failed" }
    
    # Test React Frontend
    Write-Host "`n--- Running React Tests ---" -ForegroundColor Cyan
    Set-Location "$ProjectRoot\Frontend"
    
    $env:CI = "true"  # Prevents watch mode
    
    if ($Coverage) {
        Write-Host "Running frontend tests with coverage..." -ForegroundColor White
        npm test -- --coverage --watchAll=false --verbose
    } else {
        Write-Host "Running frontend tests..." -ForegroundColor White
        npm test -- --watchAll=false
    }
    if ($LASTEXITCODE -ne 0) { throw "Frontend tests failed" }
    
    # Lint checks
    Write-Host "`n--- Running Lint Checks ---" -ForegroundColor Cyan
    
    Write-Host "Checking frontend code style..." -ForegroundColor White
    if (Test-Path "node_modules\.bin\eslint.cmd") {
        npm run lint --if-present
        if ($LASTEXITCODE -ne 0) { 
            Write-Warning "Linting issues found - please review"
        }
    }
    
    Write-Host "`n=== All Tests Completed Successfully ===" -ForegroundColor Green
    
    if ($Coverage) {
        Write-Host "`nCoverage reports generated:" -ForegroundColor Yellow
        Write-Host "Backend: $ProjectRoot\Backend\TestResults" -ForegroundColor White
        Write-Host "Frontend: $ProjectRoot\Frontend\coverage" -ForegroundColor White
    }
    
} catch {
    Write-Host "`n=== Tests Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Set-Location $ProjectRoot
    Remove-Item Env:\CI -ErrorAction SilentlyContinue
}

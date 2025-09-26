# Production Deployment Script for RR Realty AI Frontend
# This script ensures the app is built with production configuration

Write-Host "Starting production build for RR Realty AI Frontend..." -ForegroundColor Green

# Set environment variables for production
$env:NODE_ENV = "production"
$env:VITE_FORCE_PRODUCTION_URL = "true"

# Clean previous build
Write-Host "Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path "dist") {
    Remove-Item -Recurse -Force "dist"
}

# Install dependencies if needed
Write-Host "Installing dependencies..." -ForegroundColor Yellow
npm install

# Build with production configuration
Write-Host "Building with production configuration..." -ForegroundColor Yellow
npm run build

Write-Host "Production build completed!" -ForegroundColor Green
Write-Host "Build output is in the 'dist' directory" -ForegroundColor Cyan
Write-Host "The app is configured to use production URL: https://testing.rrrealty.ai" -ForegroundColor Cyan

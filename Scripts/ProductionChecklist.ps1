# RR Realty AI Production Deployment Checklist
# Validates environment configuration and deployment readiness
# Author: Generated for production deployment
# Date: 2025-01-12

param(
    [string]$Environment = "Production",
    [switch]$Fix,
    [switch]$Verbose
)

$Global:ChecklistItems = @()
$Global:PassedChecks = 0
$Global:FailedChecks = 0
$Global:WarningChecks = 0

function Write-ChecklistHeader {
    param([string]$Title)
    Write-Host "`n" + "="*50 -ForegroundColor Magenta
    Write-Host $Title.ToUpper() -ForegroundColor Magenta
    Write-Host "="*50 -ForegroundColor Magenta
}

function Write-CheckResult {
    param(
        [string]$Item,
        [string]$Status,
        [string]$Message = "",
        [string]$Recommendation = ""
    )
    
    $color = switch ($Status) {
        "PASS" { "Green"; $Global:PassedChecks++ }
        "FAIL" { "Red"; $Global:FailedChecks++ }
        "WARN" { "Yellow"; $Global:WarningChecks++ }
        default { "White" }
    }
    
    $Global:ChecklistItems += @{
        Item = $Item
        Status = $Status
        Message = $Message
        Recommendation = $Recommendation
        Timestamp = Get-Date
    }
    
    $statusPadded = $Status.PadLeft(6)
    Write-Host "[$statusPadded] $Item" -ForegroundColor $color
    if ($Message) {
        Write-Host "         $Message" -ForegroundColor Gray
    }
    if ($Recommendation) {
        Write-Host "         -> $Recommendation" -ForegroundColor Cyan
    }
}

function Test-EnvironmentVariables {
    Write-ChecklistHeader "Environment Variables"
    
    $requiredVars = @(
        @{Name = "AZURE_OPENAI_API_KEY"; Description = "Azure OpenAI API Key"},
        @{Name = "AZURE_OPENAI_ENDPOINT"; Description = "Azure OpenAI Endpoint"},
        @{Name = "AZURE_OPENAI_DEPLOYMENT"; Description = "Azure OpenAI Deployment Name"},
        @{Name = "AZURE_TENANT_ID"; Description = "Azure AD Tenant ID"},
        @{Name = "AZURE_CLIENT_ID"; Description = "Azure AD Client ID"},
        @{Name = "AZURE_CLIENT_SECRET"; Description = "Azure AD Client Secret"},
        @{Name = "AZURE_DOMAIN"; Description = "Azure AD Domain"}
    )
    
    foreach ($var in $requiredVars) {
        $value = [Environment]::GetEnvironmentVariable($var.Name)
        if ($value) {
            if ($value.Length -gt 10) {
                Write-CheckResult $var.Description "PASS" "Configured ($($value.Substring(0,10))...)"
            } else {
                Write-CheckResult $var.Description "WARN" "Value seems too short" "Verify the value is correct"
            }
        } else {
            Write-CheckResult $var.Description "FAIL" "Not set" "Set environment variable $($var.Name)"
        }
    }
}

function Test-ConfigurationFiles {
    Write-ChecklistHeader "Configuration Files"
    
    $configFiles = @(
        @{Path = "Backend\appsettings.json"; Required = $true},
        @{Path = "Backend\appsettings.Production.json"; Required = $true},
        @{Path = "Frontend\package.json"; Required = $true},
        @{Path = "Frontend\vite.config.ts"; Required = $true}
    )
    
    foreach ($config in $configFiles) {
        if (Test-Path $config.Path) {
            Write-CheckResult "Config: $($config.Path)" "PASS" "File exists"
            
            # Check for sensitive data in config files
            $content = Get-Content $config.Path -Raw
            $hasSecrets = $false
            # Only flag if there are actual non-empty values after the property names
            if ($content -match 'password.*[=:]\s*".{3,}"' -or $content -match "secret.*[=:]\s*'.{3,}'" -or $content -match 'key.*[=:]\s*".{3,}"') {
                $hasSecrets = $true
            }
            if ($hasSecrets) {
                Write-CheckResult "Security: $($config.Path)" "FAIL" "Contains hardcoded secrets" "Move secrets to environment variables"
            } else {
                Write-CheckResult "Security: $($config.Path)" "PASS" "No hardcoded secrets detected"
            }
        } else {
            $status = if ($config.Required) { "FAIL" } else { "WARN" }
            Write-CheckResult "Config: $($config.Path)" $status "File missing" "Create required configuration file"
        }
    }
}

function Test-Dependencies {
    Write-ChecklistHeader "Dependencies"
    
    # Check .NET version
    try {
        $dotnetVersion = dotnet --version
        if ($dotnetVersion -match "^9\.") {
            Write-CheckResult ".NET Version" "PASS" "Version: $dotnetVersion"
        } else {
            Write-CheckResult ".NET Version" "WARN" "Version: $dotnetVersion" "Consider upgrading to .NET 9"
        }
    }
    catch {
        Write-CheckResult ".NET Installation" "FAIL" "Not installed or not in PATH" "Install .NET 9 SDK"
    }
    
    # Check Node.js version
    try {
        $nodeVersion = node --version
        if ($nodeVersion -match "v1[8-9]|v2[0-9]") {
            Write-CheckResult "Node.js Version" "PASS" "Version: $nodeVersion"
        } else {
            Write-CheckResult "Node.js Version" "WARN" "Version: $nodeVersion" "Consider upgrading to Node.js 18+"
        }
    }
    catch {
        Write-CheckResult "Node.js Installation" "FAIL" "Not installed or not in PATH" "Install Node.js 18+"
    }
    
    # Check package files
    if (Test-Path "Backend\Backend.csproj") {
        Write-CheckResult "Backend Project File" "PASS" "Backend.csproj exists"
    } else {
        Write-CheckResult "Backend Project File" "FAIL" "Backend.csproj missing" "Restore project file"
    }
    
    if (Test-Path "Frontend\package.json") {
        Write-CheckResult "Frontend Package File" "PASS" "package.json exists"
        
        # Check if node_modules exists
        if (Test-Path "Frontend\node_modules") {
            Write-CheckResult "Frontend Dependencies" "PASS" "node_modules exists"
        } else {
            Write-CheckResult "Frontend Dependencies" "WARN" "node_modules missing" "Run 'npm install' in Frontend directory"
        }
    } else {
        Write-CheckResult "Frontend Package File" "FAIL" "package.json missing" "Restore package.json"
    }
}

function Test-BuildArtifacts {
    Write-ChecklistHeader "Build Artifacts"
    
    # Check if build directories exist (should be clean for production)
    $buildDirs = @("Backend\bin", "Backend\obj", "Frontend\dist", "Frontend\node_modules\.cache")
    
    foreach ($dir in $buildDirs) {
        if (Test-Path $dir) {
            Write-CheckResult "Clean Build: $dir" "WARN" "Build artifacts present" "Run cleanup script before deployment"
        } else {
            Write-CheckResult "Clean Build: $dir" "PASS" "No build artifacts"
        }
    }
    
    # Check for production build capability
    Write-CheckResult "Production Build Test" "INFO" "Testing build process..."
    
    try {
        Push-Location "Backend"
        $buildResult = dotnet build --configuration Release --verbosity quiet 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-CheckResult "Backend Build" "PASS" "Builds successfully"
        } else {
            Write-CheckResult "Backend Build" "FAIL" "Build failed" "Fix build errors: $buildResult"
        }
        Pop-Location
    }
    catch {
        Write-CheckResult "Backend Build" "FAIL" "Build test failed" $_.Exception.Message
        Pop-Location
    }
}

function Test-SecurityConfiguration {
    Write-ChecklistHeader "Security Configuration"
    
    # Check HTTPS configuration
    if (Test-Path "Backend\appsettings.Production.json") {
        try {
            $prodConfig = Get-Content "Backend\appsettings.Production.json" | ConvertFrom-Json
            
            if ($prodConfig.Kestrel.Endpoints.Https) {
                Write-CheckResult "HTTPS Configuration" "PASS" "HTTPS endpoint configured"
            } else {
                Write-CheckResult "HTTPS Configuration" "WARN" "HTTPS not explicitly configured" "Ensure HTTPS is enabled in production"
            }
        }
        catch {
            Write-CheckResult "HTTPS Configuration" "WARN" "Could not parse production config" "Verify JSON format"
        }
    }
    
    # Check for development settings in production config
    $devPatterns = @("localhost", "Development", "debug", "test")
    if (Test-Path "Backend\appsettings.Production.json") {
        $content = Get-Content "Backend\appsettings.Production.json" -Raw
        foreach ($pattern in $devPatterns) {
            if ($content -match $pattern) {
                Write-CheckResult "Production Config Purity" "WARN" "Contains '$pattern'" "Remove development references"
            }
        }
        if (-not ($devPatterns | Where-Object { $content -match $_ })) {
            Write-CheckResult "Production Config Purity" "PASS" "No development references found"
        }
    }
    
    # Check CORS configuration
    Write-CheckResult "CORS Security" "INFO" "Manual verification required" "Ensure CORS is properly restricted in production"
}

function Test-DeploymentReadiness {
    Write-ChecklistHeader "Deployment Readiness"
    
    # Check Git status
    try {
        $gitStatus = git status --porcelain 2>$null
        if ($gitStatus) {
            Write-CheckResult "Git Status" "WARN" "Uncommitted changes detected" "Commit all changes before deployment"
        } else {
            Write-CheckResult "Git Status" "PASS" "Working directory clean"
        }
        
        $currentBranch = git branch --show-current 2>$null
        Write-CheckResult "Git Branch" "INFO" "Current branch: $currentBranch" "Ensure deploying from correct branch"
    }
    catch {
        Write-CheckResult "Git Repository" "WARN" "Not a git repository or git not available" "Initialize git repository for version control"
    }
    
    # Check for sensitive files
    $sensitiveFiles = @(".env", "Backend\.env", "*.pfx", "*.p12", "*.key")
    foreach ($pattern in $sensitiveFiles) {
        $files = Get-ChildItem -Path . -Name $pattern -Recurse -ErrorAction SilentlyContinue
        if ($files) {
            Write-CheckResult "Sensitive Files: $pattern" "WARN" "Found: $($files -join ', ')" "Ensure these files are not deployed to production"
        } else {
            Write-CheckResult "Sensitive Files: $pattern" "PASS" "No sensitive files found"
        }
    }
    
    # Check deployment scripts
    $deploymentScripts = @("Scripts\Deploy.ps1", "Scripts\Build.ps1")
    foreach ($script in $deploymentScripts) {
        if (Test-Path $script) {
            Write-CheckResult "Deployment Script: $script" "PASS" "Script available"
        } else {
            Write-CheckResult "Deployment Script: $script" "WARN" "Script missing" "Create deployment automation script"
        }
    }
}

function Test-AzureResources {
    Write-ChecklistHeader "Azure Resources"
    
    # These are manual checks since we can't directly test Azure resources without credentials
    Write-CheckResult "Azure App Service" "INFO" "Manual verification required" "Ensure App Service is created and configured"
    Write-CheckResult "Azure OpenAI Service" "INFO" "Manual verification required" "Verify OpenAI service is deployed and accessible"
    Write-CheckResult "Azure AD App Registration" "INFO" "Manual verification required" "Confirm app registration and permissions"
    Write-CheckResult "Azure Key Vault" "INFO" "Manual verification required" "Verify Key Vault is configured with secrets"
    Write-CheckResult "Custom Domain/SSL" "INFO" "Manual verification required" "Configure custom domain and SSL certificate"
}

function Generate-ChecklistReport {
    Write-ChecklistHeader "Production Readiness Summary"
    
    $totalChecks = $Global:PassedChecks + $Global:FailedChecks + $Global:WarningChecks
    $readinessScore = if ($totalChecks -gt 0) { 
        [math]::Round((($Global:PassedChecks * 1.0 + $Global:WarningChecks * 0.5) / $totalChecks) * 100, 1) 
    } else { 0 }
    
    Write-Host "Total Checks: $totalChecks" -ForegroundColor Cyan
    Write-Host "Passed: $Global:PassedChecks" -ForegroundColor Green
    Write-Host "Failed: $Global:FailedChecks" -ForegroundColor Red
    Write-Host "Warnings: $Global:WarningChecks" -ForegroundColor Yellow
    Write-Host "Readiness Score: $readinessScore%" -ForegroundColor $(if ($readinessScore -ge 80) { "Green" } else { "Red" })
    
    # Production readiness assessment
    Write-Host "`nProduction Readiness Assessment:" -ForegroundColor Magenta
    
    if ($Global:FailedChecks -eq 0 -and $readinessScore -ge 90) {
        Write-Host "Ready for Production Deployment" -ForegroundColor Green
        Write-Host "All critical checks passed. Proceed with deployment." -ForegroundColor Green
    } elseif ($Global:FailedChecks -eq 0 -and $readinessScore -ge 75) {
        Write-Host "Mostly Ready - Minor Issues" -ForegroundColor Yellow
        Write-Host "Address warnings before deployment for optimal security." -ForegroundColor Yellow
    } elseif ($Global:FailedChecks -le 2) {
        Write-Host "Needs Attention" -ForegroundColor Yellow
        Write-Host "Fix failed checks before proceeding with deployment." -ForegroundColor Yellow
    } else {
        Write-Host "Not Ready for Production" -ForegroundColor Red
        Write-Host "Critical issues must be resolved before deployment." -ForegroundColor Red
    }
    
    # Save detailed report
    $reportPath = "ProductionChecklist_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"
    $Global:ChecklistItems | ConvertTo-Json -Depth 3 | Out-File $reportPath
    Write-Host "`nDetailed checklist saved to: $reportPath" -ForegroundColor Cyan
    
    # Next steps
    Write-Host "`nNext Steps:" -ForegroundColor Magenta
    Write-Host "1. Address all FAILED items" -ForegroundColor White
    Write-Host "2. Review and fix WARNING items" -ForegroundColor White
    Write-Host "3. Run production test suite: .\Scripts\ProductionTest.ps1" -ForegroundColor White
    Write-Host "4. Deploy using: .\Scripts\Deploy.ps1" -ForegroundColor White
    
    return $Global:FailedChecks -eq 0 -and $readinessScore -ge 75
}

# Main execution
Write-Host "RR Realty AI Production Deployment Checklist" -ForegroundColor Magenta
Write-Host "============================================" -ForegroundColor Magenta
Write-Host "Environment: $Environment" -ForegroundColor Cyan
Write-Host "Timestamp: $(Get-Date)" -ForegroundColor Cyan

# Execute checklist items
Test-EnvironmentVariables
Test-ConfigurationFiles
Test-Dependencies
Test-BuildArtifacts
Test-SecurityConfiguration
Test-DeploymentReadiness
Test-AzureResources

# Generate final report
$isReady = Generate-ChecklistReport

# Exit with appropriate code
exit $(if ($isReady) { 0 } else { 1 })

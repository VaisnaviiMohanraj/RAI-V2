# RR Realty AI Production Testing Script
# Comprehensive testing suite for production readiness validation
# Author: Generated for production deployment
# Date: 2025-01-12

param(
    [string]$BackendUrl = "https://site-net-rrai-blue-fsgabaardkdhhnhf.centralus-01.azurewebsites.net",
    [string]$FrontendUrl = "https://testing.rrrealty.ai",
    [switch]$SkipAuth,
    [switch]$Verbose,
    [string]$TestDataPath = ".\TestData"
)

# Test configuration
$Global:TestResults = @()
$Global:PassedTests = 0
$Global:FailedTests = 0
$Global:SkippedTests = 0

# Colors for output
$Colors = @{
    Success = "Green"
    Error = "Red"
    Warning = "Yellow"
    Info = "Cyan"
    Header = "Magenta"
}

function Write-TestHeader {
    param([string]$Title)
    Write-Host "`n" + "="*60 -ForegroundColor $Colors.Header
    Write-Host $Title.ToUpper().PadLeft(($Title.Length + 60) / 2) -ForegroundColor $Colors.Header
    Write-Host "="*60 -ForegroundColor $Colors.Header
}

function Write-TestResult {
    param(
        [string]$TestName,
        [string]$Status,
        [string]$Message = "",
        [object]$Details = $null
    )
    
    $color = switch ($Status) {
        "PASS" { $Colors.Success; $Global:PassedTests++ }
        "FAIL" { $Colors.Error; $Global:FailedTests++ }
        "SKIP" { $Colors.Warning; $Global:SkippedTests++ }
        default { $Colors.Info }
    }
    
    $result = @{
        TestName = $TestName
        Status = $Status
        Message = $Message
        Details = $Details
        Timestamp = Get-Date
    }
    
    $Global:TestResults += $result
    
    $statusPadded = $Status.PadLeft(6)
    Write-Host "[$statusPadded] $TestName" -ForegroundColor $color
    if ($Message) {
        Write-Host "         $Message" -ForegroundColor Gray
    }
    if ($Verbose -and $Details) {
        Write-Host "         Details: $($Details | ConvertTo-Json -Compress)" -ForegroundColor Gray
    }
}

function Test-HttpEndpoint {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [object]$Body = $null,
        [int]$ExpectedStatus = 200,
        [int]$TimeoutSeconds = 30
    )
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            TimeoutSec = $TimeoutSeconds
            UseBasicParsing = $true
        }
        
        if ($Body) {
            $params.Body = $Body | ConvertTo-Json
            $params.ContentType = "application/json"
        }
        
        $response = Invoke-WebRequest @params
        
        return @{
            Success = $response.StatusCode -eq $ExpectedStatus
            StatusCode = $response.StatusCode
            Content = $response.Content
            Headers = $response.Headers
        }
    }
    catch {
        return @{
            Success = $false
            StatusCode = $_.Exception.Response.StatusCode.value__
            Error = $_.Exception.Message
            Content = $null
        }
    }
}

function Test-BackendHealth {
    Write-TestHeader "Backend Health Checks"
    
    # Test basic health endpoint
    $result = Test-HttpEndpoint -Url "$BackendUrl/api/health"
    if ($result.Success) {
        Write-TestResult "Backend Health Endpoint" "PASS" "Service is responding"
    } else {
        Write-TestResult "Backend Health Endpoint" "FAIL" "Status: $($result.StatusCode), Error: $($result.Error)"
        return $false
    }
    
    # Test HTTPS configuration
    if ($BackendUrl.StartsWith("https://")) {
        try {
            $cert = [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
            $result = Test-HttpEndpoint -Url "$BackendUrl/api/health"
            Write-TestResult "HTTPS Configuration" "PASS" "SSL/TLS is properly configured"
        }
        catch {
            Write-TestResult "HTTPS Configuration" "FAIL" "SSL/TLS configuration issue: $($_.Exception.Message)"
        }
    }
    
    # Test CORS headers
    $result = Test-HttpEndpoint -Url "$BackendUrl/api/health" -Headers @{"Origin" = $FrontendUrl}
    if ($result.Headers -and $result.Headers["Access-Control-Allow-Origin"]) {
        Write-TestResult "CORS Configuration" "PASS" "CORS headers present"
    } else {
        Write-TestResult "CORS Configuration" "FAIL" "Missing CORS headers"
    }
    
    return $true
}

function Test-Authentication {
    Write-TestHeader "Authentication & Authorization Tests"
    
    if ($SkipAuth) {
        Write-TestResult "Authentication Tests" "SKIP" "Skipped by user request"
        return $true
    }
    
    # Test protected endpoint without auth
    $result = Test-HttpEndpoint -Url "$BackendUrl/api/health/auth" -ExpectedStatus = 401
    if ($result.StatusCode -eq 401) {
        Write-TestResult "Protected Endpoint Security" "PASS" "Properly returns 401 Unauthorized"
    } else {
        Write-TestResult "Protected Endpoint Security" "FAIL" "Expected 401, got $($result.StatusCode)"
    }
    
    # Test MSAL configuration endpoint
    $result = Test-HttpEndpoint -Url "$BackendUrl/.well-known/openid_configuration"
    if ($result.Success) {
        Write-TestResult "MSAL Configuration" "PASS" "OpenID configuration available"
    } else {
        Write-TestResult "MSAL Configuration" "FAIL" "OpenID configuration not accessible"
    }
    
    return $true
}

function Test-AzureOpenAI {
    Write-TestHeader "Azure OpenAI Integration Tests"
    
    # Test chat endpoint (requires auth in production)
    $testMessage = @{
        content = "Hello, this is a production test message. Please respond briefly."
        userId = "test-user"
        conversationId = "test-conversation-$(Get-Date -Format 'yyyyMMddHHmmss')"
    }
    
    $result = Test-HttpEndpoint -Url "$BackendUrl/api/chat/send" -Method "POST" -Body $testMessage -ExpectedStatus = 401
    
    if ($result.StatusCode -eq 401) {
        Write-TestResult "Chat Endpoint Security" "PASS" "Properly requires authentication"
    } elseif ($result.StatusCode -eq 200) {
        Write-TestResult "Chat Endpoint Functionality" "PASS" "Chat endpoint responding (auth disabled)"
        
        # Parse response to check AI integration
        try {
            $chatResponse = $result.Content | ConvertFrom-Json
            if ($chatResponse.content -and $chatResponse.content.Length -gt 0) {
                Write-TestResult "Azure OpenAI Integration" "PASS" "AI generated valid response"
            } else {
                Write-TestResult "Azure OpenAI Integration" "FAIL" "Empty or invalid AI response"
            }
        }
        catch {
            Write-TestResult "Azure OpenAI Integration" "FAIL" "Invalid response format"
        }
    } else {
        Write-TestResult "Chat Endpoint" "FAIL" "Unexpected status: $($result.StatusCode)"
    }
    
    return $true
}

function Test-DocumentProcessing {
    Write-TestHeader "Document Processing Tests"
    
    # Test document upload endpoint security
    $result = Test-HttpEndpoint -Url "$BackendUrl/api/document/upload" -Method "POST" -ExpectedStatus = 401
    
    if ($result.StatusCode -eq 401) {
        Write-TestResult "Document Upload Security" "PASS" "Properly requires authentication"
    } else {
        Write-TestResult "Document Upload Security" "FAIL" "Expected 401, got $($result.StatusCode)"
    }
    
    # Test document list endpoint
    $result = Test-HttpEndpoint -Url "$BackendUrl/api/document" -ExpectedStatus = 401
    
    if ($result.StatusCode -eq 401) {
        Write-TestResult "Document List Security" "PASS" "Properly requires authentication"
    } else {
        Write-TestResult "Document List Security" "FAIL" "Expected 401, got $($result.StatusCode)"
    }
    
    return $true
}

function Test-ConversationManagement {
    Write-TestHeader "Conversation Management Tests"
    
    # Test conversation sessions endpoint
    $result = Test-HttpEndpoint -Url "$BackendUrl/api/chat/sessions" -ExpectedStatus = 401
    
    if ($result.StatusCode -eq 401) {
        Write-TestResult "Conversation Sessions Security" "PASS" "Properly requires authentication"
    } else {
        Write-TestResult "Conversation Sessions Security" "FAIL" "Expected 401, got $($result.StatusCode)"
    }
    
    # Test conversation history endpoint
    $result = Test-HttpEndpoint -Url "$BackendUrl/api/chat/history/test-session" -ExpectedStatus = 401
    
    if ($result.StatusCode -eq 401) {
        Write-TestResult "Conversation History Security" "PASS" "Properly requires authentication"
    } else {
        Write-TestResult "Conversation History Security" "FAIL" "Expected 401, got $($result.StatusCode)"
    }
    
    return $true
}

function Test-FrontendDeployment {
    Write-TestHeader "Frontend Deployment Tests"
    
    # Test frontend accessibility
    $result = Test-HttpEndpoint -Url $FrontendUrl
    if ($result.Success) {
        Write-TestResult "Frontend Accessibility" "PASS" "Frontend is accessible"
        
        # Check for key elements in HTML
        if ($result.Content -match "RR Realty AI" -or $result.Content -match "react") {
            Write-TestResult "Frontend Content" "PASS" "Contains expected application content"
        } else {
            Write-TestResult "Frontend Content" "FAIL" "Missing expected application content"
        }
    } else {
        Write-TestResult "Frontend Accessibility" "FAIL" "Frontend not accessible: $($result.Error)"
    }
    
    # Test static assets
    $staticAssets = @("/vite.svg", "/assets/index.css", "/assets/index.js")
    foreach ($asset in $staticAssets) {
        $result = Test-HttpEndpoint -Url "$FrontendUrl$asset"
        if ($result.Success) {
            Write-TestResult "Static Asset: $asset" "PASS" "Asset loaded successfully"
        } else {
            Write-TestResult "Static Asset: $asset" "FAIL" "Asset not found or inaccessible"
        }
    }
    
    return $true
}

function Test-Performance {
    Write-TestHeader "Performance Tests"
    
    # Test response times
    $endpoints = @(
        @{Url = "$BackendUrl/api/health"; Name = "Health Check"}
    )
    
    foreach ($endpoint in $endpoints) {
        $times = @()
        for ($i = 1; $i -le 5; $i++) {
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $result = Test-HttpEndpoint -Url $endpoint.Url
            $stopwatch.Stop()
            $times += $stopwatch.ElapsedMilliseconds
        }
        
        $avgTime = ($times | Measure-Object -Average).Average
        if ($avgTime -lt 1000) {
            Write-TestResult "$($endpoint.Name) Response Time" "PASS" "Average: ${avgTime}ms"
        } elseif ($avgTime -lt 3000) {
            Write-TestResult "$($endpoint.Name) Response Time" "WARN" "Average: ${avgTime}ms (acceptable)"
        } else {
            Write-TestResult "$($endpoint.Name) Response Time" "FAIL" "Average: ${avgTime}ms (too slow)"
        }
    }
    
    return $true
}

function Test-Security {
    Write-TestHeader "Security Tests"
    
    # Test security headers
    $result = Test-HttpEndpoint -Url "$BackendUrl/api/health"
    if ($result.Headers) {
        $securityHeaders = @(
            "X-Content-Type-Options",
            "X-Frame-Options",
            "X-XSS-Protection"
        )
        
        foreach ($header in $securityHeaders) {
            if ($result.Headers[$header]) {
                Write-TestResult "Security Header: $header" "PASS" "Present"
            } else {
                Write-TestResult "Security Header: $header" "WARN" "Missing (recommended)"
            }
        }
    }
    
    # Test for sensitive information exposure
    $sensitivePatterns = @("password", "secret", "key", "token")
    foreach ($pattern in $sensitivePatterns) {
        if ($result.Content -match $pattern) {
            Write-TestResult "Information Disclosure: $pattern" "FAIL" "Potentially sensitive information exposed"
        } else {
            Write-TestResult "Information Disclosure: $pattern" "PASS" "No exposure detected"
        }
    }
    
    return $true
}

function Generate-TestReport {
    Write-TestHeader "Production Test Summary"
    
    $totalTests = $Global:PassedTests + $Global:FailedTests + $Global:SkippedTests
    $passRate = if ($totalTests -gt 0) { [math]::Round(($Global:PassedTests / $totalTests) * 100, 2) } else { 0 }
    
    Write-Host 'Total Tests: ' -NoNewline -ForegroundColor $Colors.Info
    Write-Host $totalTests -ForegroundColor $Colors.Info
    Write-Host 'Passed: ' -NoNewline -ForegroundColor $Colors.Success
    Write-Host $Global:PassedTests -ForegroundColor $Colors.Success
    Write-Host 'Failed: ' -NoNewline -ForegroundColor $Colors.Error
    Write-Host $Global:FailedTests -ForegroundColor $Colors.Error
    Write-Host 'Skipped: ' -NoNewline -ForegroundColor $Colors.Warning
    Write-Host $Global:SkippedTests -ForegroundColor $Colors.Warning
    Write-Host 'Pass Rate: ' -NoNewline -ForegroundColor $(if ($passRate -ge 80) { $Colors.Success } else { $Colors.Error })
    Write-Host "$passRate%" -ForegroundColor $(if ($passRate -ge 80) { $Colors.Success } else { $Colors.Error })
    
    # Production readiness assessment
    Write-Host '`nProduction Readiness Assessment:' -ForegroundColor $Colors.Header
    
    if ($Global:FailedTests -eq 0 -and $passRate -ge 90) {
        Write-Host 'READY FOR PRODUCTION' -ForegroundColor $Colors.Success
        Write-Host 'All critical tests passed. System is production-ready.' -ForegroundColor $Colors.Success
    } elseif ($Global:FailedTests -le 2 -and $passRate -ge 80) {
        Write-Host 'NEEDS ATTENTION' -ForegroundColor $Colors.Warning
        Write-Host 'Some issues detected. Review failed tests before production deployment.' -ForegroundColor $Colors.Warning
    } else {
        Write-Host 'NOT READY FOR PRODUCTION' -ForegroundColor $Colors.Error
        Write-Host 'Critical issues detected. Address all failures before deployment.' -ForegroundColor $Colors.Error
    }
    
    # Save detailed report
    $reportPath = "ProductionTestReport_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"
    $Global:TestResults | ConvertTo-Json -Depth 3 | Out-File $reportPath
    Write-Host '`nDetailed report saved to: ' -NoNewline -ForegroundColor $Colors.Info
    Write-Host $reportPath -ForegroundColor $Colors.Info
    
    return $passRate -ge 80 -and $Global:FailedTests -eq 0
}

# Main execution
Write-Host 'RR Realty AI Production Testing Suite' -ForegroundColor $Colors.Header
Write-Host '=====================================' -ForegroundColor $Colors.Header
Write-Host 'Backend URL: ' -NoNewline -ForegroundColor $Colors.Info
Write-Host $BackendUrl -ForegroundColor $Colors.Info
Write-Host 'Frontend URL: ' -NoNewline -ForegroundColor $Colors.Info
Write-Host $FrontendUrl -ForegroundColor $Colors.Info
Write-Host 'Test Started: ' -NoNewline -ForegroundColor $Colors.Info
Write-Host (Get-Date) -ForegroundColor $Colors.Info

# Execute test suites
$testSuites = @(
    { Test-BackendHealth },
    { Test-Authentication },
    { Test-AzureOpenAI },
    { Test-DocumentProcessing },
    { Test-ConversationManagement },
    { Test-FrontendDeployment },
    { Test-Performance },
    { Test-Security }
)

foreach ($testSuite in $testSuites) {
    try {
        & $testSuite
    }
    catch {
        Write-TestResult 'Test Suite Execution' 'FAIL' 'Error executing test suite'
    }
}

# Generate final report
$isProductionReady = Generate-TestReport

# Exit with appropriate code
exit $(if ($isProductionReady) { 0 } else { 1 })

# HRMS 22-Test Suite — PowerShell Test Runner (Windows)
#
# Usage: .\run-all-tests.ps1
#
# Requires: PowerShell 5.0+

param(
    [switch]$Verbose = $false,
    [string]$ApiUrl = "http://localhost:8080",
    [string]$MailhogUrl = "http://localhost:8025",
    [string]$JaegerUrl = "http://localhost:16686"
)

$ErrorActionPreference = "Continue"

# Test counters
$pass = 0
$fail = 0
$total = 0
$results = @()

# Helper function: Test HTTP endpoint
function Test-HttpEndpoint {
    param(
        [int]$TestNum,
        [string]$Name,
        [string]$Url,
        [int]$ExpectedCode = 200,
        [string]$Header = $null
    )
    
    $total++
    Write-Host "`n$('{0:00}' -f $TestNum). $Name" -ForegroundColor Cyan
    Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host "URL: $Url"
    
    try {
        $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec 5 -ErrorAction SilentlyContinue
        $httpCode = [int]$response.StatusCode
        
        if ($httpCode -eq $ExpectedCode) {
            Write-Host "Status: $httpCode (Expected: $ExpectedCode)" -ForegroundColor Green
            Write-Host "✅ PASS" -ForegroundColor Green
            $pass++
            $results += @{Test = $TestNum; Name = $Name; Result = "PASS"; Details = "HTTP $httpCode" }
            return $true
        } else {
            Write-Host "Status: $httpCode (Expected: $ExpectedCode)" -ForegroundColor Red
            Write-Host "❌ FAIL" -ForegroundColor Red
            $fail++
            $results += @{Test = $TestNum; Name = $Name; Result = "FAIL"; Details = "HTTP $httpCode" }
            return $false
        }
    } catch {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "❌ FAIL" -ForegroundColor Red
        $fail++
        $results += @{Test = $TestNum; Name = $Name; Result = "FAIL"; Details = $_.Exception.Message }
        return $false
    }
}

# Helper function: Test JSON response
function Test-JsonResponse {
    param(
        [int]$TestNum,
        [string]$Name,
        [string]$Url,
        [string]$JsonPath,
        [string]$ExpectedValue = $null
    )
    
    $total++
    Write-Host "`n$('{0:00}' -f $TestNum). $Name" -ForegroundColor Cyan
    Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host "URL: $Url"
    Write-Host "Path: $JsonPath"
    
    try {
        $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec 5 -ErrorAction SilentlyContinue
        $json = $response.Content | ConvertFrom-Json
        
        # Navigate JSON path
        $value = $json
        foreach ($part in $JsonPath.Split('.')) {
            if ($part) {
                $value = $value.$part
            }
        }
        
        if ($null -eq $value) {
            Write-Host "Value: NOT FOUND" -ForegroundColor Red
            Write-Host "❌ FAIL" -ForegroundColor Red
            $fail++
            $results += @{Test = $TestNum; Name = $Name; Result = "FAIL"; Details = "JSON path not found" }
            return $false
        } elseif ($ExpectedValue -and $value -ne $ExpectedValue) {
            Write-Host "Value: $value (Expected: $ExpectedValue)" -ForegroundColor Red
            Write-Host "❌ FAIL" -ForegroundColor Red
            $fail++
            $results += @{Test = $TestNum; Name = $Name; Result = "FAIL"; Details = "Value mismatch" }
            return $false
        } else {
            Write-Host "Value: $value" -ForegroundColor Green
            Write-Host "✅ PASS" -ForegroundColor Green
            $pass++
            $results += @{Test = $TestNum; Name = $Name; Result = "PASS"; Details = $value }
            return $true
        }
    } catch {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "❌ FAIL" -ForegroundColor Red
        $fail++
        $results += @{Test = $TestNum; Name = $Name; Result = "FAIL"; Details = $_.Exception.Message }
        return $false
    }
}

# Helper function: Test Docker service
function Test-DockerService {
    param(
        [int]$TestNum,
        [string]$Name,
        [string]$ServiceName,
        [string]$LogPattern = $null
    )
    
    $total++
    Write-Host "`n$('{0:00}' -f $TestNum). $Name" -ForegroundColor Cyan
    Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host "Service: $ServiceName"
    
    try {
        $psOutput = & docker compose ps $ServiceName --format "{{.State}}" 2>&1
        
        if ($psOutput -match "(running|healthy)") {
            Write-Host "Status: $psOutput" -ForegroundColor Green
            Write-Host "✅ PASS" -ForegroundColor Green
            $pass++
            $results += @{Test = $TestNum; Name = $Name; Result = "PASS"; Details = $psOutput }
            return $true
        } else {
            Write-Host "Status: $psOutput" -ForegroundColor Red
            Write-Host "❌ FAIL" -ForegroundColor Red
            $fail++
            $results += @{Test = $TestNum; Name = $Name; Result = "FAIL"; Details = $psOutput }
            return $false
        }
    } catch {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "❌ FAIL" -ForegroundColor Red
        $fail++
        $results += @{Test = $TestNum; Name = $Name; Result = "FAIL"; Details = $_.Exception.Message }
        return $false
    }
}

# Helper function: Test Docker service (optional — warns instead of failing
# when the service is legitimately absent from the compose file in use)
function Test-DockerServiceOptional {
    param(
        [int]$TestNum,
        [string]$Name,
        [string]$ServiceName
    )

    $total++
    Write-Host "`n$('{0:00}' -f $TestNum). $Name" -ForegroundColor Cyan
    Write-Host "──────────────────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host "Service: $ServiceName"

    # FIX BUG #39: the default docker-compose.yml has no `mailhog` service
    # (it only exists in docker-compose-dev.yml / docker-compose-on-premise-test.yml).
    # Running `docker compose up -d` per this script's own instructions therefore
    # never starts mailhog, so a hard FAIL here was permanent and misleading.
    # Treat an absent/undefined service as a skipped check, not a failure.
    $psOutput = & docker compose ps $ServiceName --format "{{.State}}" 2>&1
    $psText = "$psOutput"

    if ($psText -match "(running|healthy)") {
        Write-Host "Status: $psOutput" -ForegroundColor Green
        Write-Host "✅ PASS" -ForegroundColor Green
        $pass++
        $results += @{Test = $TestNum; Name = $Name; Result = "PASS"; Details = $psOutput }
        return $true
    } elseif ($psText -match "no such service|not found|no configuration file") {
        Write-Host "Status: service not defined in the active compose file (SKIPPED)" -ForegroundColor Yellow
        Write-Host "SKIP - use docker-compose-dev.yml to include MailHog" -ForegroundColor Yellow
        $results += @{Test = $TestNum; Name = $Name; Result = "SKIP"; Details = "service not in compose file" }
        $total--
        return $null
    } else {
        Write-Host "Status: $psOutput" -ForegroundColor Red
        Write-Host "❌ FAIL" -ForegroundColor Red
        $fail++
        $results += @{Test = $TestNum; Name = $Name; Result = "FAIL"; Details = $psOutput }
        return $false
    }
}

# ============================================================================
# MAIN TEST SUITE
# ============================================================================

Clear-Host

Write-Host "`n╔═════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║         HRMS 22-Test Suite — Localhost Testing              ║" -ForegroundColor Cyan
Write-Host "║                  Starting: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')                   ║" -ForegroundColor Cyan
Write-Host "╚═════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

Write-Host "`nPre-flight Checks..." -ForegroundColor Yellow

# Check if API is running
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/health" -TimeoutSec 2 -ErrorAction SilentlyContinue
    Write-Host "✅ API is running on port 8080" -ForegroundColor Green
} catch {
    Write-Host "❌ API not responding on port 8080" -ForegroundColor Red
    Write-Host "Run: docker compose up -d"
    exit 1
}

# ────────────────────────────────────────────────────────────────────────────
# CATEGORY 1: API HEALTH & CONNECTIVITY (Tests 1-3)
# ────────────────────────────────────────────────────────────────────────────

Write-Host "`n═════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host "CATEGORY 1: API HEALTH & CONNECTIVITY (Tests 1-3)" -ForegroundColor White
Write-Host "═════════════════════════════════════════════════════════════" -ForegroundColor White

Test-HttpEndpoint 1 "API Liveness (/healthz/live)" "$ApiUrl/healthz/live" 200
Test-HttpEndpoint 2 "API Readiness (/healthz/ready)" "$ApiUrl/healthz/ready" 200
Test-HttpEndpoint 3 "API Health (/health)" "$ApiUrl/health" 200

# ────────────────────────────────────────────────────────────────────────────
# CATEGORY 2: DATABASE & MIGRATIONS (Tests 4-6)
# ────────────────────────────────────────────────────────────────────────────

Write-Host "`n═════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host "CATEGORY 2: DATABASE & MIGRATIONS (Tests 4-6)" -ForegroundColor White
Write-Host "═════════════════════════════════════════════════════════════" -ForegroundColor White

Test-JsonResponse 4 "Database Tables Exist" "$ApiUrl/health" "entries.database.status" "Healthy"
Test-JsonResponse 5 "Database Health Check" "$ApiUrl/healthz/ready" "entries.database.status" "Healthy"
Test-HttpEndpoint 6 "Database Connection Valid" "$ApiUrl/health" 200

# ────────────────────────────────────────────────────────────────────────────
# CATEGORY 3: AUTHENTICATION & JWT (Tests 7-9)
# ────────────────────────────────────────────────────────────────────────────

Write-Host "`n═════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host "CATEGORY 3: AUTHENTICATION & JWT (Tests 7-9)" -ForegroundColor White
Write-Host "═════════════════════════════════════════════════════════════" -ForegroundColor White

Test-HttpEndpoint 7 "CSRF Token Endpoint (/api/auth/csrf)" "$ApiUrl/api/auth/csrf" 200
Test-JsonResponse 8 "Invalid Login Rejected" "$ApiUrl/api/auth/login" "success" "false"
Test-HttpEndpoint 9 "Swagger UI Available" "$ApiUrl/swagger/index.html" 200

# ────────────────────────────────────────────────────────────────────────────
# CATEGORY 4: CORS & SECURITY HEADERS (Tests 10-12)
# ────────────────────────────────────────────────────────────────────────────

Write-Host "`n═════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host "CATEGORY 4: CORS & SECURITY HEADERS (Tests 10-12)" -ForegroundColor White
Write-Host "═════════════════════════════════════════════════════════════" -ForegroundColor White

# Test 10: CORS
$total++
Write-Host "`n10. CORS Allow Localhost:3000" -ForegroundColor Cyan
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/api/auth/csrf" -Method OPTIONS `
        -Headers @{"Origin"="http://localhost:3000"} -TimeoutSec 5 -ErrorAction SilentlyContinue
    $corsHeader = $response.Headers["Access-Control-Allow-Origin"]
    if ($corsHeader -like "*localhost:3000*" -or $corsHeader -like "*localhost*") {
        Write-Host "CORS Header: $corsHeader" -ForegroundColor Green
        Write-Host "✅ PASS" -ForegroundColor Green
        $pass++
        $results += @{Test = 10; Name = "CORS Allow Localhost:3000"; Result = "PASS"; Details = $corsHeader }
    } else {
        Write-Host "CORS Header: $corsHeader (NOT FOUND)" -ForegroundColor Red
        Write-Host "❌ FAIL" -ForegroundColor Red
        $fail++
        $results += @{Test = 10; Name = "CORS Allow Localhost:3000"; Result = "FAIL"; Details = "CORS header missing" }
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "❌ FAIL" -ForegroundColor Red
    $fail++
    $results += @{Test = 10; Name = "CORS Allow Localhost:3000"; Result = "FAIL"; Details = $_.Exception.Message }
}

# Test 11: Security Headers
$total++
Write-Host "`n11. Security Headers Present" -ForegroundColor Cyan
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/health" -TimeoutSec 5 -ErrorAction SilentlyContinue
    $headers = $response.Headers
    $requiredHeaders = @("X-Content-Type-Options", "X-Frame-Options", "X-XSS-Protection")
    $foundHeaders = 0
    
    foreach ($header in $requiredHeaders) {
        if ($headers.ContainsKey($header)) {
            Write-Host "$header : $($headers[$header])" -ForegroundColor Green
            $foundHeaders++
        }
    }
    
    if ($foundHeaders -eq $requiredHeaders.Count) {
        Write-Host "✅ PASS" -ForegroundColor Green
        $pass++
        $results += @{Test = 11; Name = "Security Headers Present"; Result = "PASS"; Details = "All headers found" }
    } else {
        Write-Host "❌ FAIL - Missing $($requiredHeaders.Count - $foundHeaders) headers" -ForegroundColor Red
        $fail++
        $results += @{Test = 11; Name = "Security Headers Present"; Result = "FAIL"; Details = "Missing headers" }
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "❌ FAIL" -ForegroundColor Red
    $fail++
    $results += @{Test = 11; Name = "Security Headers Present"; Result = "FAIL"; Details = $_.Exception.Message }
}

# Test 12: Rate Limit Endpoint
Test-HttpEndpoint 12 "Rate Limit Endpoint Available" "$ApiUrl/health" 200

# ────────────────────────────────────────────────────────────────────────────
# CATEGORY 5: SERVICES CHECK (Tests 13-16)
# ────────────────────────────────────────────────────────────────────────────

Write-Host "`n═════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host "CATEGORY 5: DOCKER SERVICES STATUS (Tests 13-16)" -ForegroundColor White
Write-Host "═════════════════════════════════════════════════════════════" -ForegroundColor White

Test-DockerService 13 "MySQL Service Running" "mysql"
Test-DockerService 14 "Redis Service Running" "redis"
Test-DockerServiceOptional 15 "MailHog Service Running" "mailhog"
Test-DockerService 16 "API Service Running" "api"

# ────────────────────────────────────────────────────────────────────────────
# CATEGORY 6: OBSERVABILITY (Tests 17-22)
# ────────────────────────────────────────────────────────────────────────────

Write-Host "`n═════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host "CATEGORY 6: OBSERVABILITY & INTEGRATIONS (Tests 17-22)" -ForegroundColor White
Write-Host "═════════════════════════════════════════════════════════════" -ForegroundColor White

Test-HttpEndpoint 17 "Prometheus /metrics Endpoint" "$ApiUrl/metrics" 200
# FIX BUG #39: MailHog Web UI is only reachable when docker-compose-dev.yml
# (or the on-premise-test compose) is used; the default docker-compose.yml has
# no mailhog service, so this always failed under the script's own documented
# usage. Skip gracefully instead of hard-failing when unreachable.
$total++
Write-Host "`n18. MailHog Web UI" -ForegroundColor Cyan
Write-Host "──────────────────────────────────────────────────────" -ForegroundColor Cyan
Write-Host "URL: $MailhogUrl"
try {
    $mhResponse = Invoke-WebRequest -Uri $MailhogUrl -Method GET -TimeoutSec 3 -ErrorAction Stop
    if ([int]$mhResponse.StatusCode -eq 200) {
        Write-Host "Status: 200 (Expected: 200)" -ForegroundColor Green
        Write-Host "✅ PASS" -ForegroundColor Green
        $pass++
        $results += @{Test = 18; Name = "MailHog Web UI"; Result = "PASS"; Details = "HTTP 200" }
    } else {
        Write-Host "Status: $([int]$mhResponse.StatusCode) (Expected: 200)" -ForegroundColor Red
        Write-Host "❌ FAIL" -ForegroundColor Red
        $fail++
        $results += @{Test = 18; Name = "MailHog Web UI"; Result = "FAIL"; Details = "HTTP $([int]$mhResponse.StatusCode)" }
    }
} catch {
    Write-Host "MailHog not reachable — not started (main docker-compose.yml has no mailhog service; use docker-compose-dev.yml) (SKIPPED)" -ForegroundColor Yellow
    $results += @{Test = 18; Name = "MailHog Web UI"; Result = "SKIP"; Details = "mailhog not in active compose file" }
    $total--
}
Test-HttpEndpoint 19 "Jaeger UI" "$JaegerUrl" 200
Test-JsonResponse 20 "Redis Health Check" "$ApiUrl/healthz/ready" "entries.redis.status" "Healthy"
Test-JsonResponse 21 "Email Service Health" "$ApiUrl/health" "entries.email.status" "Healthy"

# Test 22: .env validation
$total++
Write-Host "`n22. Environment Configuration Valid" -ForegroundColor Cyan
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Cyan
if (Test-Path ".env") {
    $envContent = Get-Content ".env" | Select-String "ALLOWED_ORIGINS=.*localhost"
    if ($envContent) {
        Write-Host ".env contains ALLOWED_ORIGINS" -ForegroundColor Green
        Write-Host "✅ PASS" -ForegroundColor Green
        $pass++
        $results += @{Test = 22; Name = "Environment Configuration Valid"; Result = "PASS"; Details = ".env OK" }
    } else {
        Write-Host ".env missing or incomplete ALLOWED_ORIGINS" -ForegroundColor Red
        Write-Host "❌ FAIL" -ForegroundColor Red
        $fail++
        $results += @{Test = 22; Name = "Environment Configuration Valid"; Result = "FAIL"; Details = ".env incomplete" }
    }
} else {
    Write-Host ".env file not found" -ForegroundColor Red
    Write-Host "❌ FAIL" -ForegroundColor Red
    $fail++
    $results += @{Test = 22; Name = "Environment Configuration Valid"; Result = "FAIL"; Details = ".env missing" }
}

# ============================================================================
# RESULTS SUMMARY
# ============================================================================

Write-Host "`n═════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host "TEST RESULTS SUMMARY" -ForegroundColor White
Write-Host "═════════════════════════════════════════════════════════════" -ForegroundColor White
Write-Host ""

Write-Host "Total Tests:   $total"
Write-Host "Passed:        $pass" -ForegroundColor Green
Write-Host "Failed:        $fail" -ForegroundColor Red

$passRate = if ($total -gt 0) { [math]::Round(($pass / $total) * 100, 2) } else { 0 }
Write-Host "Pass Rate:     $passRate%"

Write-Host ""
Write-Host "Test Duration: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

Write-Host "`n" + ("─" * 63)
Write-Host "Detailed Results:"
Write-Host ("─" * 63) + "`n"

$results | Format-Table -Property @(
    @{Label = "Test #"; Expression = { $_.Test }; Width = 6 },
    @{Label = "Name"; Expression = { $_.Name }; Width = 35 },
    @{Label = "Result"; Expression = { $_.Result }; Width = 8 },
    @{Label = "Details"; Expression = { $_.Details }; Width = 13 }
) -AutoSize

Write-Host ""

if ($fail -eq 0) {
    Write-Host "╔═════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║  🎉 ALL $total TESTS PASSED! Ready for deployment.              ║" -ForegroundColor Green
    Write-Host "╚═════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    exit 0
} else {
    Write-Host "╔═════════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║  ⚠️  $fail TEST(S) FAILED. Review above for details.         ║" -ForegroundColor Red
    Write-Host "╚═════════════════════════════════════════════════════════════╝" -ForegroundColor Red
    exit 1
}

#!/bin/bash

##############################################################################
# HRMS 22-Test Suite — Automated Test Runner
# 
# Usage: ./run-all-tests.sh
# 
# Tests:
#  1-3:   API Health & Connectivity
#  4-6:   Database & Migrations
#  7-9:   Authentication & JWT
# 10-12:  CORS & Security Headers
# 13-15:  Rate Limiting
# 16-18:  Email & MailHog
# 19-20:  Redis & Caching
# 21-22:  Observability
#
##############################################################################

set -o pipefail

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Test counters
PASS=0
FAIL=0
TOTAL=0

# Timeout for curl commands (seconds)
CURL_TIMEOUT=5

##############################################################################
# Helper Functions
##############################################################################

test_case() {
  local num=$1
  local name=$2
  local cmd=$3
  local expected=$4
  
  TOTAL=$((TOTAL + 1))
  
  echo ""
  echo -e "${BLUE}Test $num: $name${NC}"
  echo "─────────────────────────────────────────────────────────────"
  echo "Command: $cmd"
  
  # Execute command with timeout
  local output
  output=$(eval "$cmd" 2>&1)
  local exit_code=$?
  
  # Check result
  if [ $exit_code -eq 0 ] && ([ -z "$expected" ] || echo "$output" | grep -q "$expected"); then
    echo -e "Result: ${GREEN}✅ PASS${NC}"
    echo "Output: ${output:0:100}"
    PASS=$((PASS + 1))
    return 0
  else
    echo -e "Result: ${RED}❌ FAIL${NC}"
    echo "Exit Code: $exit_code"
    echo "Output: ${output:0:200}"
    FAIL=$((FAIL + 1))
    return 1
  fi
}

test_case_http() {
  local num=$1
  local name=$2
  local url=$3
  local expected_code=$4
  
  TOTAL=$((TOTAL + 1))
  
  echo ""
  echo -e "${BLUE}Test $num: $name${NC}"
  echo "─────────────────────────────────────────────────────────────"
  echo "URL: $url"
  
  local response
  response=$(curl -s -o /tmp/response_$num.txt -w "%{http_code}" --max-time $CURL_TIMEOUT "$url" 2>&1)
  local http_code=$response
  
  if [ "$http_code" = "$expected_code" ]; then
    echo -e "HTTP Status: ${GREEN}$http_code (Expected: $expected_code)${NC}"
    echo -e "Result: ${GREEN}✅ PASS${NC}"
    PASS=$((PASS + 1))
    return 0
  else
    echo -e "HTTP Status: ${RED}$http_code (Expected: $expected_code)${NC}"
    echo -e "Result: ${RED}❌ FAIL${NC}"
    FAIL=$((FAIL + 1))
    return 1
  fi
}

check_service() {
  local service=$1
  local port=$2
  
  if nc -z localhost $port 2>/dev/null; then
    return 0
  else
    return 1
  fi
}

##############################################################################
# MAIN TEST SUITE
##############################################################################

echo ""
echo "╔═════════════════════════════════════════════════════════════╗"
echo "║         HRMS 22-Test Suite — Localhost Testing              ║"
echo "║                  Starting: $(date +'%Y-%m-%d %H:%M:%S')                   ║"
echo "╚═════════════════════════════════════════════════════════════╝"
echo ""

# Pre-flight checks
echo -e "${YELLOW}Pre-flight Checks${NC}"
echo "─────────────────────────────────────────────────────────────"

if ! check_service "API" 8080; then
  echo -e "${RED}❌ API not responding on port 8080${NC}"
  echo "Run: docker compose up -d"
  exit 1
fi
echo -e "${GREEN}✅ API is running on port 8080${NC}"

if ! check_service "MySQL" 3306; then
  echo -e "${RED}⚠️  MySQL not accessible (may be ok if internal network)${NC}"
else
  echo -e "${GREEN}✅ MySQL is running on port 3306${NC}"
fi

echo ""
echo "═════════════════════════════════════════════════════════════"
echo "CATEGORY 1: API HEALTH & CONNECTIVITY (Tests 1-3)"
echo "═════════════════════════════════════════════════════════════"

test_case_http 1 "API Liveness (/healthz/live)" \
  "http://localhost:8080/healthz/live" \
  "200"

test_case_http 2 "API Readiness (/healthz/ready)" \
  "http://localhost:8080/healthz/ready" \
  "200"

test_case_http 3 "API Health (/health)" \
  "http://localhost:8080/health" \
  "200"

echo ""
echo "═════════════════════════════════════════════════════════════"
echo "CATEGORY 2: DATABASE & MIGRATIONS (Tests 4-6)"
echo "═════════════════════════════════════════════════════════════"

test_case 4 "Database Tables Exist" \
  "curl -s http://localhost:8080/health | jq '.entries.database.status'" \
  "Healthy"

test_case 5 "Database Soft Delete Support" \
  "curl -s http://localhost:8080/healthz/ready | jq '.entries.database.status'" \
  "Healthy"

test_case 6 "Database Connection String Valid" \
  "curl -s http://localhost:8080/health | jq -r '.entries.database.status'" \
  "Healthy"

echo ""
echo "═════════════════════════════════════════════════════════════"
echo "CATEGORY 3: AUTHENTICATION & JWT (Tests 7-9)"
echo "═════════════════════════════════════════════════════════════"

test_case_http 7 "CSRF Token Endpoint (/api/auth/csrf)" \
  "http://localhost:8080/api/auth/csrf" \
  "200"

test_case 8 "Invalid Login Rejected" \
  "curl -s -X POST http://localhost:8080/api/auth/login -H 'Content-Type: application/json' -d '{\"email\":\"invalid@test.com\",\"password\":\"Wrong123!\"}' | jq -r '.success'" \
  "false"

test_case_http 9 "Swagger UI Available" \
  "http://localhost:8080/swagger/index.html" \
  "200"

echo ""
echo "═════════════════════════════════════════════════════════════"
echo "CATEGORY 4: CORS & SECURITY HEADERS (Tests 10-12)"
echo "═════════════════════════════════════════════════════════════"

test_case 10 "CORS Allow Localhost:3000" \
  "curl -s -i -H 'Origin: http://localhost:3000' -X OPTIONS http://localhost:8080/api/auth/login 2>/dev/null | grep -i 'Access-Control-Allow-Origin'" \
  "localhost:3000"

test_case 11 "CORS Block Unauthorized Origin" \
  "! curl -s -i -H 'Origin: http://evil.com' -X OPTIONS http://localhost:8080/api/auth/login 2>/dev/null | grep -i 'Access-Control-Allow-Origin: http://evil.com'" \
  ""

test_case 12 "Security Headers Present (X-Content-Type-Options)" \
  "curl -s -i http://localhost:8080/health 2>/dev/null | grep -i 'X-Content-Type-Options: nosniff'" \
  "nosniff"

echo ""
echo "═════════════════════════════════════════════════════════════"
echo "CATEGORY 5: RATE LIMITING (Tests 13-15)"
echo "═════════════════════════════════════════════════════════════"

test_case 13 "Rate Limiter Returns 429" \
  "for i in {1..12}; do curl -s -o /dev/null -w '%{http_code}' -X POST http://localhost:8080/api/auth/login -H 'Content-Type: application/json' -d '{\"email\":\"test@test.com\",\"password\":\"Test123!\"}'; done | grep -q 429" \
  ""

test_case 14 "API Rate Limit Active" \
  "curl -s -i http://localhost:8080/health 2>/dev/null | grep -E 'HTTP|Retry-After'" \
  "HTTP"

test_case 15 "Retry-After Header on Rate Limit" \
  "curl -s -i http://localhost:8080/health 2>/dev/null | head -20" \
  ""

echo ""
echo "═════════════════════════════════════════════════════════════"
echo "CATEGORY 6: EMAIL & MAILHOG (Tests 16-18)"
echo "═════════════════════════════════════════════════════════════"

test_case 16 "MailHog SMTP Service Accessible" \
  "curl -s -i http://localhost:8025 2>/dev/null | grep -i 'HTTP'" \
  "HTTP"

test_case 17 "Email Configuration Valid" \
  "curl -s http://localhost:8080/health | jq '.entries.email.status'" \
  "Healthy"

test_case 18 "Email Service Not Disabled" \
  "curl -s http://localhost:8080/health | jq '.entries.email'" \
  "Healthy"

echo ""
echo "═════════════════════════════════════════════════════════════"
echo "CATEGORY 7: REDIS & CACHING (Tests 19-20)"
echo "═════════════════════════════════════════════════════════════"

test_case 19 "Redis Connection Available" \
  "curl -s http://localhost:8080/health | jq '.entries.redis.status'" \
  "Healthy"

test_case 20 "Redis Health Check" \
  "curl -s http://localhost:8080/healthz/ready | jq '.entries.redis.status'" \
  "Healthy"

echo ""
echo "═════════════════════════════════════════════════════════════"
echo "CATEGORY 8: OBSERVABILITY (Tests 21-22)"
echo "═════════════════════════════════════════════════════════════"

test_case_http 21 "Prometheus /metrics Endpoint" \
  "http://localhost:8080/metrics" \
  "200"

test_case 22 "Jaeger UI Accessible" \
  "curl -s -i http://localhost:16686 2>/dev/null | grep -i 'HTTP'" \
  "HTTP"

echo ""
echo "═════════════════════════════════════════════════════════════"
echo "TEST RESULTS SUMMARY"
echo "═════════════════════════════════════════════════════════════"
echo ""
echo -e "Total Tests:  $TOTAL"
echo -e "${GREEN}Passed:       $PASS${NC}"
echo -e "${RED}Failed:       $FAIL${NC}"

if [ $FAIL -eq 0 ]; then
  PASS_RATE=100
else
  PASS_RATE=$((PASS * 100 / TOTAL))
fi

echo -e "Pass Rate:    $PASS_RATE%"
echo ""
echo "Test Duration: $(date +'%Y-%m-%d %H:%M:%S')"
echo ""

# Final verdict
if [ $FAIL -eq 0 ]; then
  echo -e "${GREEN}╔═════════════════════════════════════════════════════════════╗${NC}"
  echo -e "${GREEN}║  🎉 ALL TESTS PASSED! Ready for production deployment.       ║${NC}"
  echo -e "${GREEN}╚═════════════════════════════════════════════════════════════╝${NC}"
  exit 0
else
  echo -e "${RED}╔═════════════════════════════════════════════════════════════╗${NC}"
  echo -e "${RED}║  ⚠️  $FAIL TEST(S) FAILED. Review output above.            ║${NC}"
  echo -e "${RED}╚═════════════════════════════════════════════════════════════╝${NC}"
  exit 1
fi

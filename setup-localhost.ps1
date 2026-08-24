# HRMS Localhost Setup Script
# Run this to set all environment variables and start the system

Write-Host "╔════════════════════════════════════════════════════════════╗"
Write-Host "║     HRMS v1.0.5 - Localhost Setup & Startup               ║"
Write-Host "╚════════════════════════════════════════════════════════════╝"
Write-Host ""

# Check prerequisites
Write-Host "📋 Checking prerequisites..."
Write-Host ""

$checks = @(
    @{Name="dotnet"; Cmd="dotnet --version"},
    @{Name="docker"; Cmd="docker --version"},
    @{Name="mysql"; Cmd="mysql --version"},
    @{Name="node"; Cmd="node --version"}
)

foreach ($check in $checks) {
    try {
        $result = Invoke-Expression $check.Cmd 2>&1
        Write-Host "✅ $($check.Name): $($result[0])"
    } catch {
        Write-Host "❌ $($check.Name): NOT INSTALLED"
    }
}

Write-Host ""
Write-Host "📁 Checking MySQL connection..."
try {
    mysql -h localhost -u test -ptest -e "SELECT 1;" 2>&1 | Out-Null
    Write-Host "✅ MySQL: Connected"
} catch {
    Write-Host "❌ MySQL: Connection failed - ensure MySQL is running"
    exit 1
}

Write-Host ""
Write-Host "🔧 Setting environment variables..."

# Database
$env:ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=hrms;User ID=test;Password=test;SslMode=None"

# Email (your credentials - Brevo SMTP relay)
$env:Email__Host="smtp-relay.brevo.com"
$env:Email__Port="587"
$env:Email__Username="rtmishra8985@gmail.com"
$env:Email__Password="Rtmishra@7040"

# Optional settings
$env:AppSettings__EnableSwagger="true"
$env:Serilog__MinimumLevel__Default="Information"

Write-Host "✅ Environment variables set"
Write-Host ""

# JWT Keys check
Write-Host "🔐 JWT Key Configuration"
if (-not $env:Jwt__PrivateKeyPem) {
    Write-Host "⚠️  Jwt__PrivateKeyPem not set. You need to generate or provide JWT keys."
    Write-Host ""
    Write-Host "To generate new keys:"
    Write-Host "  openssl genrsa -out private.pem 2048"
    Write-Host "  openssl rsa -in private.pem -pubout -out public.pem"
    Write-Host ""
    Write-Host "Then set the environment variables:"
    Write-Host '  $env:Jwt__PrivateKeyPem="..."'
    Write-Host '  $env:Jwt__PublicKeyPem="..."'
    exit 1
} else {
    Write-Host "✅ Jwt__PrivateKeyPem is configured"
}

if (-not $env:Security__EncryptionKey) {
    Write-Host "⚠️  Security__EncryptionKey not set. You need to provide an encryption key."
    Write-Host ""
    Write-Host "Generate a 32-byte base64 key:"
    Write-Host '  $key = [Convert]::ToBase64String((1..32 | % {[byte](Get-Random -Max 256)}))'
    Write-Host '  Write-Host $key'
    Write-Host '  $env:Security__EncryptionKey=$key'
    exit 1
} else {
    Write-Host "✅ Security__EncryptionKey is configured"
}

Write-Host ""
Write-Host "🏗️  Building HRMS.API..."
$buildStart = Get-Date
$buildOutput = dotnet build HRMS.API -c Release 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed:"
    Write-Host $buildOutput
    exit 1
}
$buildTime = (Get-Date) - $buildStart
Write-Host "✅ Build succeeded in $($buildTime.TotalSeconds)s"

Write-Host ""
Write-Host "🚀 Starting HRMS.API..."
Write-Host ""
Write-Host "API will be available at:"
Write-Host "  • http://localhost:5000"
Write-Host "  • https://localhost:5001"
Write-Host ""
Write-Host "Health checks:"
Write-Host "  • http://localhost:5000/healthz/live"
Write-Host "  • http://localhost:5000/healthz/ready"
Write-Host ""
Write-Host "Login credentials:"
Write-Host "  • Email: superadmin@hrms.com"
Write-Host "  • Password: (check logs for initial password)"
Write-Host ""
Write-Host "React SPA:"
Write-Host "  • Start separately: cd HRMS.SPA.Source && npm run dev"
Write-Host "  • Access at: http://localhost:5173"
Write-Host ""
Write-Host "Press Ctrl+C to stop the API"
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
Write-Host ""

# Start the API
dotnet run --configuration Release --project HRMS.API

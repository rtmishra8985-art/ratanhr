# Testing Guide
**HRMS v2.0.0**

---

## Test Project

`HRMS.Tests` — xUnit test suite.

```bash
# Run all tests
dotnet test HRMS.Tests/HRMS.Tests.csproj

# Run with coverage
dotnet test HRMS.Tests/HRMS.Tests.csproj \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=opencover \
  /p:CoverletOutput=./coverage.xml

# Run specific test class
dotnet test --filter "FullyQualifiedName~PayrollServiceTests"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

---

## Test Categories

| Category | Files | Focus |
|----------|-------|-------|
| Unit | `*ServiceTests.cs` | Service business logic |
| Security | `IDORTests.cs`, `*SecurityTests.cs` | Cross-tenant access, authentication |
| Boundary | `BoundaryTests.cs` | Edge cases, null inputs, overflow |
| Integration | `IntegrationTests/*` | Multi-step workflows |
| Performance | `*PerformanceTests.cs` | Query timing, memory |

---

## Test Coverage Areas

| Module | Tests | Key Scenarios |
|--------|-------|--------------|
| Auth | `AuthServiceTests`, `JwtServiceTests` | Login, refresh, password reset |
| Payroll | `PayrollServiceTests`, `BulkPayrollTests` | Calculations, edge cases |
| Leave | `LeaveServiceTests`, `LeaveBalanceAdjustmentTests` | Approval, balance |
| Attendance | `AttendanceCalculationTests`, `BackDatedAttendanceTests` | Check-in/out, backdating |
| IDOR | `IDORExtendedTests`, `ReportControllerIDORTests` | Cross-company access denied |
| Security | `BonusDeductionSecurityTests`, `EncryptionServiceTests` | PII, XSS, injection |

---

## Writing New Tests

### Unit Test Template

```csharp
public class EmployeeServiceTests
{
    private readonly Mock<ApplicationDbContext> _dbMock;
    private readonly EmployeeService _sut;

    public EmployeeServiceTests()
    {
        _dbMock = new Mock<ApplicationDbContext>();
        _sut    = new EmployeeService(_dbMock.Object, ...);
    }

    [Fact]
    public async Task GetEmployee_WhenNotInCompany_ThrowsUnauthorized()
    {
        // Arrange
        var empId = "EMP001";
        var userCompanyId = 1;
        var empCompanyId  = 2;
        // ... setup mock

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GetEmployeeAsync(empId, userCompanyId));
    }
}
```

### Naming Convention

`MethodName_WhenCondition_ExpectedBehavior`

Examples:
- `GeneratePayroll_WhenLocked_ThrowsInvalidOperation`
- `Login_WithInvalidPassword_Returns401`
- `GetEmployees_WhenDifferentTenant_ReturnsEmpty`

---

## Mocking Strategy

- **DbContext**: Use in-memory provider for unit tests
- **Services**: Moq for interface mocking
- **HttpContext**: Use `DefaultHttpContext` with manual claims setup

```csharp
// Create test user context
var claims = new[] {
    new Claim(ClaimTypes.NameIdentifier, "user-id"),
    new Claim("CompanyId", "1"),
    new Claim(ClaimTypes.Role, "admin")
};
var identity = new ClaimsIdentity(claims, "Test");
var user     = new ClaimsPrincipal(identity);
httpContext.User = user;
```

---

## CI Integration

Tests run automatically in GitHub Actions on every push/PR:

```yaml
- name: Run tests
  run: dotnet test HRMS.Tests/HRMS.Tests.csproj
       --logger "trx;LogFileName=test-results.trx"
       -- RunConfiguration.FailFast=true
```

- `FailFast=true` — stops on first failure (faster CI feedback)
- TRX results uploaded as artifact and published as PR check
- `TreatWarningsAsErrors=true` in build — no warnings in test code either

using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Tests.Payroll;

public sealed class PayrollAtomicityTests
{
    [Fact]
    public async Task GeneratePayslip_StampsAuthenticatedTenantOnNewRow()
    {
        await using var db = TestHelpers.CreateInMemoryDb();
        db.Employees.Add(new Employee
        {
            EmployeeCode = "TENANT_EMP",
            CompanyId = 7,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var id = await service.GeneratePayslipAsync(
            BuildDto("TENANT_EMP"),
            callerCompanyId: 7);

        var payslip = await db.Payslips.SingleAsync(p => p.Id == id);
        Assert.Equal(7, payslip.CompanyId);
    }

    [Fact]
    public async Task GeneratePayslip_AuditFailure_RollsBackPayslipWrite()
    {
        var (db, connection) = TestHelpers.CreateSqliteDb();
        await using (db)
        await using (connection)
        {
            // payslips.company_id has a real FK to companies(id); seed the tenant row
            // so the rollback assertion tests audit failure, not FK failure.
            db.Companies.Add(new HRMS.Domain.Entities.Company.Company
            {
                CompanyId = 7,
                Name      = "Atomic Test Co",
                IsActive  = true
            });
            db.Employees.Add(new Employee
            {
                EmployeeCode = "ATOMIC_EMP",
                CompanyId = 7,
                IsActive = true
            });
            await db.SaveChangesAsync();

            var audit = new Mock<IAuditService>();
            audit
                .Setup(a => a.LogAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<int?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int?>(),
                    It.IsAny<string?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()))
                .ThrowsAsync(new InvalidOperationException("simulated audit outage"));

            var service = new PayrollService(
                db,
                audit.Object,
                new MockNotificationService(),
                new MockPayrollCalculator(),
                new MockLogger<PayrollService>());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GeneratePayslipAsync(
                    BuildDto("ATOMIC_EMP"),
                    callerCompanyId: 7));

            Assert.Empty(await db.Payslips
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync());
        }
    }

    private static PayrollService BuildService(ApplicationDbContext db) =>
        new(
            db,
            new MockAuditService(),
            new MockNotificationService(),
            new MockPayrollCalculator(),
            new MockLogger<PayrollService>());

    private static GeneratePayslipDto BuildDto(string employeeId) => new()
    {
        EmployeeId = employeeId,
        Month = 7,
        Year = 2026,
        CompanyId = 7,
        BasicPay = 30_000m,
        WorkingDays = 26,
        DaysPresent = 26,
        AutoCalculate = true
    };
}
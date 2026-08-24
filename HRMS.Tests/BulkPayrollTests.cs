using HRMS.Application.DTOs.Payroll;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Xunit;

namespace HRMS.Tests;

public class BulkPayrollTests
{
    [Fact]
    public async Task BulkGenerate_GeneratesPayslipForAllEmployees()
    {
        using var db = TestHelpers.CreateInMemoryDb();

        // Seed employees with salary structures
        for (int i = 1; i <= 3; i++)
        {
            var empCode = $"EMP10{i}";
            db.Employees.Add(new Employee {
                EmployeeId = i, EmployeeCode = empCode, FullName = $"Employee {i}", IsActive = true, CompanyId = 1
            });
            db.SalaryStructures.Add(new SalaryStructure {
                EmployeeId = empCode, BasicPay = 20000, HRA = 8000, DA = 2000,
                Conveyance = 1600, MedicalAllowance = 1250, OtherAllowances = 3150,
                PFEmployee = 1800, PFEmployer = 1800, ESI = 0, PT = 200, TDS = 0,
                EffectiveFrom = DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
                IsActive = true, CreatedAt = DateTime.UtcNow
            });
        }
        db.SaveChanges();

        var svc = new PayrollService(db, new MockAuditService(), new MockNotificationService(),
                                     new MockPayrollCalculator(), new MockLogger<PayrollService>());
        var result = await svc.BulkGeneratePayslipsAsync(new BulkPayrollDto {
            Month = 7, Year = 2026, CompanyId = 1
        });

        Assert.Equal(3, result.Generated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task BulkGenerate_SkipsWhenPayslipExists_IfNoOverwrite()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.Employees.Add(new Employee { EmployeeId = 1, EmployeeCode = "EMP201", FullName = "Jane", IsActive = true, CompanyId = 1 });
        db.SalaryStructures.Add(new SalaryStructure {
            EmployeeId = "EMP201", BasicPay = 30000, HRA = 12000, IsActive = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
            CreatedAt = DateTime.UtcNow
        });
        db.Payslips.Add(new Payslip {
            EmployeeId = "EMP201", Month = 8, Year = 2026,
            BasicPay = 30000, GrossEarnings = 42000, NetPay = 40000,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var svc = new PayrollService(db, new MockAuditService(), new MockNotificationService(),
                                     new MockPayrollCalculator(), new MockLogger<PayrollService>());
        var result = await svc.BulkGeneratePayslipsAsync(new BulkPayrollDto {
            Month = 8, Year = 2026, CompanyId = 1, Overwrite = false
        });

        Assert.Equal(0, result.Generated);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public async Task BulkGenerate_Overwrite_RegeneratesExistingPayslip()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.Employees.Add(new Employee { EmployeeId = 1, EmployeeCode = "EMP301", FullName = "Sam", IsActive = true, CompanyId = 1 });
        db.SalaryStructures.Add(new SalaryStructure {
            EmployeeId = "EMP301", BasicPay = 25000, HRA = 10000, IsActive = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
            CreatedAt = DateTime.UtcNow
        });
        db.Payslips.Add(new Payslip {
            EmployeeId = "EMP301", Month = 9, Year = 2026,
            BasicPay = 25000, GrossEarnings = 35000, NetPay = 33000,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var svc = new PayrollService(db, new MockAuditService(), new MockNotificationService(),
                                     new MockPayrollCalculator(), new MockLogger<PayrollService>());
        var result = await svc.BulkGeneratePayslipsAsync(new BulkPayrollDto {
            Month = 9, Year = 2026, CompanyId = 1, Overwrite = true
        });

        Assert.Equal(1, result.Generated);
        Assert.Equal(0, result.Skipped);
    }
}

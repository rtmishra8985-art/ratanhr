using System.Security.Claims;
using HRMS.API.Controllers.Reports;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// IDOR unit tests for all report controllers:
///   • PayrollReportController  — cross-tenant query param must be ignored for regular admins
///   • AttendanceReportController — same
///   • EmployeeReportController   — same
///   • DashboardReportController  — same
///   • SalaryRegisterController   — same
///
/// Root vulnerability: the original controllers used <c>companyId ?? CompanyId</c>.
/// When a regular admin supplies <c>?companyId=999</c>, the query param is non-null so
/// it wins the null-coalescing operator — exposing another tenant's data (IDOR).
///
/// Fix: the controllers now use <c>CompanyId ?? companyId</c> (i.e. EffectiveCompanyId).
/// For admins <c>CompanyId</c> is always set from the JWT claim and therefore wins.
/// For superadmins <c>CompanyId</c> is null, so the query param is honoured.
///
/// Each test class verifies three scenarios:
///   1. Admin supplies a cross-tenant companyId → service is called with the JWT companyId, NOT the attacker value.
///   2. Admin supplies own companyId → service is called with that companyId (200 OK).
///   3. Superadmin supplies any companyId → service is called with that companyId (200 OK).
/// </summary>
public class ReportControllerIDORTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static ClaimsPrincipal MakePrincipal(string role, int companyId, int userId = 1)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role,           role),
            new("companyId",               companyId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static void SetCaller(ControllerBase ctrl, ClaimsPrincipal principal)
    {
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // ══════════════════════════════════════════════════════════════════════
    // PayrollReportController
    // ══════════════════════════════════════════════════════════════════════

    public class PayrollReportIDOR
    {
        [Fact]
        public async Task Monthly_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            // Arrange: admin with JWT companyId=1 passes ?companyId=999 (cross-tenant attack)
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetPayrollReportAsync(1, 7, 2026))
               .ReturnsAsync(new PayrollReportDto());

            var ctrl = new PayrollReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            // Act: attacker tries to pull company 999's payroll
            var result = await ctrl.Monthly(companyId: 999, month: 7, year: 2026);

            // Assert: service was called with companyId=1 (from JWT), not 999
            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetPayrollReportAsync(1, 7, 2026), Times.Once);
            svc.Verify(s => s.GetPayrollReportAsync(999, It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Monthly_OwnCompanyAdmin_Returns200()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetPayrollReportAsync(1, 7, 2026))
               .ReturnsAsync(new PayrollReportDto());

            var ctrl = new PayrollReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Monthly(companyId: 1, month: 7, year: 2026);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetPayrollReportAsync(1, 7, 2026), Times.Once);
        }

        [Fact]
        public async Task Monthly_Superadmin_CanAccessAnyTenant()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetPayrollReportAsync(999, 7, 2026))
               .ReturnsAsync(new PayrollReportDto());

            var ctrl = new PayrollReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

            // Superadmin explicitly passes ?companyId=999 → service should receive 999
            var result = await ctrl.Monthly(companyId: 999, month: 7, year: 2026);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetPayrollReportAsync(999, 7, 2026), Times.Once);
        }

        [Fact]
        public async Task Export_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.ExportPayrollReportAsync(1, 7, 2026))
               .ReturnsAsync(Array.Empty<byte>());

            var ctrl = new PayrollReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Export(companyId: 999, month: 7, year: 2026);

            Assert.IsType<FileContentResult>(result);
            svc.Verify(s => s.ExportPayrollReportAsync(1, 7, 2026), Times.Once);
            svc.Verify(s => s.ExportPayrollReportAsync(999, It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // AttendanceReportController
    // ══════════════════════════════════════════════════════════════════════

    public class AttendanceReportIDOR
    {
        [Fact]
        public async Task Monthly_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetMonthlyAttendanceReportAsync(1, 7, 2026))
               .ReturnsAsync(new List<MonthlyAttendanceReportDto>());

            var ctrl = new AttendanceReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Monthly(companyId: 999, month: 7, year: 2026);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetMonthlyAttendanceReportAsync(1, 7, 2026), Times.Once);
            svc.Verify(s => s.GetMonthlyAttendanceReportAsync(999, It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Daily_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            var from = new DateOnly(2026, 7, 1);
            var to   = new DateOnly(2026, 7, 31);

            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetDailyAttendanceReportAsync(1, from, to))
               .ReturnsAsync(new List<DailyAttendanceReportDto>());

            var ctrl = new AttendanceReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Daily(companyId: 999, from: from, to: to);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetDailyAttendanceReportAsync(1, from, to), Times.Once);
            svc.Verify(s => s.GetDailyAttendanceReportAsync(999, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Never);
        }

        [Fact]
        public async Task Monthly_Superadmin_CanAccessAnyTenant()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetMonthlyAttendanceReportAsync(999, 7, 2026))
               .ReturnsAsync(new List<MonthlyAttendanceReportDto>());

            var ctrl = new AttendanceReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

            var result = await ctrl.Monthly(companyId: 999, month: 7, year: 2026);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetMonthlyAttendanceReportAsync(999, 7, 2026), Times.Once);
        }

        [Fact]
        public async Task Export_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.ExportAttendanceReportAsync(1, 7, 2026))
               .ReturnsAsync(Array.Empty<byte>());

            var ctrl = new AttendanceReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Export(companyId: 999, month: 7, year: 2026);

            Assert.IsType<FileContentResult>(result);
            svc.Verify(s => s.ExportAttendanceReportAsync(1, 7, 2026), Times.Once);
            svc.Verify(s => s.ExportAttendanceReportAsync(999, It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // EmployeeReportController
    // ══════════════════════════════════════════════════════════════════════

    public class EmployeeReportIDOR
    {
        [Fact]
        public async Task Summary_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetEmployeeSummaryReportAsync(1))
               .ReturnsAsync(new EmployeeSummaryReportDto());

            var ctrl = new EmployeeReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Summary(companyId: 999);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetEmployeeSummaryReportAsync(1), Times.Once);
            svc.Verify(s => s.GetEmployeeSummaryReportAsync(999), Times.Never);
        }

        [Fact]
        public async Task Summary_OwnCompanyAdmin_Returns200()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetEmployeeSummaryReportAsync(1))
               .ReturnsAsync(new EmployeeSummaryReportDto());

            var ctrl = new EmployeeReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Summary(companyId: 1);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetEmployeeSummaryReportAsync(1), Times.Once);
        }

        [Fact]
        public async Task Summary_Superadmin_CanAccessAnyTenant()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetEmployeeSummaryReportAsync(999))
               .ReturnsAsync(new EmployeeSummaryReportDto());

            var ctrl = new EmployeeReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

            var result = await ctrl.Summary(companyId: 999);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetEmployeeSummaryReportAsync(999), Times.Once);
        }

        [Fact]
        public async Task Export_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.ExportEmployeeReportAsync(1))
               .ReturnsAsync(Array.Empty<byte>());

            var ctrl = new EmployeeReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Export(companyId: 999);

            Assert.IsType<FileContentResult>(result);
            svc.Verify(s => s.ExportEmployeeReportAsync(1), Times.Once);
            svc.Verify(s => s.ExportEmployeeReportAsync(999), Times.Never);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // DashboardReportController
    // ══════════════════════════════════════════════════════════════════════

    public class DashboardReportIDOR
    {
        [Fact]
        public async Task GetDashboard_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetDashboardKpisAsync(1))
               .ReturnsAsync(new DashboardKpiDto());

            var ctrl = new DashboardReportController(svc.Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.GetDashboard(companyId: 999);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetDashboardKpisAsync(1), Times.Once);
            svc.Verify(s => s.GetDashboardKpisAsync(999), Times.Never);
        }

        [Fact]
        public async Task GetDashboard_OwnCompanyAdmin_Returns200()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetDashboardKpisAsync(1))
               .ReturnsAsync(new DashboardKpiDto());

            var ctrl = new DashboardReportController(svc.Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.GetDashboard(companyId: 1);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetDashboardKpisAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetDashboard_Superadmin_CanAccessAnyTenant()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetDashboardKpisAsync(999))
               .ReturnsAsync(new DashboardKpiDto());

            var ctrl = new DashboardReportController(svc.Object);
            SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

            var result = await ctrl.GetDashboard(companyId: 999);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetDashboardKpisAsync(999), Times.Once);
        }

        [Fact]
        public async Task GetKpis_Admin_AlwaysUsesJwtCompanyId()
        {
            // GetKpis has no companyId parameter — it's always safe.
            // This test confirms it still uses the JWT claim correctly.
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetDashboardKpisAsync(1))
               .ReturnsAsync(new DashboardKpiDto());

            var ctrl = new DashboardReportController(svc.Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.GetKpis();

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetDashboardKpisAsync(1), Times.Once);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // SalaryRegisterController
    // ══════════════════════════════════════════════════════════════════════

    public class SalaryRegisterIDOR
    {
        [Fact]
        public async Task Get_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetSalaryRegisterAsync(1, 7, 2026))
               .ReturnsAsync(new SalaryRegisterDto());

            var ctrl = new SalaryRegisterController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Get(companyId: 999, month: 7, year: 2026);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetSalaryRegisterAsync(1, 7, 2026), Times.Once);
            svc.Verify(s => s.GetSalaryRegisterAsync(999, It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Get_Superadmin_CanAccessAnyTenant()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetSalaryRegisterAsync(999, 7, 2026))
               .ReturnsAsync(new SalaryRegisterDto());

            var ctrl = new SalaryRegisterController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

            var result = await ctrl.Get(companyId: 999, month: 7, year: 2026);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetSalaryRegisterAsync(999, 7, 2026), Times.Once);
        }

        [Fact]
        public async Task Export_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.ExportSalaryRegisterAsync(1, 7, 2026))
               .ReturnsAsync(Array.Empty<byte>());

            var ctrl = new SalaryRegisterController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Export(companyId: 999, month: 7, year: 2026);

            Assert.IsType<FileContentResult>(result);
            svc.Verify(s => s.ExportSalaryRegisterAsync(1, 7, 2026), Times.Once);
            svc.Verify(s => s.ExportSalaryRegisterAsync(999, It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Get_InvalidMonth_Returns400()
        {
            var svc = new Mock<IReportService>();
            var ctrl = new SalaryRegisterController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Get(companyId: null, month: 13, year: 2026);

            Assert.IsType<BadRequestObjectResult>(result);
            svc.Verify(s => s.GetSalaryRegisterAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // LeaveReportController
    // ══════════════════════════════════════════════════════════════════════

    public class LeaveReportIDOR
    {
        [Fact]
        public async Task Monthly_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetLeaveReportAsync(1, 7, 2026))
               .ReturnsAsync(new LeaveReportDto());

            var ctrl = new LeaveReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            // Cross-tenant attack: admin from company 1 requests company 999's leave data
            var result = await ctrl.Monthly(companyId: 999, month: 7, year: 2026);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetLeaveReportAsync(1, 7, 2026), Times.Once);
            svc.Verify(s => s.GetLeaveReportAsync(999, It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Monthly_Superadmin_CanAccessAnyTenant()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.GetLeaveReportAsync(999, 7, 2026))
               .ReturnsAsync(new LeaveReportDto());

            var ctrl = new LeaveReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

            var result = await ctrl.Monthly(companyId: 999, month: 7, year: 2026);

            Assert.IsType<OkObjectResult>(result);
            svc.Verify(s => s.GetLeaveReportAsync(999, 7, 2026), Times.Once);
        }

        [Fact]
        public async Task Export_CrossTenantAdmin_UsesJwtCompanyId_NotQueryParam()
        {
            var svc = new Mock<IReportService>();
            svc.Setup(s => s.ExportLeaveReportAsync(1, 7, 2026))
               .ReturnsAsync(Array.Empty<byte>());

            var ctrl = new LeaveReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Export(companyId: 999, month: 7, year: 2026);

            Assert.IsType<FileContentResult>(result);
            svc.Verify(s => s.ExportLeaveReportAsync(1, 7, 2026), Times.Once);
            svc.Verify(s => s.ExportLeaveReportAsync(999, It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Monthly_InvalidMonth_Returns400()
        {
            var svc  = new Mock<IReportService>();
            var ctrl = new LeaveReportController(svc.Object, new Mock<IStreamingReportService>().Object);
            SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

            var result = await ctrl.Monthly(companyId: null, month: 13, year: 2026);

            Assert.IsType<BadRequestObjectResult>(result);
            svc.Verify(s => s.GetLeaveReportAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }
    }
}

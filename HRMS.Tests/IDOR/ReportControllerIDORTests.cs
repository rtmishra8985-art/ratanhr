using System.Security.Claims;
using HRMS.API.Controllers.Reports;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HRMS.Tests.IDOR;

/// <summary>
/// IDOR / cross-tenant isolation tests for all report controllers.
///
/// Each test verifies that a company-A admin CANNOT access company-B data by
/// supplying ?companyId=B — the controller must ignore the parameter and
/// always resolve the tenant from the JWT claim.
/// </summary>
public class ReportControllerIDORTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Creates an HttpContext whose User is an admin for <paramref name="companyId"/>.</summary>
    private static DefaultHttpContext AdminContext(int companyId) =>
        MakeContext(AppRoles.Admin, companyId.ToString());

    /// <summary>Creates an HttpContext whose User is a SuperAdmin (no companyId claim).</summary>
    private static DefaultHttpContext SuperAdminContext() =>
        MakeContext(AppRoles.SuperAdmin, companyId: null);

    private static DefaultHttpContext MakeContext(string role, string? companyId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "99"),
            new(ClaimTypes.Role,           role),
        };
        if (companyId is not null)
            claims.Add(new Claim("companyId", companyId));

        var identity  = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        return new DefaultHttpContext { User = principal };
    }

    private static T Wire<T>(T controller, DefaultHttpContext ctx)
        where T : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };
        return controller;
    }

    // ══════════════════════════════════════════════════════════════════════
    // AttendanceReportController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AttendanceReport_Monthly_AdminCannotRequestOtherCompany()
    {
        var svcMock = new Mock<IReportService>();
        svcMock.Setup(s => s.GetMonthlyAttendanceReportAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
               .ReturnsAsync(new List<MonthlyAttendanceReportDto>());

        var streamMock = new Mock<IStreamingReportService>();
        var ctrl = Wire(
            new AttendanceReportController(svcMock.Object, streamMock.Object),
            AdminContext(companyId: 1));

        // Admin for company 1 requests companyId=2 — must be ignored.
        await ctrl.Monthly(companyId: 2, month: 7, year: 2026);

        // Service must be called with 1 (JWT claim), NOT 2 (query param).
        svcMock.Verify(s =>
            s.GetMonthlyAttendanceReportAsync(1, 7, 2026),
            Times.Once);
    }

    [Fact]
    public async Task AttendanceReport_Monthly_SuperAdminCanTargetSpecificCompany()
    {
        var svcMock = new Mock<IReportService>();
        svcMock.Setup(s => s.GetMonthlyAttendanceReportAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
               .ReturnsAsync(new List<MonthlyAttendanceReportDto>());

        var streamMock = new Mock<IStreamingReportService>();
        var ctrl = Wire(
            new AttendanceReportController(svcMock.Object, streamMock.Object),
            SuperAdminContext());

        await ctrl.Monthly(companyId: 5, month: 7, year: 2026);

        // SuperAdmin: no claim override — query param should be honoured.
        svcMock.Verify(s =>
            s.GetMonthlyAttendanceReportAsync(5, 7, 2026),
            Times.Once);
    }

    // ══════════════════════════════════════════════════════════════════════
    // LeaveReportController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LeaveReport_Monthly_AdminCannotRequestOtherCompany()
    {
        var svcMock = new Mock<IReportService>();
        svcMock.Setup(s => s.GetLeaveReportAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
               .ReturnsAsync(new LeaveReportDto());

        var streamMock = new Mock<IStreamingReportService>();
        var ctrl = Wire(
            new LeaveReportController(svcMock.Object, streamMock.Object),
            AdminContext(companyId: 10));

        await ctrl.Monthly(companyId: 99, month: 1, year: 2026);

        // Must resolve to 10, not 99.
        svcMock.Verify(s =>
            s.GetLeaveReportAsync(10, 1, 2026),
            Times.Once);
    }

    // ══════════════════════════════════════════════════════════════════════
    // PayrollReportController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PayrollReport_Monthly_AdminCannotRequestOtherCompany()
    {
        var svcMock = new Mock<IReportService>();
        svcMock.Setup(s => s.GetPayrollReportAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
               .ReturnsAsync(new PayrollReportDto());

        var streamMock = new Mock<IStreamingReportService>();
        var ctrl = Wire(
            new PayrollReportController(svcMock.Object, streamMock.Object),
            AdminContext(companyId: 3));

        await ctrl.Monthly(companyId: 7, month: 6, year: 2026);

        svcMock.Verify(s =>
            s.GetPayrollReportAsync(3, 6, 2026),
            Times.Once);
    }

    // ══════════════════════════════════════════════════════════════════════
    // SalaryRegisterController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SalaryRegister_AdminCannotRequestOtherCompany()
    {
        var svcMock = new Mock<IReportService>();
        svcMock.Setup(s => s.GetSalaryRegisterAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
               .ReturnsAsync(new SalaryRegisterDto());

        var streamMock = new Mock<IStreamingReportService>();
        var ctrl = Wire(
            new SalaryRegisterController(svcMock.Object, streamMock.Object),
            AdminContext(companyId: 2));

        await ctrl.Get(companyId: 50, month: 3, year: 2026);

        svcMock.Verify(s =>
            s.GetSalaryRegisterAsync(2, 3, 2026),
            Times.Once);
    }

    // ══════════════════════════════════════════════════════════════════════
    // DashboardReportController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DashboardReport_AdminCannotRequestOtherCompany()
    {
        var svcMock = new Mock<IReportService>();
        svcMock.Setup(s => s.GetDashboardKpisAsync(It.IsAny<int?>()))
               .ReturnsAsync(new DashboardKpiDto());

        var ctrl = Wire(
            new DashboardReportController(svcMock.Object),
            AdminContext(companyId: 4));

        await ctrl.GetDashboard(companyId: 99);

        svcMock.Verify(s => s.GetDashboardKpisAsync(4), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════════════
    // EmployeeReportController (P2-B — previously missing from this suite)
    // ══════════════════════════════════════════════════════════════════════
    // Reference pattern: AnalyticsController.ResolveCompanyId()
    // See: HRMS.API/Controllers/Analytics/AnalyticsController.cs

    [Fact]
    public async Task EmployeeReport_Summary_AdminCannotRequestOtherCompany()
    {
        var svcMock = new Mock<IReportService>();
        svcMock.Setup(s => s.GetEmployeeSummaryReportAsync(It.IsAny<int?>()))
               .ReturnsAsync(new EmployeeSummaryReportDto());

        var streamMock = new Mock<IStreamingReportService>();
        var ctrl = Wire(
            new EmployeeReportController(svcMock.Object, streamMock.Object),
            AdminContext(companyId: 7));

        await ctrl.Summary(companyId: 99);

        // Must resolve to 7 (JWT claim), not 99 (query param).
        svcMock.Verify(s =>
            s.GetEmployeeSummaryReportAsync(7),
            Times.Once);
    }

    [Fact]
    public async Task EmployeeReport_Summary_SuperAdminCanTargetSpecificCompany()
    {
        var svcMock = new Mock<IReportService>();
        svcMock.Setup(s => s.GetEmployeeSummaryReportAsync(It.IsAny<int?>()))
               .ReturnsAsync(new EmployeeSummaryReportDto());

        var streamMock = new Mock<IStreamingReportService>();
        var ctrl = Wire(
            new EmployeeReportController(svcMock.Object, streamMock.Object),
            SuperAdminContext());

        await ctrl.Summary(companyId: 33);

        // SuperAdmin: no JWT override — query param is honoured.
        svcMock.Verify(s =>
            s.GetEmployeeSummaryReportAsync(33),
            Times.Once);
    }

}

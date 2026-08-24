using System.Security.Claims;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using HRMS.API.Controllers.Reports;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HRMS.Tests.Reports;

/// <summary>
/// FIX TEST-04 (SEC-01): Regression tests verifying that all report controllers
/// return an empty/403 result — not cross-tenant data — when the caller's JWT
/// companyId claim is absent or malformed.
///
/// Before the SEC-01 fix the shadow 'private new int? CompanyId' returned null
/// on parse failure, which was identical to the SuperAdmin bypass path. This
/// caused EffectiveCompanyId(?companyId=X) to fall back to the attacker-supplied
/// query parameter X, exposing cross-tenant report data.
///
/// After the fix the shadow returns -1 (fail-closed sentinel). No company has
/// PK == -1 (auto-increment starts at 1), so all report service calls receive
/// companyId = -1 and must return empty / 404 results.
/// </summary>
public class ReportControllerIDORTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an HttpContext for a non-SuperAdmin user whose 'companyId' JWT claim
    /// is deliberately absent (simulates a malformed/missing claim).
    /// </summary>
    private static DefaultHttpContext MakeContextNoCompanyClaim(string role = "Admin")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Role, role),
            // Intentionally omitted: "companyId" claim
        };
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
    }

    private static DefaultHttpContext MakeAdminContext(int companyId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("companyId", companyId.ToString()),
        };
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
    }

    // ── AttendanceReportController ─────────────────────────────────────────

    [Fact]
    public async Task AttendanceReport_Monthly_MalformedClaim_ServiceReceivesNegativeOne()
    {
        // Arrange
        var capturedCompanyId = (int?)0; // sentinel before capture

        var svcMock = new Mock<IReportService>();
        svcMock
            .Setup(s => s.GetMonthlyAttendanceReportAsync(
                It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<int?, int, int>((cid, _, __) => capturedCompanyId = cid)
            .ReturnsAsync(new List<MonthlyAttendanceReportDto>());

        var streamingMock = new Mock<IStreamingReportService>();
        var ctrl = new AttendanceReportController(svcMock.Object, streamingMock.Object);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeContextNoCompanyClaim()
        };

        // Act — attacker supplies companyId = 99 via query param
        await ctrl.Monthly(companyId: 99, month: 1, year: 2026);

        // Assert — service must receive -1, NOT 99
        Assert.Equal(-1, capturedCompanyId);
    }

    [Fact]
    public async Task AttendanceReport_Monthly_ValidClaim_ServiceReceivesCorrectId()
    {
        // Arrange — normal admin with a valid companyId claim
        var capturedCompanyId = (int?)0;

        var svcMock = new Mock<IReportService>();
        svcMock
            .Setup(s => s.GetMonthlyAttendanceReportAsync(
                It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<int?, int, int>((cid, _, __) => capturedCompanyId = cid)
            .ReturnsAsync(new List<MonthlyAttendanceReportDto>());

        var streamingMock = new Mock<IStreamingReportService>();
        var ctrl = new AttendanceReportController(svcMock.Object, streamingMock.Object);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeAdminContext(companyId: 5)
        };

        // Act — companyId query param is ignored; JWT claim (5) takes precedence
        await ctrl.Monthly(companyId: 99, month: 1, year: 2026);

        // Assert — service receives the JWT claim value, NOT the attacker-supplied 99
        Assert.Equal(5, capturedCompanyId);
    }

    // ── PayrollReportController ────────────────────────────────────────────

    [Fact]
    public async Task PayrollReport_MalformedClaim_ServiceReceivesNegativeOne()
    {
        var capturedCompanyId = (int?)0;

        var svcMock = new Mock<IReportService>();
        svcMock
            .Setup(s => s.GetPayrollReportAsync(
                It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<int?, int, int>((cid, _, __) => capturedCompanyId = cid)
            .ReturnsAsync(new PayrollReportDto());

        var streamingMock = new Mock<IStreamingReportService>();
        var ctrl = new PayrollReportController(svcMock.Object, streamingMock.Object);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeContextNoCompanyClaim()
        };

        await ctrl.Monthly(companyId: 7, month: 3, year: 2026);

        Assert.Equal(-1, capturedCompanyId);
    }

    // ── LeaveReportController ──────────────────────────────────────────────

    [Fact]
    public async Task LeaveReport_MalformedClaim_ServiceReceivesNegativeOne()
    {
        var capturedCompanyId = (int?)0;

        var svcMock = new Mock<IReportService>();
        svcMock
            .Setup(s => s.GetLeaveReportAsync(
                It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<int?, int, int>((cid, _, __) => capturedCompanyId = cid)
            .ReturnsAsync(new LeaveReportDto());

        var streamingMock = new Mock<IStreamingReportService>();
        var ctrl = new LeaveReportController(svcMock.Object, streamingMock.Object);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeContextNoCompanyClaim()
        };

        await ctrl.Monthly(companyId: 12, month: 6, year: 2026);

        Assert.Equal(-1, capturedCompanyId);
    }
}

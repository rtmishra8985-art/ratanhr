using System.Security.Claims;
using HRMS.API.Controllers.Attendance;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces.Biometric;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HRMS.Tests.IDOR;

/// <summary>
/// Regression tests for RHR: <see cref="BiometricController"/> previously passed
/// the raw BaseController.CompanyId sentinel (-1 for SuperAdmin-without-tenant)
/// straight into IBiometricDeviceService / IBiometricSyncService methods that take
/// a non-nullable int companyId with no "unrestricted" escape hatch. A SuperAdmin
/// calling Sync/GetSettings/UpdateSettings/GetDashboard/GetRealtime without first
/// impersonating a tenant got a silent, misleading empty/wrong-scoped result
/// instead of a clear error. Fixed with an explicit TryGetCompanyId() guard
/// mirroring AssetsController's existing pattern for the same class of module.
/// </summary>
public class BiometricControllerIDORTests
{
    private const int CompanyA = 10;

    [Fact]
    public async Task GetSettings_SuperAdminWithoutTenantContext_Returns403()
    {
        var (controller, deviceSvc, _) = BuildController(companyId: null, isSuperAdmin: true);

        var result = await controller.GetSettings(CancellationToken.None);

        var actionResult = Assert.IsType<ActionResult<ApiResponse<HRMS.Application.DTOs.Attendance.BiometricSettingsDto>>>(result);
        Assert.IsType<ForbidResult>(actionResult.Result);
        deviceSvc.Verify(s => s.GetSettingsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDashboard_SuperAdminWithoutTenantContext_Returns403()
    {
        var (controller, deviceSvc, _) = BuildController(companyId: null, isSuperAdmin: true);

        var result = await controller.GetDashboard(CancellationToken.None);

        var actionResult = Assert.IsType<ActionResult<ApiResponse<HRMS.Application.DTOs.Attendance.BiometricDashboardDto>>>(result);
        Assert.IsType<ForbidResult>(actionResult.Result);
        deviceSvc.Verify(s => s.GetDashboardAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sync_SuperAdminWithoutTenantContext_Returns403()
    {
        var (controller, _, syncSvc) = BuildController(companyId: null, isSuperAdmin: true);

        var result = await controller.Sync(
            new BiometricSyncRequest { Vendor = "ZKTeco", From = DateTime.UtcNow.AddDays(-1), To = DateTime.UtcNow },
            CancellationToken.None);

        var actionResult = Assert.IsType<ActionResult<ApiResponse<HRMS.Application.DTOs.Attendance.BiometricSyncResultDto>>>(result);
        Assert.IsType<ForbidResult>(actionResult.Result);
        syncSvc.Verify(s => s.SyncAttendanceAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetSettings_RegularAdminWithCompanyClaim_ReturnsOwnCompanySettings()
    {
        var (controller, deviceSvc, _) = BuildController(companyId: CompanyA, isSuperAdmin: false);
        deviceSvc.Setup(s => s.GetSettingsAsync(CompanyA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HRMS.Application.DTOs.Attendance.BiometricSettingsDto(
                Id: 1, CompanyId: CompanyA, AutoSyncEnabled: true, SyncIntervalMinutes: 15,
                SyncLookbackDays: 1, GraceTimeMinutes: 10, MinHalfDayHours: 4,
                EnableDuplicatePunchDetection: true, DedupeWindowMinutes: 2,
                QueueUnknownEmployees: false, RealtimeEnabled: false, PersistRawLogs: true,
                LogRetentionDays: 90, UpdatedAt: DateTime.UtcNow));

        var result = await controller.GetSettings(CancellationToken.None);

        var actionResult = Assert.IsType<ActionResult<ApiResponse<HRMS.Application.DTOs.Attendance.BiometricSettingsDto>>>(result);
        Assert.IsType<OkObjectResult>(actionResult.Result);
        deviceSvc.Verify(s => s.GetSettingsAsync(CompanyA, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    private static (BiometricController controller, Mock<IBiometricDeviceService> deviceSvc, Mock<IBiometricSyncService> syncSvc) BuildController(
        int? companyId, bool isSuperAdmin)
    {
        var syncSvc = new Mock<IBiometricSyncService>();
        var factory = new Mock<IBiometricProviderFactory>();
        var deviceSvc = new Mock<IBiometricDeviceService>();
        var config = new ConfigurationBuilder().Build();

        var controller = new BiometricController(
            syncSvc.Object,
            factory.Object,
            deviceSvc.Object,
            NullLogger<BiometricController>.Instance,
            config);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-1"),
            new(ClaimTypes.Name, "testuser"),
        };

        if (companyId.HasValue)
            claims.Add(new Claim("companyId", companyId.Value.ToString()));

        claims.Add(new Claim(ClaimTypes.Role, isSuperAdmin ? AppRoles.SuperAdmin : AppRoles.Admin));

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return (controller, deviceSvc, syncSvc);
    }
}

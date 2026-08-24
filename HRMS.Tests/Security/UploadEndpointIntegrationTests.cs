// Endpoint-level upload validation integration tests (security/upload-validation audit).
//
// EmployeeSelfControllerIdorIntegrationTests.cs already established the pattern for
// exercising the *full* HTTP pipeline (auth middleware -> routing -> [Authorize] ->
// controller -> service -> EF Core) against an in-memory database via
// WebApplicationFactory<Program>. These tests reuse that same pattern, adding a
// controllable, per-factory-instance file storage root so "the rejected file was
// never persisted" can be asserted directly against disk rather than inferred from
// the HTTP status code alone.
//
// Every endpoint tested here validates through the single shared UploadValidator
// (HRMS.Infrastructure.Security.UploadValidator) — no per-endpoint validation logic
// is duplicated by these tests; they only assert on the HTTP-visible outcome
// (status code, body, and filesystem side effects) of that shared gate.
//
// Coverage in this file:
//   - CompanyController.UploadLogo     (POST /api/companies/{id}/logo)        — Image profile
//       * explicitly required by the audit: SVG rejection + no-persistence regression
//   - LogoController.Upload            (POST /api/logo/{companyId})          — Image profile
//   - ProfileController.UploadPicture  (POST /api/profile/picture)           — Image profile
//   - AttendanceController.UploadExcel (POST /api/attendance/excel/upload)   — Spreadsheet profile
//
// NOT yet covered by endpoint-level HTTP tests in this pass (helper-level coverage
// for all of these already exists in UploadValidatorTests.cs and UploadSizeLimitTests.cs,
// which exercise the exact same shared UploadValidator/FileStorageService gate):
//   - AppreciationController.Create (POST /api/appreciation)
//   - EmployeeDocumentController.Upload (POST /api/employees/{employeeId}/documents)
//   - ExpenseController.Create / SubmitLegacy (receipt attachments)
//   - EmployeeController.Create / Update (onboarding document collection)
// See Documentation/UploadValidationCoverage.md for the full endpoint inventory and
// the exact reason each remaining row is still BLOCKED rather than claimed as PASS.
using System.Net;
using System.Net.Http.Headers;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace HRMS.Tests.Security;

/// <summary>
/// Same shape as <see cref="HrmsTestWebAppFactory"/>, plus a dedicated temp directory
/// for <c>FileStorage:RootPath</c> so tests can assert "no file was written" by
/// listing the actual filesystem rather than trusting the HTTP response alone.
/// </summary>
public sealed class UploadEndpointTestWebAppFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _dbName = "hrms_upload_test_" + Guid.NewGuid();

    /// <summary>Root directory this factory's IFileStorageService writes under. Empty until a save succeeds.</summary>
    public string StorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), "hrms-upload-endpoint-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Number of files currently under <see cref="StorageRoot"/>. The factory is a
    /// class fixture shared by every test in this file, so an absolute
    /// "no file exists" assertion would break as soon as one valid-upload test
    /// legitimately writes a file. Tests therefore assert that a *rejected* request
    /// adds no new file (count delta of zero), which is the property under test.
    /// </summary>
    public int PersistedFileCount() =>
        Directory.Exists(StorageRoot)
            ? Directory.EnumerateFiles(StorageRoot, "*", SearchOption.AllDirectories).Count()
            : 0;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var (priv, pub) = TestHostEnvironment.Apply();
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PrivateKeyPem"]      = priv,
                ["Jwt:PublicKeyPem"]       = pub,
                ["Jwt:Issuer"]             = "hrms-test",
                ["Jwt:Audience"]           = "hrms-test",
                ["Hangfire:UseInMemory"]   = "true",
                // Redirect all uploads for this factory instance to an isolated temp
                // directory so persistence can be asserted directly. Kept identical
                // to production's FileUpload:* allow-list/size settings via appsettings.json.
                ["FileStorage:RootPath"]   = StorageRoot,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContextFactory<ApplicationDbContext>(opts =>
                opts.UseInMemoryDatabase(_dbName));

            services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            services.AddDistributedMemoryCache();

            services.RemoveAll<Microsoft.AspNetCore.Hosting.IStartupFilter>();

            // AttendanceController.UploadExcel parses via IStreamingReportService before
            // any UploadValidator-rejected file would ever reach it. For the one "valid
            // upload succeeds" case we stub it so the test does not depend on a real
            // Open XML SAX parse of a hand-built .xlsx byte stream — the thing under
            // test is the HTTP-layer validation gate, not the Excel parser.
            services.RemoveAll<IStreamingReportService>();
            var streamingStub = new Mock<IStreamingReportService>();
            streamingStub
                .Setup(s => s.ReadAttendanceUploadRowsAsync(
                    It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<HRMS.Application.Interfaces.AttendanceExcelRow>)
                    new List<HRMS.Application.Interfaces.AttendanceExcelRow>());
            services.AddSingleton(streamingStub.Object);

            services.RemoveAll<IClamAvVirusScanService>();
            var clamAvStub = new Mock<IClamAvVirusScanService>();
            clamAvStub
                .Setup(s => s.ScanAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ScanResult(IsClean: true, Threat: null));
            services.AddSingleton(clamAvStub.Object);
        });

        builder.ConfigureTestServices(services =>
        {
            services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(opts =>
            {
                opts.DefaultAuthenticateScheme = "Test";
                opts.DefaultChallengeScheme    = "Test";
            });
            services.AddAuthentication("Test")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                        "Test", _ => { });
        });
    }

    public new void Dispose()
    {
        try { if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, recursive: true); }
        catch { /* best-effort cleanup */ }
        base.Dispose();
    }
}

// ── Claims helper ──────────────────────────────────────────────────────────
// EmployeeSelfControllerIdorIntegrationTests.cs declares its equivalent helper as a
// `file static class`, so it is not visible outside that file. This is a local copy
// producing the identical "X-Test-Claims" header that TestAuthHandler consumes.
file static class UploadTestClaimsHelper
{
    public static HttpRequestMessage WithClaims(this HttpRequestMessage req, string role,
        int companyId, int userId = 1, string? employeeId = null)
    {
        var claims = new List<object>
        {
            new { type = System.Security.Claims.ClaimTypes.NameIdentifier, value = userId.ToString() },
            new { type = System.Security.Claims.ClaimTypes.Role,           value = role },
            new { type = "companyId",                                      value = companyId.ToString() },
        };
        if (employeeId != null)
            claims.Add(new { type = "employeeId", value = employeeId });

        var json = System.Text.Json.JsonSerializer.Serialize(claims);
        req.Headers.Add("X-Test-Claims",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)));
        return req;
    }
}

public class UploadEndpointIntegrationTests
    : IClassFixture<UploadEndpointTestWebAppFactory>
{
    private readonly UploadEndpointTestWebAppFactory _factory;
    private readonly HttpClient _client;

    public UploadEndpointIntegrationTests(UploadEndpointTestWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Byte fixtures ────────────────────────────────────────────────────────
    private static readonly byte[] ValidPngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] ValidXlsxBytes =
        [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    // Declares itself PNG via extension/Content-Type but the real bytes are a JPEG —
    // exactly the spoofed magic-number scenario the audit requires be rejected.
    private static readonly byte[] SpoofedMagicBytes =
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    private static readonly byte[] SvgBytes =
        System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>");
    private static readonly byte[] ExeBytes =
        [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00]; // MZ header

    private async Task SeedCompany(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.Companies.AnyAsync(c => c.Id == id))
        {
            db.Companies.Add(new HRMS.Domain.Entities.Company.Company
            {
                CompanyId = id,
                Name      = $"Upload Test Co {id}",
                IsActive  = true
            });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Sends a request that is expected to be rejected, and asserts the shared storage
    /// root gained no new file as a result — i.e. the rejected upload was never
    /// persisted. Uses a count delta because the storage root is shared by the whole
    /// class fixture (see UploadEndpointTestWebAppFactory.PersistedFileCount).
    /// </summary>
    private async Task<HttpResponseMessage> SendAssertingNoNewFile(HttpRequestMessage req)
    {
        var before   = _factory.PersistedFileCount();
        var response = await _client.SendAsync(req);
        Assert.Equal(before, _factory.PersistedFileCount());
        return response;
    }

    private static MultipartFormDataContent BuildMultipart(
        string fieldName, string fileName, string contentType, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, fieldName, fileName);
        return form;
    }

    // ── CompanyController.UploadLogo — POST /api/companies/{id}/logo ──────────
    // Image profile: .jpg/.jpeg/.png/.webp/.gif only, SVG explicitly excluded.

    [Fact]
    public async Task UploadLogo_ValidPng_Succeeds()
    {
        await SeedCompany(101);
        using var form = BuildMultipart("logo", "logo.png", "image/png", ValidPngBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/companies/101/logo") { Content = form }
            .WithClaims(role: "admin", companyId: 101);

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadLogo_SpoofedMagicNumber_Returns400()
    {
        await SeedCompany(102);
        // Extension/Content-Type both claim PNG; real bytes are a JPEG signature.
        using var form = BuildMultipart("logo", "logo.png", "image/png", SpoofedMagicBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/companies/102/logo") { Content = form }
            .WithClaims(role: "admin", companyId: 102);

        var response = await SendAssertingNoNewFile(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadLogo_DangerousExtension_Returns400_AndDoesNotPersist()
    {
        await SeedCompany(103);
        using var form = BuildMultipart("logo", "logo.exe", "application/octet-stream", ExeBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/companies/103/logo") { Content = form }
            .WithClaims(role: "admin", companyId: 103);

        var response = await SendAssertingNoNewFile(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Explicitly required by the audit: SVG rejection + no-persistence regression test.
    [Fact]
    public async Task UploadLogo_Svg_Returns400_AndDoesNotPersist()
    {
        await SeedCompany(104);
        using var form = BuildMultipart("logo", "logo.svg", "image/svg+xml", SvgBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/companies/104/logo") { Content = form }
            .WithClaims(role: "admin", companyId: 104);

        var response = await SendAssertingNoNewFile(req);

        // The Image profile's AllowedExtensions list does not include ".svg" — see
        // UploadProfile.Image in HRMS.Infrastructure/Security/UploadValidator.cs. This
        // is intentional: an inline-served SVG is a stored-XSS vector (it can carry
        // <script> as shown in SvgBytes above), so it is rejected at the extension
        // gate before MIME/magic-byte checks are even reached.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not allowed", body, StringComparison.OrdinalIgnoreCase);

    }

    [Fact]
    public async Task UploadLogo_CrossTenantId_Returns404_RegardlessOfFileValidity()
    {
        await SeedCompany(105);
        await SeedCompany(106);
        using var form = BuildMultipart("logo", "logo.png", "image/png", ValidPngBytes);
        // Admin scoped to company 105 attempts to upload a logo for company 106.
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/companies/106/logo") { Content = form }
            .WithClaims(role: "admin", companyId: 105);

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadLogo_Unauthenticated_Returns401()
    {
        using var form = BuildMultipart("logo", "logo.png", "image/png", ValidPngBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/companies/107/logo") { Content = form };

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── AttendanceController.UploadExcel — POST /api/attendance/excel/upload ──
    // Spreadsheet profile: .xlsx/.xls only. Does not persist through FileStorageService
    // at all (parses in-memory), so "no persistence" is structural rather than asserted
    // via StorageRoot for this endpoint.

    [Fact]
    public async Task UploadExcel_ValidXlsx_Succeeds()
    {
        using var form = BuildMultipart("file", "attendance.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ValidXlsxBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/attendance/excel/upload") { Content = form }
            .WithClaims(role: "admin", companyId: 201);

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadExcel_SpoofedMagicNumber_Returns400()
    {
        // Claims to be .xlsx but the real bytes are a JPEG signature.
        using var form = BuildMultipart("file", "attendance.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", SpoofedMagicBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/attendance/excel/upload") { Content = form }
            .WithClaims(role: "admin", companyId: 202);

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadExcel_DangerousExtension_Returns400()
    {
        using var form = BuildMultipart("file", "attendance.exe", "application/octet-stream", ExeBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/attendance/excel/upload") { Content = form }
            .WithClaims(role: "admin", companyId: 203);

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadExcel_Svg_Returns400()
    {
        // SVG is not part of the Spreadsheet profile's allow-list either.
        using var form = BuildMultipart("file", "attendance.svg", "image/svg+xml", SvgBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/attendance/excel/upload") { Content = form }
            .WithClaims(role: "admin", companyId: 204);

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadExcel_EmployeeRole_Returns403()
    {
        using var form = BuildMultipart("file", "attendance.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ValidXlsxBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/attendance/excel/upload") { Content = form }
            .WithClaims(role: "employee", companyId: 205);

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── LogoController.Upload — POST /api/logo/{companyId} ────────────────────
    // Image profile via [FromForm] UploadLogoRequest wrapper. Also carries
    // [EnableRateLimiting("upload")] (BLOCKER-11) — left in place; these tests
    // do not disable or bypass the rate limiter.

    [Fact]
    public async Task LogoController_ValidPng_Succeeds()
    {
        await SeedCompany(301);
        using var form = BuildMultipart("Logo", "logo.png", "image/png", ValidPngBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/logo/301") { Content = form }
            .WithClaims(role: "admin", companyId: 301);

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LogoController_Svg_Returns400_AndDoesNotPersist()
    {
        await SeedCompany(302);
        using var form = BuildMultipart("Logo", "logo.svg", "image/svg+xml", SvgBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/logo/302") { Content = form }
            .WithClaims(role: "admin", companyId: 302);

        var response = await SendAssertingNoNewFile(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LogoController_SpoofedMagicNumber_Returns400()
    {
        await SeedCompany(303);
        using var form = BuildMultipart("Logo", "logo.png", "image/png", SpoofedMagicBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/logo/303") { Content = form }
            .WithClaims(role: "admin", companyId: 303);

        var response = await SendAssertingNoNewFile(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LogoController_DangerousExtension_Returns400()
    {
        await SeedCompany(304);
        using var form = BuildMultipart("Logo", "logo.exe", "application/octet-stream", ExeBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/logo/304") { Content = form }
            .WithClaims(role: "admin", companyId: 304);

        var response = await SendAssertingNoNewFile(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LogoController_CrossTenant_Returns403()
    {
        await SeedCompany(305);
        await SeedCompany(306);
        using var form = BuildMultipart("Logo", "logo.png", "image/png", ValidPngBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/logo/306") { Content = form }
            .WithClaims(role: "admin", companyId: 305);

        var response = await _client.SendAsync(req);

        // LogoController.CallerOwnsCompany returns Forbid() (403) rather than the
        // 404-shaped IDOR response CompanyController uses — this reflects a genuine
        // difference between the two controllers' existing IDOR strategies, not a bug
        // introduced here.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── ProfileController.UploadPicture — POST /api/profile/picture ───────────
    // Image profile; persists through IAuthService.UpdateProfilePictureAsync ->
    // FileStorageService, so no-persistence is asserted against StorageRoot as with
    // CompanyController.UploadLogo.

    private async Task<int> SeedUser(int companyId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new HRMS.Domain.Entities.Authentication.User
        {
            Email    = $"upload-test-{Guid.NewGuid():N}@example.com",
            PasswordHash = "not-a-real-hash",
            Role     = "admin",
            CompanyId = companyId,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task UploadPicture_ValidPng_Succeeds()
    {
        var userId = await SeedUser(401);
        using var form = BuildMultipart("file", "avatar.png", "image/png", ValidPngBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/picture") { Content = form }
            .WithClaims(role: "admin", companyId: 401, userId: userId);

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadPicture_Svg_Returns400_AndDoesNotPersist()
    {
        var userId = await SeedUser(402);
        using var form = BuildMultipart("file", "avatar.svg", "image/svg+xml", SvgBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/picture") { Content = form }
            .WithClaims(role: "admin", companyId: 402, userId: userId);

        var response = await SendAssertingNoNewFile(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadPicture_SpoofedMagicNumber_Returns400()
    {
        var userId = await SeedUser(403);
        using var form = BuildMultipart("file", "avatar.png", "image/png", SpoofedMagicBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/picture") { Content = form }
            .WithClaims(role: "admin", companyId: 403, userId: userId);

        var response = await SendAssertingNoNewFile(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadPicture_DangerousExtension_Returns400()
    {
        var userId = await SeedUser(404);
        using var form = BuildMultipart("file", "avatar.exe", "application/octet-stream", ExeBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/picture") { Content = form }
            .WithClaims(role: "admin", companyId: 404, userId: userId);

        var response = await SendAssertingNoNewFile(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

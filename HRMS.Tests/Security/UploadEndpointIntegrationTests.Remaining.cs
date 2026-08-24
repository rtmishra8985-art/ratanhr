using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HRMS.Infrastructure.Data;
using HRMS.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HRMS.Tests.Security;

/// <summary>
/// Endpoint-level HTTP coverage for upload routes not included in the first
/// upload-validation integration-test pass. Each rejected request is also
/// checked against the isolated FileStorage root, proving that validation
/// happens before persistence.
/// </summary>
public sealed class UploadEndpointIntegrationTestsRemaining
    : IClassFixture<UploadEndpointTestWebAppFactory>
{
    private readonly UploadEndpointTestWebAppFactory _factory;
    private readonly HttpClient _client;

    private static readonly byte[] ValidPngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] ValidPdfBytes =
        [(byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-', (byte)'1', (byte)'.', (byte)'7'];
    private static readonly byte[] SpoofedMagicBytes =
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    private static readonly byte[] ExeBytes =
        [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
    private static readonly byte[] SvgBytes =
        Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>");

    public UploadEndpointIntegrationTestsRemaining(UploadEndpointTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
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

    private static MultipartFormDataContent BuildForm(
        IEnumerable<(string Name, string Value)> fields,
        string fileField,
        string fileName,
        string contentType,
        byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields)
            form.Add(new StringContent(value), name);

        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, fileField, fileName);
        return form;
    }

    private static HttpRequestMessage WithClaims(
        HttpRequestMessage request,
        string role,
        int companyId,
        int userId = 1,
        string? employeeId = null)
    {
        var claims = new List<object>
        {
            new { type = System.Security.Claims.ClaimTypes.NameIdentifier, value = userId.ToString() },
            new { type = System.Security.Claims.ClaimTypes.Role, value = role },
            new { type = "companyId", value = companyId.ToString() },
        };
        if (employeeId is not null)
            claims.Add(new { type = "employeeId", value = employeeId });

        var json = JsonSerializer.Serialize(claims);
        request.Headers.Add(
            "X-Test-Claims",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
        return request;
    }

    private async Task<HttpResponseMessage> SendAssertingNoNewFile(HttpRequestMessage request)
    {
        var before = _factory.PersistedFileCount();
        var response = await _client.SendAsync(request);
        Assert.Equal(before, _factory.PersistedFileCount());
        return response;
    }

    private async Task SeedEmployee(string employeeCode, int companyId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.Employees.AnyAsync(e => e.EmployeeCode == employeeCode))
            return;

        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = employeeCode,
            CompanyId = companyId,
            FullName = "Upload Endpoint Employee",
            Designation = "Tester",
            Department = "Security",
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }

    private static void AddEmployeeFields(MultipartFormDataContent form, string fullName = "New Upload Employee")
    {
        form.Add(new StringContent(fullName), "FullName");
        form.Add(new StringContent("Tester"), "Designation");
        form.Add(new StringContent("Security"), "Department");
    }

    private static void AddExpenseFields(MultipartFormDataContent form)
    {
        form.Add(new StringContent("Travel claim"), "Title");
        form.Add(new StringContent("INR"), "Currency");
        form.Add(new StringContent("Food"), "Items[0].Category");
        form.Add(new StringContent("Client lunch"), "Items[0].Description");
        form.Add(new StringContent("100"), "Items[0].Amount");
        form.Add(new StringContent("18"), "Items[0].GstAmount");
        form.Add(new StringContent("INR"), "Items[0].Currency");
        form.Add(new StringContent("2026-08-11"), "Items[0].ExpenseDate");
    }

    // ── POST /api/appreciation ───────────────────────────────────────────────

    [Fact]
    public async Task Appreciation_ValidPng_Succeeds()
    {
        using var form = BuildForm(
            [("employeeId", "EMP-APP-001"), ("message", "Thank you")],
            "file", "appreciation.png", "image/png", ValidPngBytes);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/appreciation") { Content = form },
            "admin", 501);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Appreciation_SpoofedMagicNumber_Returns400_AndDoesNotPersist()
    {
        using var form = BuildForm(
            [("employeeId", "EMP-APP-002")],
            "file", "appreciation.png", "image/png", SpoofedMagicBytes);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/appreciation") { Content = form },
            "admin", 502);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Appreciation_DangerousExtension_Returns400_AndDoesNotPersist()
    {
        using var form = BuildForm(
            [("employeeId", "EMP-APP-003")],
            "file", "appreciation.exe", "application/octet-stream", ExeBytes);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/appreciation") { Content = form },
            "admin", 503);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Appreciation_Svg_Returns400_AndDoesNotPersist()
    {
        using var form = BuildForm(
            [("employeeId", "EMP-APP-004")],
            "file", "appreciation.svg", "image/svg+xml", SvgBytes);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/appreciation") { Content = form },
            "admin", 504);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── POST /api/employees/{employeeId}/documents ───────────────────────────

    [Fact]
    public async Task EmployeeDocument_ValidPdf_Succeeds()
    {
        await SeedEmployee("EMP-DOC-001", 601);
        using var form = BuildForm(
            [("DocumentType", "Other"), ("Notes", "Upload test")],
            "file", "document.pdf", "application/pdf", ValidPdfBytes);
        using var request = WithClaims(
            new HttpRequestMessage(
                HttpMethod.Post, "/api/employees/EMP-DOC-001/documents") { Content = form },
            "admin", 601);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeDocument_SpoofedMagicNumber_Returns400_AndDoesNotPersist()
    {
        await SeedEmployee("EMP-DOC-002", 602);
        using var form = BuildForm(
            [("DocumentType", "Other")],
            "file", "document.pdf", "application/pdf", SpoofedMagicBytes);
        using var request = WithClaims(
            new HttpRequestMessage(
                HttpMethod.Post, "/api/employees/EMP-DOC-002/documents") { Content = form },
            "admin", 602);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeDocument_DangerousExtension_Returns400_AndDoesNotPersist()
    {
        await SeedEmployee("EMP-DOC-003", 603);
        using var form = BuildForm(
            [("DocumentType", "Other")],
            "file", "document.exe", "application/octet-stream", ExeBytes);
        using var request = WithClaims(
            new HttpRequestMessage(
                HttpMethod.Post, "/api/employees/EMP-DOC-003/documents") { Content = form },
            "admin", 603);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeDocument_Svg_Returns400_AndDoesNotPersist()
    {
        await SeedEmployee("EMP-DOC-004", 604);
        using var form = BuildForm(
            [("DocumentType", "Other")],
            "file", "document.svg", "image/svg+xml", SvgBytes);
        using var request = WithClaims(
            new HttpRequestMessage(
                HttpMethod.Post, "/api/employees/EMP-DOC-004/documents") { Content = form },
            "admin", 604);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── POST /api/expenses and /api/expenses/legacy ──────────────────────────

    [Fact]
    public async Task ExpenseCreate_ValidPdf_Succeeds()
    {
        using var form = BuildMultipart(
            "Items[0].Receipt", "receipt.pdf", "application/pdf", ValidPdfBytes);
        AddExpenseFields(form);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/expenses") { Content = form },
            "employee", 701, employeeId: "EMP-EXP-001");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ExpenseCreate_SpoofedMagicNumber_Returns400_AndDoesNotPersist()
    {
        using var form = BuildMultipart(
            "Items[0].Receipt", "receipt.pdf", "application/pdf", SpoofedMagicBytes);
        AddExpenseFields(form);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/expenses") { Content = form },
            "employee", 702, employeeId: "EMP-EXP-002");

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExpenseCreate_DangerousExtension_Returns400_AndDoesNotPersist()
    {
        using var form = BuildMultipart(
            "Items[0].Receipt", "receipt.exe", "application/octet-stream", ExeBytes);
        AddExpenseFields(form);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/expenses") { Content = form },
            "employee", 703, employeeId: "EMP-EXP-003");

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExpenseCreate_Svg_Returns400_AndDoesNotPersist()
    {
        using var form = BuildMultipart(
            "Items[0].Receipt", "receipt.svg", "image/svg+xml", SvgBytes);
        AddExpenseFields(form);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/expenses") { Content = form },
            "employee", 704, employeeId: "EMP-EXP-004");

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExpenseLegacy_ValidPdf_Succeeds()
    {
        using var form = BuildForm(
            [("Title", "Legacy claim"), ("Amount", "100"), ("Currency", "INR"), ("Category", "Food")],
            "Receipt", "receipt.pdf", "application/pdf", ValidPdfBytes);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/expenses/legacy") { Content = form },
            "employee", 705, employeeId: "EMP-EXP-005");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExpenseLegacy_SpoofedMagicNumber_Returns400_AndDoesNotPersist()
    {
        using var form = BuildForm(
            [("Title", "Legacy claim"), ("Amount", "100"), ("Currency", "INR")],
            "Receipt", "receipt.pdf", "application/pdf", SpoofedMagicBytes);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/expenses/legacy") { Content = form },
            "employee", 706, employeeId: "EMP-EXP-006");

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExpenseLegacy_DangerousExtension_Returns400_AndDoesNotPersist()
    {
        using var form = BuildForm(
            [("Title", "Legacy claim"), ("Amount", "100"), ("Currency", "INR")],
            "Receipt", "receipt.exe", "application/octet-stream", ExeBytes);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/expenses/legacy") { Content = form },
            "employee", 707, employeeId: "EMP-EXP-007");

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExpenseLegacy_Svg_Returns400_AndDoesNotPersist()
    {
        using var form = BuildForm(
            [("Title", "Legacy claim"), ("Amount", "100"), ("Currency", "INR")],
            "Receipt", "receipt.svg", "image/svg+xml", SvgBytes);
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/expenses/legacy") { Content = form },
            "employee", 708, employeeId: "EMP-EXP-008");

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── POST/PUT /api/employees ──────────────────────────────────────────────

    [Fact]
    public async Task EmployeeCreate_ValidDocument_Succeeds()
    {
        using var form = new MultipartFormDataContent();
        AddEmployeeFields(form);
        var content = new ByteArrayContent(ValidPdfBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(content, "identity_docs", "identity.pdf");
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/employees") { Content = form },
            "admin", 801);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeCreate_SpoofedMagicNumber_Returns400_AndDoesNotPersist()
    {
        using var form = new MultipartFormDataContent();
        AddEmployeeFields(form);
        var content = new ByteArrayContent(SpoofedMagicBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(content, "identity_docs", "identity.pdf");
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/employees") { Content = form },
            "admin", 802);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeCreate_DangerousExtension_Returns400_AndDoesNotPersist()
    {
        using var form = new MultipartFormDataContent();
        AddEmployeeFields(form);
        var content = new ByteArrayContent(ExeBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "identity_docs", "identity.exe");
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/employees") { Content = form },
            "admin", 803);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeCreate_Svg_Returns400_AndDoesNotPersist()
    {
        using var form = new MultipartFormDataContent();
        AddEmployeeFields(form);
        var content = new ByteArrayContent(SvgBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/svg+xml");
        form.Add(content, "identity_docs", "identity.svg");
        using var request = WithClaims(
            new HttpRequestMessage(HttpMethod.Post, "/api/employees") { Content = form },
            "admin", 804);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeUpdate_ValidDocument_Succeeds()
    {
        await SeedEmployee("EMP-UPDATE-001", 805);
        using var form = new MultipartFormDataContent();
        AddEmployeeFields(form, "Updated Upload Employee");
        var content = new ByteArrayContent(ValidPdfBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(content, "identity_docs", "identity.pdf");
        using var request = WithClaims(
            new HttpRequestMessage(
                HttpMethod.Put, "/api/employees/EMP-UPDATE-001") { Content = form },
            "admin", 805);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeUpdate_SpoofedMagicNumber_Returns400_AndDoesNotPersist()
    {
        await SeedEmployee("EMP-UPDATE-002", 806);
        using var form = new MultipartFormDataContent();
        AddEmployeeFields(form);
        var content = new ByteArrayContent(SpoofedMagicBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(content, "identity_docs", "identity.pdf");
        using var request = WithClaims(
            new HttpRequestMessage(
                HttpMethod.Put, "/api/employees/EMP-UPDATE-002") { Content = form },
            "admin", 806);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeUpdate_DangerousExtension_Returns400_AndDoesNotPersist()
    {
        await SeedEmployee("EMP-UPDATE-003", 807);
        using var form = new MultipartFormDataContent();
        AddEmployeeFields(form);
        var content = new ByteArrayContent(ExeBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "identity_docs", "identity.exe");
        using var request = WithClaims(
            new HttpRequestMessage(
                HttpMethod.Put, "/api/employees/EMP-UPDATE-003") { Content = form },
            "admin", 807);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeUpdate_Svg_Returns400_AndDoesNotPersist()
    {
        await SeedEmployee("EMP-UPDATE-004", 808);
        using var form = new MultipartFormDataContent();
        AddEmployeeFields(form);
        var content = new ByteArrayContent(SvgBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/svg+xml");
        form.Add(content, "identity_docs", "identity.svg");
        using var request = WithClaims(
            new HttpRequestMessage(
                HttpMethod.Put, "/api/employees/EMP-UPDATE-004") { Content = form },
            "admin", 808);

        var response = await SendAssertingNoNewFile(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
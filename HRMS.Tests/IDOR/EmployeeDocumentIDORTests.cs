using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.FileStorage;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace HRMS.Tests.IDOR;

/// <summary>
/// Service-level IDOR tests for EmployeeDocumentService.
/// </summary>
public class EmployeeDocumentIDORTests : IDisposable
{
    private const int CompanyA = 100;
    private const int CompanyB = 200;
    private const string EmpA = "emp-a-1";
    private const string EmpB = "emp-b-1";

    private readonly ApplicationDbContext _db;
    private readonly EmployeeDocumentService _svc;

    public EmployeeDocumentIDORTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);

        _db.Employees.AddRange(
            new Employee { Id = 1, EmployeeCode = EmpA, CompanyId = CompanyA, IsActive = true, FullName = "Alice" },
            new Employee { Id = 2, EmployeeCode = EmpB, CompanyId = CompanyB, IsActive = true, FullName = "Bob" });

        _db.EmployeeDocuments.AddRange(
            new EmployeeDocument
            {
                Id = 1, EmployeeId = EmpA, CompanyId = CompanyA, DocumentType = "ID",
                FileName = "a.pdf", FilePath = "/uploads/a.pdf", UploadedAt = DateTime.UtcNow
            },
            new EmployeeDocument
            {
                Id = 2, EmployeeId = EmpB, CompanyId = CompanyB, DocumentType = "ID",
                FileName = "b.pdf", FilePath = "/uploads/b.pdf", UploadedAt = DateTime.UtcNow
            });
        _db.SaveChanges();

        var scannerMock = new Mock<IClamAvVirusScanService>();
        var storageMock = new Mock<IFileStorageService>();
        _svc = new EmployeeDocumentService(_db, scannerMock.Object, storageMock.Object);
    }

    [Fact]
    public async Task GetDocumentsPaged_CompanyA_CannotListCompanyBEmployee()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _svc.GetDocumentsPagedAsync(EmpB, 1, 25, callerCompanyId: CompanyA));
    }

    [Fact]
    public async Task GetDocumentsPaged_CompanyA_CanListOwnEmployee()
    {
        var result = await _svc.GetDocumentsPagedAsync(EmpA, 1, 25, callerCompanyId: CompanyA);
        Assert.Single(result.Items);
        Assert.Equal(1, result.Items[0].Id);
    }

    [Fact]
    public async Task GetDocumentsPaged_SuperAdmin_CanListAnyEmployee()
    {
        var result = await _svc.GetDocumentsPagedAsync(EmpB, 1, 25, callerCompanyId: null);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task VerifyDocument_CompanyA_CannotVerifyCompanyBDocument()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _svc.VerifyDocumentAsync(2, 1, EmpB, callerCompanyId: CompanyA));
    }

    [Fact]
    public async Task VerifyDocument_CompanyA_CanVerifyOwnDocument()
    {
        var ok = await _svc.VerifyDocumentAsync(1, 1, EmpA, callerCompanyId: CompanyA);
        Assert.True(ok);
        var doc = await _db.EmployeeDocuments.FindAsync(1);
        Assert.True(doc!.IsVerified);
    }

    [Fact]
    public async Task DeleteDocument_CompanyA_CannotDeleteCompanyBDocument()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _svc.DeleteDocumentAsync(2, EmpB, callerCompanyId: CompanyA));
    }

    [Fact]
    public async Task DeleteDocument_CompanyA_CanDeleteOwnDocument()
    {
        var ok = await _svc.DeleteDocumentAsync(1, EmpA, callerCompanyId: CompanyA);
        Assert.True(ok);
        Assert.Null(await _db.EmployeeDocuments.FindAsync(1));
    }

    [Fact]
    public async Task UploadDocument_CompanyA_CannotUploadToCompanyBEmployee()
    {
        var dto = new UploadDocumentDto { EmployeeId = EmpB, DocumentType = "Passport" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _svc.UploadDocumentAsync(dto, file: null, callerCompanyId: CompanyA));
    }

    [Fact]
    public async Task DeleteDocument_SuperAdmin_CanDeleteAnyDocument()
    {
        var ok = await _svc.DeleteDocumentAsync(2, EmpB, callerCompanyId: null);
        Assert.True(ok);
    }

    public void Dispose() => _db.Dispose();
}
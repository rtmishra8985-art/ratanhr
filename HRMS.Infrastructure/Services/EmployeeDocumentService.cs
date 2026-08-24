using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Security;
using HRMS.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Employee document service.
///
/// PHASE 2 FIX (P2-DOC-IDOR): Service-level tenant isolation.
/// The controller guard (<c>EmployeeBelongsToCallerAsync</c>) is a first line of
/// defence but is not sufficient on its own because:
///   1. Future callers may bypass the controller.
///   2. Direct service injection in tests/background jobs skips controller checks.
///
/// Every method that accepts a <paramref name="callerCompanyId"/> parameter now
/// verifies the document → employee → company chain internally and throws
/// <see cref="UnauthorizedAccessException"/> when the chain breaks.
/// SuperAdmin callers pass <c>null</c> for <paramref name="callerCompanyId"/>,
/// which bypasses the tenant check.
/// </summary>
public class EmployeeDocumentService : IEmployeeDocumentService
{
    private readonly ApplicationDbContext     _db;
    private readonly IClamAvVirusScanService  _scanner;
    private readonly IFileStorageService      _storage;

    public EmployeeDocumentService(
        ApplicationDbContext    db,
        IClamAvVirusScanService scanner,
        IFileStorageService     storage)
    {
        _db      = db;
        _scanner = scanner;
        _storage = storage;
    }

    // ── Tenant guard ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the employee exists and belongs to <paramref name="callerCompanyId"/>.
    /// Throws <see cref="UnauthorizedAccessException"/> when the check fails.
    /// Does nothing when <paramref name="callerCompanyId"/> is <c>null</c> (SuperAdmin).
    /// </summary>
    private async Task EnforceEmployeeTenantAsync(string employeeId, int? callerCompanyId)
    {
        if (callerCompanyId is null) return;   // SuperAdmin — unrestricted

        var belongs = await _db.Employees
            .AnyAsync(e => e.EmployeeCode == employeeId && e.CompanyId == callerCompanyId.Value
                           && e.IsActive);

        if (!belongs)
            throw new UnauthorizedAccessException(
                $"Employee '{employeeId}' does not belong to company {callerCompanyId}.");
    }

    /// <summary>
    /// Verifies the document exists, belongs to <paramref name="employeeId"/>, and
    /// that the employee belongs to <paramref name="callerCompanyId"/>.
    /// Returns the document entity on success; throws on any violation.
    /// </summary>
    private async Task<EmployeeDocument> ResolveDocumentAsync(
        int docId, string employeeId, int? callerCompanyId)
    {
        var doc = await _db.EmployeeDocuments
            .FirstOrDefaultAsync(d => d.Id == docId && d.EmployeeId == employeeId);

        if (doc is null)
            throw new KeyNotFoundException($"Document {docId} not found for employee '{employeeId}'.");

        // Service-level tenant check — guards even when controller is bypassed.
        if (callerCompanyId is not null)
        {
            var belongs = await _db.Employees.AnyAsync(e =>
                e.EmployeeCode == employeeId &&
                e.CompanyId == callerCompanyId.Value &&
                e.IsActive);
            if (!belongs)
                throw new UnauthorizedAccessException(
                    $"Document {docId} does not belong to company {callerCompanyId}.");
        }

        return doc;
    }

    private async Task<List<EmployeeDocumentDto>> QueryDocumentsAsync(string employeeId)
    {
        return await _db.EmployeeDocuments
            .Where(d => d.EmployeeId == employeeId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new EmployeeDocumentDto
            {
                Id           = d.Id,
                EmployeeId   = d.EmployeeId,
                DocumentType = d.DocumentType,
                FileName     = d.FileName,
                FilePath     = d.FilePath,
                FileSizeBytes = d.FileSizeBytes,
                Notes        = d.Notes,
                IsVerified   = d.IsVerified,
                VerifiedAt   = d.VerifiedAt,
                UploadedAt   = d.UploadedAt,
            })
            .ToListAsync();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<List<EmployeeDocumentDto>> GetDocumentsAsync(
        string employeeId, int? callerCompanyId = null)
    {
        await EnforceEmployeeTenantAsync(employeeId, callerCompanyId);
        return await QueryDocumentsAsync(employeeId);
    }

    public async Task<PagedResult<EmployeeDocumentDto>> GetDocumentsPagedAsync(
        string employeeId, int page, int pageSize, int? callerCompanyId = null)
    {
        await EnforceEmployeeTenantAsync(employeeId, callerCompanyId);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.EmployeeDocuments
            .Where(d => d.EmployeeId == employeeId)
            .OrderByDescending(d => d.UploadedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new EmployeeDocumentDto
            {
                Id            = d.Id,
                EmployeeId    = d.EmployeeId,
                DocumentType  = d.DocumentType,
                FileName      = d.FileName,
                FilePath      = d.FilePath,
                FileSizeBytes = d.FileSizeBytes,
                Notes         = d.Notes,
                IsVerified    = d.IsVerified,
                VerifiedAt    = d.VerifiedAt,
                UploadedAt    = d.UploadedAt,
            })
            .ToListAsync();

        return new PagedResult<EmployeeDocumentDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<int> UploadDocumentAsync(
        UploadDocumentDto dto, IFormFile? file, int? callerCompanyId = null)
    {
        await EnforceEmployeeTenantAsync(dto.EmployeeId, callerCompanyId);

        if (file is null || file.Length == 0)
            throw new ArgumentException("No file was provided.");

        // ClamAV scan — fail closed; never skip in Production.
        using var stream = file.OpenReadStream();
        var scanResult = await _scanner.ScanAsync(stream, file.FileName);
        if (!scanResult.IsClean)
            throw new InvalidOperationException(
                $"File rejected by virus scanner: {scanResult.Threat}");

        // Item 9: the subfolder is an employee code, so the profile must be explicit
        // rather than inferred (inference would fall back to Document anyway).
        var storedPath = await _storage.SaveAsync(file, dto.EmployeeId, UploadProfile.Document)
            ?? throw new InvalidOperationException("The document could not be stored.");
        var companyId = await _db.Employees
            .Where(e => e.EmployeeCode == dto.EmployeeId)
            .Select(e => (int?)e.CompanyId)
            .FirstOrDefaultAsync();

        var doc = new EmployeeDocument
        {
            EmployeeId    = dto.EmployeeId,
            CompanyId     = companyId,
            DocumentType  = dto.DocumentType,
            FileName      = file.FileName,
            FilePath      = storedPath,
            FileSizeBytes = file.Length,
            Notes         = dto.Notes,
            IsVerified    = false,
            UploadedAt    = DateTime.UtcNow,
        };

        _db.EmployeeDocuments.Add(doc);
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    public async Task<bool> VerifyDocumentAsync(
        int docId, int verifiedByUserId, string employeeId, int? callerCompanyId = null)
    {
        var doc = await ResolveDocumentAsync(docId, employeeId, callerCompanyId);

        doc.IsVerified       = true;
        doc.VerifiedByUserId = verifiedByUserId;
        doc.VerifiedAt       = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteDocumentAsync(
        int docId, string employeeId, int? callerCompanyId = null)
    {
        var doc = await ResolveDocumentAsync(docId, employeeId, callerCompanyId);

        _db.EmployeeDocuments.Remove(doc);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(Stream Content, string FileName)> DownloadDocumentAsync(
        int docId, string employeeId, int? callerCompanyId = null)
    {
        var doc = await ResolveDocumentAsync(docId, employeeId, callerCompanyId);
        var stream = await _storage.RetrieveAsync(doc.FilePath);
        return (stream, doc.FileName);
    }
}

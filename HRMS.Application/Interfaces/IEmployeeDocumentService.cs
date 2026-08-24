using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using Microsoft.AspNetCore.Http;

namespace HRMS.Application.Interfaces;

public interface IEmployeeDocumentService
{
    Task<List<EmployeeDocumentDto>> GetDocumentsAsync(string employeeId, int? callerCompanyId = null);
    Task<PagedResult<EmployeeDocumentDto>> GetDocumentsPagedAsync(string employeeId, int page, int pageSize, int? callerCompanyId = null);
    Task<int> UploadDocumentAsync(UploadDocumentDto dto, IFormFile? file, int? callerCompanyId = null);
    /// <param name="employeeId">Route-supplied employee ID — the service verifies the document belongs to this employee before acting.</param>
    Task<bool> VerifyDocumentAsync(int docId, int verifiedByUserId, string employeeId, int? callerCompanyId = null);
    /// <param name="employeeId">Route-supplied employee ID — the service verifies the document belongs to this employee before acting.</param>
    Task<bool> DeleteDocumentAsync(int docId, string employeeId, int? callerCompanyId = null);
    Task<(Stream Content, string FileName)> DownloadDocumentAsync(int docId, string employeeId, int? callerCompanyId = null);
}

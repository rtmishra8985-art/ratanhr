using HRMS.Application.Common;
using HRMS.Application.DTOs.Training;

namespace HRMS.Application.Interfaces;

public interface ITrainingService
{
    Task<PagedResult<TrainingDto>> GetAllAsync(int? companyId, int page, int pageSize);
    Task<TrainingDto?> GetByIdAsync(int id, int? companyId);
    Task<TrainingDto> CreateAsync(int? companyId, CreateTrainingDto dto);
    Task<bool> UpdateAsync(int id, int? companyId, CreateTrainingDto dto);
    Task<bool> DeleteAsync(int id, int? companyId);

    /// <summary>
    /// Enroll an employee in a training program.
    /// </summary>
    /// <returns>
    /// (ok, message, isCrossTenant) — when isCrossTenant is true the controller must return 403 Forbidden.
    /// </returns>
    Task<(bool ok, string message, bool isCrossTenant)> EnrollAsync(int programId, string employeeId);

    Task<List<EnrollmentDto>> GetEnrollmentsByEmployeeAsync(string employeeId);
    Task<bool> MarkCompleteAsync(int enrollmentId, int? companyId, MarkCompleteDto dto);
}

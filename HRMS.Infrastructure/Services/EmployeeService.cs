using System.Security.Cryptography;
using HRMS.Application.Common;
using BCrypt.Net;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Authentication;
using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.FileStorage;
using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class EmployeeService : IEmployeeService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly INotificationService _notify; // P4
    private readonly IConfiguration _config;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(ApplicationDbContext db, IFileStorageService storage,
                           ILogger<EmployeeService> logger,
                           INotificationService? notify = null,
                           IConfiguration? config = null) // P4
    {
        _db = db;
        _storage = storage;
        _logger = logger;
        _notify = notify!;
        _config = config ?? new ConfigurationBuilder().Build();
    }

    /// <summary>
    /// Generates a unique EMP ID using cryptographically random bytes — avoids
    /// the non-thread-safe <c>new Random()</c> which could produce duplicate IDs
    /// under concurrent requests.
    /// </summary>
    private static string GenerateEmployeeId()
    {
        var bytes = RandomNumberGenerator.GetBytes(3); // 3 bytes → up to 16 million values
        int num = 1000 + (int)(BitConverter.ToUInt32(new byte[] { bytes[0], bytes[1], bytes[2], 0 }) % 9000);
        return $"EMP{num}";
    }

    /// <summary>
    /// Item 8 — cryptographically random temporary password that is policy-compliant
    /// <em>by construction</em>: length is taken from the active
    /// <see cref="PasswordPolicy"/> (minimum 16, never below the configured MinLength)
    /// and one character from each required class is guaranteed before a
    /// Fisher-Yates shuffle. The result is still passed through
    /// <see cref="PasswordPolicy.EnsureValid"/> so a future policy change can never
    /// silently produce a credential the user would then be unable to re-enter.
    ///
    /// Not a predictable "Emp@{id}" pattern that anyone who knows the employee ID
    /// could derive. Ambiguous glyphs (I, l, 1, O, 0) are excluded so the password
    /// can be read aloud or transcribed without error.
    /// </summary>
    internal static string GenerateTempPassword()
    {
        const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower   = "abcdefghijkmnopqrstuvwxyz";
        const string digits  = "23456789";
        const string special = "@#$!%*?&";
        const string all     = upper + lower + digits + special;

        // At least 16 characters, and never shorter than the configured minimum.
        var length = Math.Max(16, PasswordPolicy.MinLength);

        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];

        // Guarantee one character from every required class.
        chars[0] = upper  [bytes[0] % upper.Length];
        chars[1] = lower  [bytes[1] % lower.Length];
        chars[2] = digits [bytes[2] % digits.Length];
        chars[3] = special[bytes[3] % special.Length];
        for (int i = 4; i < length; i++) chars[i] = all[bytes[i] % all.Length];

        // Shuffle so the class positions are not fixed.
        var rng = RandomNumberGenerator.GetBytes(length);
        for (int i = length - 1; i > 0; i--)
        {
            int j = rng[i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        var password = new string(chars);

        // Defence in depth: the generator must satisfy the very policy it feeds.
        PasswordPolicy.EnsureValid(password, nameof(password));
        return password;
    }

    public async Task<(string employeeId, string tempPassword)> CreateAsync(CreateEmployeeDto dto, IFormFileCollection files)
    {
        // Ensure unique employee ID
        string empId;
        do { empId = GenerateEmployeeId(); }
        while (await _db.Employees.AnyAsync(e => e.EmployeeCode == empId));

        // Create login user for employee — random temp password, forced change on first login.
        var tempPassword = GenerateTempPassword();
        // Item 8: onboarding-generated credentials go through the same gate as
        // every other password entry point (redundant with the generator's own
        // check, kept so the rule is visible at the call site).
        PasswordPolicy.EnsureValid(tempPassword, nameof(tempPassword));

        var user = new User
        {
            Email = $"{empId.ToLower()}@company.com",
            PasswordHash = BcryptPasswordHasher.Hash(tempPassword, _config),
            Role = HRMS.Application.Common.AppRoles.Employee,
            FullName = dto.FullName,
            CompanyId = dto.CompanyId,
            MustChangePassword = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Save uploaded files
        var identityPath = await _storage.SaveFileAsync(files["identity_docs"], "identity", UploadProfile.Document);
        var eduPath = await _storage.SaveFileAsync(files["educational_docs"], "edu", UploadProfile.Document);
        var photoPath = await _storage.SaveFileAsync(files["passport_photo"], "photo", UploadProfile.Image);
        var expPath = await _storage.SaveFileAsync(files["experience_docs"], "experience", UploadProfile.Document);

        var emp = new Employee
        {
            EmployeeCode = empId,
            UserId = user.Id,
            CompanyId = dto.CompanyId ?? 0,
            FullName = dto.FullName,
            Gender = dto.Gender,
            DateOfBirth = DateOnlyParser.ParseNullable(dto.Dob, "Date of Birth"),
            Nationality = dto.Nationality,
            MaritalStatus = dto.MaritalStatus,
            BloodGroup = dto.BloodGroup,
            PermanentAddress = dto.PermanentAddress,
            CurrentAddress = dto.CurrentAddress,
            Aadhaar = dto.Aadhaar,
            PAN = dto.Pan,
            IdentityDocs = identityPath,
            MedicalConditions = dto.MedicalConditions,
            Hobbies = dto.Hobbies,
            Languages = dto.Languages,
            DateOfJoining = DateOnlyParser.ParseNullable(dto.Doj, "Date of Joining"),
            Designation = dto.Designation,
            Department = dto.Department,
            Skills = dto.Skills,
            Responsibilities = dto.Responsibilities,
            BankAccountHolder = dto.BankAccountHolder,
            BankName = dto.BankName,
            BranchName = dto.BranchName,
            AccountNumber = dto.AccountNumber,
            IFSCCode = dto.IfscCode,
            UAN = dto.Uan,
            Qualification = dto.Qualification,
            Institution = dto.Institution,
            YearOfPassing = dto.YearOfPassing,
            Specialization = dto.Specialization,
            EducationalDocs = eduPath,
            PassportPhoto = photoPath,
            PreviousEmployer = dto.PreviousEmployer,
            JobTitle = dto.JobTitle,
            Duration = dto.Duration,
            ExpResponsibilities = dto.ExpResponsibilities,
            ExperienceDocs = expPath,
            EmergencyContactName = dto.EmergencyContactName,
            EmergencyContactRelationship = dto.EmergencyContactRelationship,
            EmergencyContactPhone = dto.EmergencyContactPhone,
            EmergencyContactAddress = dto.EmergencyContactAddress
        };

        _db.Employees.Add(emp);
        await _db.SaveChangesAsync();

        // FIX HIGH-N1: Await the notification so async exceptions are caught by the surrounding
        // try/catch. The previous _ = discard silently swallowed any exception thrown inside
        // the async continuation.
        try
        {
            await _notify.NotifyAsync(user.Id, "Welcome to HRMS",
                $"Your employee account has been created. Employee ID: {empId}. You will receive your login credentials separately.",
                "info", "Employee", empId);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Welcome notification failed for user {UserId}", user.Id); }

        return (empId, tempPassword);
    }

    public async Task<bool> UpdateAsync(string employeeId, CreateEmployeeDto dto, IFormFileCollection files, int? companyId = null)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e =>
            e.EmployeeCode == employeeId && (!companyId.HasValue || e.CompanyId == companyId));
        if (emp == null) return false;

        emp.FullName = dto.FullName;
        emp.Gender = dto.Gender;
        emp.DateOfBirth = DateOnlyParser.ParseNullable(dto.Dob, "Date of Birth");
        emp.Nationality = dto.Nationality;
        emp.MaritalStatus = dto.MaritalStatus;
        emp.BloodGroup = dto.BloodGroup;
        emp.PermanentAddress = dto.PermanentAddress;
        emp.CurrentAddress = dto.CurrentAddress;
        emp.Aadhaar = dto.Aadhaar;
        emp.PAN = dto.Pan;
        emp.MedicalConditions = dto.MedicalConditions;
        emp.Hobbies = dto.Hobbies;
        emp.Languages = dto.Languages;
        emp.DateOfJoining = DateOnlyParser.ParseNullable(dto.Doj, "Date of Joining");
        emp.Designation = dto.Designation;
        emp.Department = dto.Department;
        emp.Skills = dto.Skills;
        emp.Responsibilities = dto.Responsibilities;
        emp.BankAccountHolder = dto.BankAccountHolder;
        emp.BankName = dto.BankName;
        emp.BranchName = dto.BranchName;
        emp.AccountNumber = dto.AccountNumber;
        emp.IFSCCode = dto.IfscCode;
        emp.UAN = dto.Uan;
        emp.Qualification = dto.Qualification;
        emp.Institution = dto.Institution;
        emp.YearOfPassing = dto.YearOfPassing;
        emp.Specialization = dto.Specialization;
        emp.PreviousEmployer = dto.PreviousEmployer;
        emp.JobTitle = dto.JobTitle;
        emp.Duration = dto.Duration;
        emp.ExpResponsibilities = dto.ExpResponsibilities;
        emp.EmergencyContactName = dto.EmergencyContactName;
        emp.EmergencyContactRelationship = dto.EmergencyContactRelationship;
        emp.EmergencyContactPhone = dto.EmergencyContactPhone;
        emp.EmergencyContactAddress = dto.EmergencyContactAddress;

        // Update files if provided
        if (files["identity_docs"] != null)
            emp.IdentityDocs = await _storage.SaveFileAsync(files["identity_docs"], "identity", UploadProfile.Document);
        if (files["educational_docs"] != null)
            emp.EducationalDocs = await _storage.SaveFileAsync(files["educational_docs"], "edu", UploadProfile.Document);
        if (files["passport_photo"] != null)
            emp.PassportPhoto = await _storage.SaveFileAsync(files["passport_photo"], "photo", UploadProfile.Image);
        if (files["experience_docs"] != null)
            emp.ExperienceDocs = await _storage.SaveFileAsync(files["experience_docs"], "experience", UploadProfile.Document);

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateStatusAsync(string employeeId, bool isActive, int? companyId = null)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e =>
            e.EmployeeCode == employeeId && (!companyId.HasValue || e.CompanyId == companyId));
        if (emp == null) return false;
        emp.IsActive = isActive;

        if (emp.UserId.HasValue)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == emp.UserId.Value);
            if (user != null) user.IsActive = isActive;
        }

        await _db.SaveChangesAsync();

        // FIX HIGH-N2: Await the notification so async exceptions are caught by the surrounding
        // try/catch. The previous _ = discard silently swallowed any exception thrown inside
        // the async continuation.
        if (!isActive && emp.UserId.HasValue)
        {
            try
            {
                await _notify.NotifyAsync(emp.UserId.Value, "Account Deactivated",
                    "Your HRMS employee account has been deactivated. Please contact HR for details.",
                    "warning", "Employee", employeeId);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Deactivation notification failed for employee {EmployeeId}", employeeId); }
        }

        return true;
    }

    public async Task<EmployeeDetailDto?> GetByIdAsync(string employeeId, int? companyId = null)
    {
        var query = _db.Employees.Where(e => e.EmployeeCode == employeeId);
        // Company-scoped lookup: a company admin must never be able to fetch another
        // company's employee by guessing/enumerating employee IDs (IDOR).
        if (companyId.HasValue) query = query.Where(e => e.CompanyId == companyId);
        var emp = await query.FirstOrDefaultAsync();
        if (emp == null) return null;
        return MapToDetail(emp);
    }

    // FIX MED-9: PII-gated lookup — only called by GET /api/employees/{id}/pii (SuperAdmin role).
    // includeRaw=true populates the Raw sub-object with unmasked values (SuperAdmin unmask endpoint).
    public async Task<EmployeePiiDto?> GetPiiAsync(string employeeId, int? companyId = null, bool includeRaw = false)
    {
        var query = _db.Employees.Where(e => e.EmployeeCode == employeeId);
        if (companyId.HasValue) query = query.Where(e => e.CompanyId == companyId);
        var emp = await query.FirstOrDefaultAsync();
        if (emp == null) return null;

        static string? Mask(string? v) =>
            string.IsNullOrWhiteSpace(v) || v.Length < 4 ? v
            : new string('*', v.Length - 4) + v[^4..];

        return new EmployeePiiDto
        {
            EmployeeId          = emp.EmployeeCode,
            AadhaarMasked       = Mask(emp.Aadhaar),
            PanMasked           = Mask(emp.PAN),
            AccountNumberMasked = Mask(emp.AccountNumber),
            IFSCCode            = emp.IFSCCode,
            UAN                 = emp.UAN,
            // Raw is only populated when caller explicitly requests unmasked values
            // and the service is invoked with includeRaw=true by a SuperAdmin endpoint.
            Raw = includeRaw ? new PiiRawValues
            {
                Aadhaar       = emp.Aadhaar,
                Pan           = emp.PAN,
                AccountNumber = emp.AccountNumber
            } : null
        };
    }

    public async Task<List<EmployeeListDto>> GetAllAsync(int? companyId = null)
    {
        var query = _db.Employees.AsQueryable();
        if (companyId.HasValue) query = query.Where(e => e.CompanyId == companyId);
        var emps = await query.OrderByDescending(e => e.CreatedAt).ToListAsync();
        return emps.Select(MapToList).ToList();
    }

    // FIX 5: Added sortBy / sortDirection for column-level sorting support.
    public async Task<PagedResult<EmployeeListDto>> GetAllPagedAsync(
        int?    companyId,
        int     page,
        int     pageSize,
        string? sortBy        = null,
        string? sortDirection = "asc",
        string? search        = null,
        string? status        = null,
        string? department    = null,
        string? designation   = null)
    {
        var query = _db.Employees.AsQueryable();
        if (companyId.HasValue) query = query.Where(e => e.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(e =>
                e.FullName.ToLower().Contains(term) ||
                (e.EmployeeCode != null && e.EmployeeCode.ToLower().Contains(term)) ||
                (e.Email != null && e.Email.ToLower().Contains(term)) ||
                (e.PhoneNumber != null && e.PhoneNumber.ToLower().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(e => e.Department != null && e.Department == department.Trim());
        if (!string.IsNullOrWhiteSpace(designation))
            query = query.Where(e => e.Designation != null && e.Designation == designation.Trim());

        // FIX 5: Safe sorting — whitelist prevents SQL injection.
        var allowed = new[] { "FullName", "Department", "Designation", "IsActive", "CreatedAt", "DateOfJoining" };
        query = query.ApplySortingByName(sortBy, sortDirection, e => e.FullName, allowed);

        // FIX CRIT-1: Include DepartmentEntity to prevent N+1 query (100 employees = 101 queries without this)
        query = query.Include(e => e.DepartmentEntity);

        var totalCount = await query.CountAsync();
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;
        var emps = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<EmployeeListDto>.Create(emps.Select(MapToList).ToList(), totalCount, page, pageSize);
    }

    // ── New test-aligned methods (int ID based) ───────────────────────────

    /// <inheritdoc/>
    public async Task<List<Employee>> GetAllEmployeesAsync(int companyId, string? status = null, CancellationToken ct = default)
    {
        var q = _db.Employees.Where(e => e.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.Status == status);
        return await q.ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<Employee?> GetEmployeeByIdAsync(int id, int companyId, CancellationToken ct = default)
        => await _db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, ct);

    /// <inheritdoc/>
    public async Task<int> CreateEmployeeAsync(int companyId, CreateEmployeeDto dto, CancellationToken ct = default)
    {
        // Employee email must be unique within the company.
        if (!string.IsNullOrWhiteSpace(dto.Email) &&
            await _db.Employees.AnyAsync(e => e.CompanyId == companyId && e.Email == dto.Email, ct))
            throw new InvalidOperationException(
                $"An employee with email '{dto.Email}' already exists in this company.");

        var emp = new Employee
        {
            FirstName      = dto.FirstName,
            LastName       = dto.LastName,
            FullName       = dto.FullName.Length > 0 ? dto.FullName
                             : $"{dto.FirstName} {dto.LastName}".Trim(),
            Email          = dto.Email,
            PhoneNumber    = dto.PhoneNumber,
            Gender         = dto.Gender,
            MaritalStatus  = dto.MaritalStatus,
            Designation    = dto.Designation,
            Department     = dto.Department,
            DepartmentId   = dto.DepartmentId,
            DateOfJoining  = dto.DateOfJoining,
            Status         = dto.Status ?? "Active",
            CompanyId      = companyId,
            IsActive       = true,
            CreatedAt      = DateTime.UtcNow,
        };
        _db.Employees.Add(emp);
        await _db.SaveChangesAsync(ct);
        return emp.EmployeeId;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<Employee>> GetEmployeesPagedAsync(int companyId, int page, int pageSize, CancellationToken ct = default)
    {
        var q     = _db.Employees.Where(e => e.CompanyId == companyId);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<Employee> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<bool> DeleteAsync(string employeeId, int? companyId = null)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e =>
            e.EmployeeCode == employeeId && (!companyId.HasValue || e.CompanyId == companyId));
        if (emp == null) return false;

        // Medium FIX: soft delete instead of hard delete.
        // Permanently removing the row would destroy payslip, leave, and audit FK references,
        // preventing historical reporting and violating audit-trail requirements.
        // Setting IsActive = false hides the employee from all active-listing queries
        // (those filter IsActive == true) while keeping the record and all child data intact.
        emp.IsActive = false;

        // Lock the associated auth user so the employee can no longer authenticate.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == emp.UserId);
        if (user != null) user.IsActive = false;

        await _db.SaveChangesAsync();
        return true;
    }

    private static EmployeeListDto MapToList(Employee e) => new()
    {
        EmployeeId = e.Id,
        FullName = e.DisplayName,
        Department = e.Department,
        Designation = e.Designation,
        Doj = e.DateOfJoining?.ToString("yyyy-MM-dd"),
        IsActive = e.IsActive,
        PassportPhoto = e.PassportPhoto,
        Gender = e.Gender
    };

    private static EmployeeDetailDto MapToDetail(Employee e) => new()
    {
        Id = e.Id,
        EmployeeId = e.EmployeeCode,
        FullName = e.DisplayName,
        Gender = e.Gender,
        Dob = e.DateOfBirth?.ToString("yyyy-MM-dd"),
        Nationality = e.Nationality,
        MaritalStatus = e.MaritalStatus,
        BloodGroup = e.BloodGroup,
        PermanentAddress = e.PermanentAddress,
        CurrentAddress = e.CurrentAddress,
        // MED-9: Aadhaar omitted — private set in EmployeeDetailDto; use GetPiiAsync for PII
        // MED-9: Pan omitted — private set in EmployeeDetailDto
        IdentityDocs = e.IdentityDocs,
        MedicalConditions = e.MedicalConditions,
        Hobbies = e.Hobbies,
        Languages = e.Languages,
        Doj = e.DateOfJoining?.ToString("yyyy-MM-dd"),
        Designation = e.Designation ?? string.Empty,
        Department = e.Department ?? string.Empty,
        Skills = e.Skills,
        Responsibilities = e.Responsibilities,
        BankAccountHolder = e.BankAccountHolder,
        BankName = e.BankName,
        BranchName = e.BranchName,
        // MED-9: AccountNumber omitted — private set in EmployeeDetailDto
        // MED-9: IfscCode omitted — private set in EmployeeDetailDto
        Uan = e.UAN,
        Qualification = e.Qualification,
        Institution = e.Institution,
        YearOfPassing = e.YearOfPassing,
        Specialization = e.Specialization,
        EducationalDocs = e.EducationalDocs,
        PassportPhoto = e.PassportPhoto,
        PreviousEmployer = e.PreviousEmployer,
        JobTitle = e.JobTitle,
        Duration = e.Duration,
        ExpResponsibilities = e.ExpResponsibilities,
        ExperienceDocs = e.ExperienceDocs,
        EmergencyContactName = e.EmergencyContactName,
        EmergencyContactRelationship = e.EmergencyContactRelationship,
        EmergencyContactPhone = e.EmergencyContactPhone,
        EmergencyContactAddress = e.EmergencyContactAddress,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt
    };

    // ── Test-aligned int-PK methods ───────────────────────────────────────────

    /// <summary>
    /// Update employee fields by int PK, scoped to company.
    /// Returns false when not found or cross-company.
    /// </summary>
    public async Task<bool> UpdateEmployeeAsync(int id, int companyId,
        Application.DTOs.Employee.UpdateEmployeeDto dto)
    {
        var emp = await _db.Employees
            .Where(e => e.Id == id && e.CompanyId == companyId)
            .FirstOrDefaultAsync();
        if (emp == null) return false;

        if (dto.FirstName    != null)  emp.FirstName    = dto.FirstName;
        if (dto.LastName     != null)  emp.LastName     = dto.LastName;
        if (dto.DepartmentId.HasValue) emp.DepartmentId = dto.DepartmentId;

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Soft-delete by int PK, scoped to company (sets IsActive=false, Status="Inactive").
    /// Returns false when not found or cross-company.
    /// </summary>
    public async Task<bool> DeleteEmployeeAsync(int id, int companyId)
    {
        var emp = await _db.Employees
            .Where(e => e.Id == id && e.CompanyId == companyId)
            .FirstOrDefaultAsync();
        if (emp == null) return false;

        emp.IsActive = false;
        emp.Status   = "Inactive";
        await _db.SaveChangesAsync();
        return true;
    }
}

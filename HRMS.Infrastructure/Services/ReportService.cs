using ClosedXML.Excel;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ReportService> _logger;
    // FIX 7: Hard cap on report row count — prevents unbounded memory allocation
    // when a wide date range is requested (e.g. full year for a large company).
    // Callers should paginate or use the streaming report endpoint for large exports.
    private const int ReportRowCap = 10_000;

    public ReportService(ApplicationDbContext db, ILogger<ReportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Attendance report (existing, untouched) ────────────────────────────
    public async Task<List<AttendanceReportItemDto>> GetAttendanceReportAsync(AttendanceReportFilterDto filter)
    {
        var from = DateOnlyParser.ParseRequired(filter.From!, "From");
        var to   = DateOnlyParser.ParseRequired(filter.To!, "To");

        var webQ = _db.WebAttendances.Where(a => a.AttDate >= from && a.AttDate <= to);
        var excQ = _db.ExcelAttendances.Where(a => a.AttDate >= from && a.AttDate <= to);
        if (!string.IsNullOrEmpty(filter.EmployeeId)) {
            webQ = webQ.Where(a => a.EmployeeId == filter.EmployeeId);
            excQ = excQ.Where(a => a.EmployeeId == filter.EmployeeId);
        }
        if (filter.CompanyId.HasValue)
        {
            // FIX B part 2: CompanyId filter was previously applied only to ExcelAttendance.
            // WebAttendance must also be company-scoped; otherwise any caller could retrieve
            // web attendance for employees across all companies.
            var empIds = await _db.Employees
                .Where(e => e.CompanyId == filter.CompanyId)
                .Select(e => e.EmployeeCode)
                .ToListAsync();
            webQ = webQ.Where(a => empIds.Contains(a.EmployeeId));
            excQ = excQ.Where(a => a.CompanyId == filter.CompanyId);
        }

        // FIX 7: Cap rows to prevent OOM on large date ranges.
        var webCount = await webQ.CountAsync();
        var excCount = await excQ.CountAsync();
        if (webCount + excCount > ReportRowCap)
            _logger.LogWarning(
                "[ReportService] GetAttendanceReportAsync: query would return {Total} rows " +
                "(cap={Cap}). Truncating to first {Cap} combined rows. Use the streaming " +
                "export endpoint for full data sets.", webCount + excCount, ReportRowCap, ReportRowCap);

        var webRows = await webQ.Take(ReportRowCap).ToListAsync();
        var excRows = await excQ.Take(Math.Max(0, ReportRowCap - webRows.Count)).ToListAsync();

        var allEmpIds = webRows.Select(r => r.EmployeeId).Concat(excRows.Select(r => r.EmployeeId)).Distinct().ToList();
        var empDict = await _db.Employees.Where(e => allEmpIds.Contains(e.EmployeeCode))
                                         .ToDictionaryAsync(e => e.EmployeeCode, e => e.FullName);

        var result = new List<AttendanceReportItemDto>();
        foreach (var row in webRows)
            result.Add(new AttendanceReportItemDto {
                EmployeeId = row.EmployeeId,
                EmployeeName = empDict.GetValueOrDefault(row.EmployeeId),
                Date = row.AttDate.ToString("yyyy-MM-dd"),
                Status = row.Status, Source = "Web",
                CheckIn  = row.CheckIn?.ToString("HH:mm"),
                CheckOut = row.CheckOut?.ToString("HH:mm")
            });
        foreach (var row in excRows)
            result.Add(new AttendanceReportItemDto {
                EmployeeId = row.EmployeeId,
                EmployeeName = empDict.GetValueOrDefault(row.EmployeeId),
                Date = row.AttDate.ToString("yyyy-MM-dd"),
                Status = row.Status, Source = "Excel",
                HoursWorked = row.HoursWorked
            });

        return result.OrderBy(r => r.Date).ThenBy(r => r.EmployeeId).ToList();
    }

    // ── Monthly attendance report ──────────────────────────────────────────
    public async Task<List<MonthlyAttendanceReportDto>> GetMonthlyAttendanceReportAsync(
        int? companyId, int month, int year)
    {
        var from = new DateOnly(year, month, 1);
        var to   = from.AddMonths(1).AddDays(-1);

        var empQ = _db.Employees.Where(e => e.IsActive);
        if (companyId.HasValue) empQ = empQ.Where(e => e.CompanyId == companyId);

        // FIX 7: Cap rows to prevent OOM on very large companies.
        var empCount = await empQ.CountAsync();
        if (empCount > ReportRowCap)
            _logger.LogWarning(
                "[ReportService] GetMonthlyAttendanceReportAsync: employee count {Count} " +
                "exceeds cap {Cap}. Truncating result set. Use the streaming export " +
                "endpoint for full data.", empCount, ReportRowCap);
        var employees = await empQ.Take(ReportRowCap).ToListAsync();

        var empIds = employees.Select(e => e.EmployeeCode).ToList();
        var webAtt  = await _db.WebAttendances
            .Where(a => a.AttDate >= from && a.AttDate <= to && empIds.Contains(a.EmployeeId)).ToListAsync();
        var excAtt  = await _db.ExcelAttendances
            .Where(a => a.AttDate >= from && a.AttDate <= to && empIds.Contains(a.EmployeeId)).ToListAsync();

        int workingDays = to.Day; // simplistic; adjust for weekends/holidays if needed

        // FIX H-03: Replace O(N×M) per-employee linear scan with a single O(N+M) GroupBy pass.
        // Previously, each employee iterated the entire webAtt/excAtt lists to count "Present"
        // records, producing quadratic cost when both lists are large.
        // Now we build two dictionaries in one pass (O(N) and O(M)), then each employee lookup
        // is O(1) via dictionary key access.
        var webPresentByEmp = webAtt
            .Where(a => a.Status == "Present")
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Count());

        var excPresentByEmp = excAtt
            .Where(a => a.Status == "Present")
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<MonthlyAttendanceReportDto>();
        foreach (var emp in employees)
        {
            var webPresent = webPresentByEmp.GetValueOrDefault(emp.EmployeeCode, 0);
            var excPresent = excPresentByEmp.GetValueOrDefault(emp.EmployeeCode, 0);
            var present    = webPresent + excPresent;
            result.Add(new MonthlyAttendanceReportDto {
                EmployeeId   = emp.EmployeeCode,
                EmployeeName = emp.FullName,
                Department   = emp.Department,
                WorkingDays  = workingDays,
                DaysPresent  = present,
                DaysAbsent   = Math.Max(0, workingDays - present),
                AttendancePct = workingDays > 0 ? Math.Round((decimal)present / workingDays * 100, 1) : 0
            });
        }
        return result.OrderBy(r => r.EmployeeName).ToList();
    }

    // ── Daily attendance report ────────────────────────────────────────────
    public async Task<List<DailyAttendanceReportDto>> GetDailyAttendanceReportAsync(
        int? companyId, DateOnly from, DateOnly to)
    {
        var empQ = _db.Employees.Where(e => e.IsActive);
        if (companyId.HasValue) empQ = empQ.Where(e => e.CompanyId == companyId);
        var employees = await empQ.ToDictionaryAsync(e => e.EmployeeCode, e => e.FullName);

        // BUG FIX (cross-tenant data leak): these two queries previously had no company
        // filter at all, unlike GetMonthlyAttendanceReportAsync above which correctly
        // restricts to empIds.Contains(a.EmployeeId). Every attendance row in the date
        // range from EVERY company was added to the result below; only the EmployeeName
        // lookup was company-scoped (silently null for other companies' employees), while
        // EmployeeId, Date, Status, Source, CheckIn/CheckOut for those rows still leaked
        // into the response. A company admin requesting their own daily attendance report
        // received every other tenant's attendance rows mixed in.
        var empIds = employees.Keys.ToList();
        var webAtt = await _db.WebAttendances
            .Where(a => a.AttDate >= from && a.AttDate <= to && empIds.Contains(a.EmployeeId)).ToListAsync();
        var excAtt = await _db.ExcelAttendances
            .Where(a => a.AttDate >= from && a.AttDate <= to && empIds.Contains(a.EmployeeId)).ToListAsync();

        var result = new List<DailyAttendanceReportDto>();
        foreach (var a in webAtt)
            result.Add(new DailyAttendanceReportDto {
                EmployeeId = a.EmployeeId,
                EmployeeName = employees.GetValueOrDefault(a.EmployeeId),
                Date = a.AttDate.ToString("yyyy-MM-dd"), Status = a.Status, Source = "Web",
                CheckIn  = a.CheckIn?.ToString("HH:mm"), CheckOut = a.CheckOut?.ToString("HH:mm")
            });
        foreach (var a in excAtt)
            result.Add(new DailyAttendanceReportDto {
                EmployeeId = a.EmployeeId,
                EmployeeName = employees.GetValueOrDefault(a.EmployeeId),
                Date = a.AttDate.ToString("yyyy-MM-dd"), Status = a.Status, Source = "Excel",
                HoursWorked = a.HoursWorked
            });
        return result.OrderBy(r => r.Date).ThenBy(r => r.EmployeeId).ToList();
    }

    // ── Export monthly attendance Excel ────────────────────────────────────
    public async Task<byte[]> ExportAttendanceReportAsync(int? companyId, int month, int year)
    {
        var data = await GetMonthlyAttendanceReportAsync(companyId, month, year);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Attendance");
        var months = new[]{"","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"};
        ws.Cell(1,1).Value = $"Monthly Attendance Report – {months[month]} {year}";
        ws.Range(1,1,1,7).Merge().Style.Font.Bold = true;

        var headers = new[]{ "Employee ID","Name","Department","Working Days","Days Present","Days Absent","Attendance %" };
        for (int c = 0; c < headers.Length; c++) {
            ws.Cell(2,c+1).Value = headers[c];
            ws.Cell(2,c+1).Style.Font.Bold = true;
            ws.Cell(2,c+1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }
        int row = 3;
        foreach (var d in data) {
            ws.Cell(row,1).Value = d.EmployeeId;
            ws.Cell(row,2).Value = d.EmployeeName;
            ws.Cell(row,3).Value = d.Department;
            ws.Cell(row,4).Value = d.WorkingDays;
            ws.Cell(row,5).Value = d.DaysPresent;
            ws.Cell(row,6).Value = d.DaysAbsent;
            ws.Cell(row,7).Value = (double)d.AttendancePct;
            row++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Employee summary report ────────────────────────────────────────────
    public async Task<EmployeeSummaryReportDto> GetEmployeeSummaryReportAsync(int? companyId)
    {
        var q = _db.Employees.AsQueryable();
        if (companyId.HasValue) q = q.Where(e => e.CompanyId == companyId);
        var employees = await q.ToListAsync();
        return new EmployeeSummaryReportDto {
            TotalEmployees  = employees.Count,
            ActiveEmployees = employees.Count(e => e.IsActive),
            InactiveEmployees = employees.Count(e => !e.IsActive),
            ByDepartment    = employees.GroupBy(e => e.Department ?? "Unknown")
                .Select(g => new GroupCount { Name = g.Key, Count = g.Count() }).ToList(),
            ByDesignation   = employees.GroupBy(e => e.Designation ?? "Unknown")
                .Select(g => new GroupCount { Name = g.Key, Count = g.Count() }).ToList(),
            ByGender        = employees.GroupBy(e => e.Gender ?? "Not Specified")
                .Select(g => new GroupCount { Name = g.Key, Count = g.Count() }).ToList(),
            Details = employees.Select(e => new EmployeeSummaryItemDto {
                EmployeeId   = e.EmployeeCode, FullName = e.FullName,
                Department   = e.Department, Designation = e.Designation,
                DateOfJoining = e.DateOfJoining?.ToString("yyyy-MM-dd"),
                IsActive     = e.IsActive
            }).OrderBy(e => e.FullName).ToList()
        };
    }

    public async Task<byte[]> ExportEmployeeReportAsync(int? companyId)
    {
        var data = await GetEmployeeSummaryReportAsync(companyId);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Employees");
        var headers = new[]{ "Employee ID","Full Name","Department","Designation","Joining Date","Status" };
        for (int c = 0; c < headers.Length; c++) {
            ws.Cell(1,c+1).Value = headers[c];
            ws.Cell(1,c+1).Style.Font.Bold = true;
            ws.Cell(1,c+1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }
        int row = 2;
        foreach (var d in data.Details) {
            ws.Cell(row,1).Value = d.EmployeeId;
            ws.Cell(row,2).Value = d.FullName;
            ws.Cell(row,3).Value = d.Department;
            ws.Cell(row,4).Value = d.Designation;
            ws.Cell(row,5).Value = d.DateOfJoining;
            ws.Cell(row,6).Value = d.IsActive ? "Active" : "Inactive";
            row++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Payroll report ─────────────────────────────────────────────────────
    public async Task<PayrollReportDto> GetPayrollReportAsync(int? companyId, int month, int year)
    {
        var empQ = _db.Employees.Where(e => e.IsActive);
        if (companyId.HasValue) empQ = empQ.Where(e => e.CompanyId == companyId);
        var empIds = await empQ.Select(e => e.EmployeeCode).ToListAsync();

        var payslips = await _db.Payslips
            .Where(p => empIds.Contains(p.EmployeeId) && p.Month == month && p.Year == year)
            .ToListAsync();

        var empDict = await _db.Employees.Where(e => empIds.Contains(e.EmployeeCode))
                                         .ToDictionaryAsync(e => e.EmployeeCode, e => new { e.FullName, e.Department, e.Designation });
        var items = payslips.Select(p => {
            var emp = empDict.GetValueOrDefault(p.EmployeeId);
            return new PayrollReportItemDto {
                EmployeeId = p.EmployeeId, EmployeeName = emp?.FullName,
                Department = emp?.Department, Designation = emp?.Designation,
                GrossEarnings = p.GrossEarnings, TotalDeductions = p.TotalDeductions, NetPay = p.NetPay,
                PFEmployee = p.PFEmployee, PFEmployer = p.PFEmployer, ESI = p.ESI, PT = p.PT, TDS = p.TDS
            };
        }).ToList();

        return new PayrollReportDto {
            Month = month, Year = year, EmployeeCount = items.Count,
            TotalGross = items.Sum(i => i.GrossEarnings),
            TotalDeductions = items.Sum(i => i.TotalDeductions),
            TotalNetPay = items.Sum(i => i.NetPay),
            TotalPFEmployee = items.Sum(i => i.PFEmployee),
            TotalPFEmployer = items.Sum(i => i.PFEmployer),
            TotalESI = items.Sum(i => i.ESI),
            TotalPT  = items.Sum(i => i.PT),
            TotalTDS = items.Sum(i => i.TDS),
            Items = items
        };
    }

    public async Task<byte[]> ExportPayrollReportAsync(int? companyId, int month, int year)
    {
        var data = await GetPayrollReportAsync(companyId, month, year);
        var months = new[]{"","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"};
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Payroll");
        ws.Cell(1,1).Value = $"Payroll Report – {months[month]} {year}";
        ws.Range(1,1,1,11).Merge().Style.Font.Bold = true;
        var hdr = new[]{ "Employee ID","Name","Department","Designation","Gross","PF(Emp)","PF(Empl)","ESI","PT","TDS","Net Pay" };
        for (int c = 0; c < hdr.Length; c++) {
            ws.Cell(2,c+1).Value = hdr[c];
            ws.Cell(2,c+1).Style.Font.Bold = true;
            ws.Cell(2,c+1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }
        int row = 3;
        foreach (var d in data.Items) {
            ws.Cell(row,1).Value  = d.EmployeeId;
            ws.Cell(row,2).Value  = d.EmployeeName;
            ws.Cell(row,3).Value  = d.Department;
            ws.Cell(row,4).Value  = d.Designation;
            ws.Cell(row,5).Value  = (double)d.GrossEarnings;
            ws.Cell(row,6).Value  = (double)d.PFEmployee;
            ws.Cell(row,7).Value  = (double)d.PFEmployer;
            ws.Cell(row,8).Value  = (double)d.ESI;
            ws.Cell(row,9).Value  = (double)d.PT;
            ws.Cell(row,10).Value = (double)d.TDS;
            ws.Cell(row,11).Value = (double)d.NetPay;
            row++;
        }
        // Totals row
        ws.Cell(row,1).Value = "TOTAL";
        ws.Cell(row,5).Value = (double)data.TotalGross;
        ws.Cell(row,6).Value = (double)data.TotalPFEmployee;
        ws.Cell(row,7).Value = (double)data.TotalPFEmployer;
        ws.Cell(row,8).Value = (double)data.TotalESI;
        ws.Cell(row,9).Value = (double)data.TotalPT;
        ws.Cell(row,10).Value = (double)data.TotalTDS;
        ws.Cell(row,11).Value = (double)data.TotalNetPay;
        ws.Row(row).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Salary Register ────────────────────────────────────────────────────
    public async Task<SalaryRegisterDto> GetSalaryRegisterAsync(int? companyId, int month, int year)
    {
        var empQ = _db.Employees.Where(e => e.IsActive);
        if (companyId.HasValue) empQ = empQ.Where(e => e.CompanyId == companyId);
        var employees = await empQ.ToListAsync();
        var empIds = employees.Select(e => e.EmployeeCode).ToList();

        var payslips = await _db.Payslips
            .Where(p => empIds.Contains(p.EmployeeId) && p.Month == month && p.Year == year)
            .ToListAsync();

        var empDict = employees.ToDictionary(e => e.EmployeeCode);

        var rows = payslips.Select(p => {
            var emp = empDict.GetValueOrDefault(p.EmployeeId);
            return new SalaryRegisterItemDto {
                EmployeeId       = p.EmployeeId,
                EmployeeName     = emp?.FullName,
                Department       = emp?.Department,
                Designation      = emp?.Designation,
                BankName         = emp?.BankName,
                AccountNumber    = emp?.AccountNumber,
                UAN              = emp?.UAN,
                DaysPresent      = p.DaysPresent,
                WorkingDays      = p.WorkingDays,
                BasicPay         = p.BasicPay,
                HRA              = p.HRA,
                DA               = p.DA,
                Conveyance       = p.Conveyance,
                MedicalAllowance = p.MedicalAllowance,
                OtherAllowances  = p.OtherAllowances,
                GrossEarnings    = p.GrossEarnings,
                PFEmployee       = p.PFEmployee,
                PFEmployer       = p.PFEmployer,
                ESI              = p.ESI,
                PT               = p.PT,
                TDS              = p.TDS,
                OtherDeductions  = p.OtherDeductions,
                TotalDeductions  = p.TotalDeductions,
                NetPay           = p.NetPay
            };
        }).OrderBy(r => r.EmployeeName).ToList();

        return new SalaryRegisterDto {
            Month = month, Year = year, EmployeeCount = rows.Count,
            TotalCTC         = rows.Sum(r => r.BasicPay * 12 + r.PFEmployer * 12), // approximate CTC
            TotalGross       = rows.Sum(r => r.GrossEarnings),
            TotalPFEmployee  = rows.Sum(r => r.PFEmployee),
            TotalPFEmployer  = rows.Sum(r => r.PFEmployer),
            TotalESI         = rows.Sum(r => r.ESI),
            TotalPT          = rows.Sum(r => r.PT),
            TotalTDS         = rows.Sum(r => r.TDS),
            TotalDeductions  = rows.Sum(r => r.TotalDeductions),
            TotalNetPay      = rows.Sum(r => r.NetPay),
            Rows             = rows
        };
    }

    public async Task<byte[]> ExportSalaryRegisterAsync(int? companyId, int month, int year)
    {
        var data = await GetSalaryRegisterAsync(companyId, month, year);
        var months = new[]{"","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"};
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("SalaryRegister");
        ws.Cell(1,1).Value = $"Salary Register – {months[month]} {year}";
        ws.Range(1,1,1,20).Merge().Style.Font.Bold = true;

        var hdr = new[]{ "Emp ID","Name","Dept","Designation","Bank","Account No","UAN",
            "Days Present","Working Days","Basic","HRA","DA","Conv","Medical","Other Allow.",
            "Gross","PF(Emp)","PF(Empl)","ESI","PT","TDS","Other Ded.","Total Ded.","Net Pay" };
        for (int c = 0; c < hdr.Length; c++) {
            ws.Cell(2,c+1).Value = hdr[c];
            ws.Cell(2,c+1).Style.Font.Bold = true;
            ws.Cell(2,c+1).Style.Fill.BackgroundColor = XLColor.LightGreen;
        }
        int row = 3;
        foreach (var r in data.Rows) {
            ws.Cell(row,1).Value  = r.EmployeeId;
            ws.Cell(row,2).Value  = r.EmployeeName;
            ws.Cell(row,3).Value  = r.Department;
            ws.Cell(row,4).Value  = r.Designation;
            ws.Cell(row,5).Value  = r.BankName;
            ws.Cell(row,6).Value  = r.AccountNumber;
            ws.Cell(row,7).Value  = r.UAN;
            ws.Cell(row,8).Value  = r.DaysPresent;
            ws.Cell(row,9).Value  = r.WorkingDays;
            ws.Cell(row,10).Value = (double)r.BasicPay;
            ws.Cell(row,11).Value = (double)r.HRA;
            ws.Cell(row,12).Value = (double)r.DA;
            ws.Cell(row,13).Value = (double)r.Conveyance;
            ws.Cell(row,14).Value = (double)r.MedicalAllowance;
            ws.Cell(row,15).Value = (double)r.OtherAllowances;
            ws.Cell(row,16).Value = (double)r.GrossEarnings;
            ws.Cell(row,17).Value = (double)r.PFEmployee;
            ws.Cell(row,18).Value = (double)r.PFEmployer;
            ws.Cell(row,19).Value = (double)r.ESI;
            ws.Cell(row,20).Value = (double)r.PT;
            ws.Cell(row,21).Value = (double)r.TDS;
            ws.Cell(row,22).Value = (double)r.OtherDeductions;
            ws.Cell(row,23).Value = (double)r.TotalDeductions;
            ws.Cell(row,24).Value = (double)r.NetPay;
            row++;
        }
        // Totals
        ws.Cell(row,1).Value  = "TOTAL";
        ws.Cell(row,16).Value = (double)data.TotalGross;
        ws.Cell(row,17).Value = (double)data.TotalPFEmployee;
        ws.Cell(row,18).Value = (double)data.TotalPFEmployer;
        ws.Cell(row,19).Value = (double)data.TotalESI;
        ws.Cell(row,20).Value = (double)data.TotalPT;
        ws.Cell(row,21).Value = (double)data.TotalTDS;
        ws.Cell(row,23).Value = (double)data.TotalDeductions;
        ws.Cell(row,24).Value = (double)data.TotalNetPay;
        ws.Row(row).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Leave Report ───────────────────────────────────────────────────────
    public async Task<LeaveReportDto> GetLeaveReportAsync(int? companyId, int month, int year)
    {
        var q = _db.LeaveRequests.AsQueryable();
        if (companyId.HasValue) q = q.Where(r => r.CompanyId == companyId);
        if (month > 0)
            q = q.Where(r => r.StartDate.Month == month && r.StartDate.Year == year);
        else
            q = q.Where(r => r.StartDate.Year == year);

        var requests = await q.ToListAsync();

        var empIds = requests.Select(r => r.EmployeeId).Distinct().ToList();
        var empDict = await _db.Employees.Where(e => empIds.Contains(e.EmployeeCode))
                                         .ToDictionaryAsync(e => e.EmployeeCode, e => e.FullName);
        var typeIds  = requests.Select(r => r.LeaveTypeId).Distinct().ToList();
        var typeDict = await _db.LeaveTypes.Where(t => typeIds.Contains(t.Id))
                                           .ToDictionaryAsync(t => t.Id, t => t.Name);

        var details = requests.Select(r => new LeaveReportItemDto {
            EmployeeId    = r.EmployeeId,
            EmployeeName  = empDict.GetValueOrDefault(r.EmployeeId),
            LeaveTypeName = typeDict.GetValueOrDefault(r.LeaveTypeId, $"Type#{r.LeaveTypeId}"),
            StartDate     = r.StartDate.ToString("yyyy-MM-dd"),
            EndDate       = r.EndDate.ToString("yyyy-MM-dd"),
            TotalDays     = r.TotalDays,
            Status        = r.Status,
            Reason        = r.Reason
        }).ToList();

        return new LeaveReportDto {
            Month = month, Year = year, CompanyId = companyId,
            TotalRequests     = details.Count,
            Approved          = details.Count(d => d.Status == "Approved"),
            Rejected          = details.Count(d => d.Status == "Rejected"),
            Pending           = details.Count(d => d.Status == "Pending"),
            TotalDaysApproved = requests.Where(r => r.Status == "Approved").Sum(r => r.TotalDays),
            Details           = details
        };
    }

    public async Task<byte[]> ExportLeaveReportAsync(int? companyId, int month, int year)
    {
        var data = await GetLeaveReportAsync(companyId, month, year);
        var months = new[]{"","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"};
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("LeaveReport");
        var period = month > 0 ? $"{months[month]} {year}" : $"Year {year}";
        ws.Cell(1,1).Value = $"Leave Utilisation Report – {period}";
        ws.Range(1,1,1,8).Merge().Style.Font.Bold = true;

        var hdr = new[]{ "Employee ID","Employee Name","Leave Type","Start Date","End Date","Days","Status","Reason" };
        for (int c = 0; c < hdr.Length; c++) {
            ws.Cell(2,c+1).Value = hdr[c];
            ws.Cell(2,c+1).Style.Font.Bold = true;
            ws.Cell(2,c+1).Style.Fill.BackgroundColor = XLColor.LightYellow;
        }
        int row = 3;
        foreach (var d in data.Details) {
            ws.Cell(row,1).Value = d.EmployeeId;
            ws.Cell(row,2).Value = d.EmployeeName;
            ws.Cell(row,3).Value = d.LeaveTypeName;
            ws.Cell(row,4).Value = d.StartDate;
            ws.Cell(row,5).Value = d.EndDate;
            ws.Cell(row,6).Value = d.TotalDays;
            ws.Cell(row,7).Value = d.Status;
            ws.Cell(row,8).Value = d.Reason;
            // Colour rows by status
            var fill = d.Status == "Approved" ? XLColor.LightGreen
                     : d.Status == "Rejected" ? XLColor.LightCoral : XLColor.LightYellow;
            ws.Row(row).Style.Fill.BackgroundColor = fill;
            row++;
        }
        // Summary block below data
        row += 2;
        ws.Cell(row,1).Value = "Summary"; ws.Cell(row,1).Style.Font.Bold = true;
        ws.Cell(row+1,1).Value = "Total Requests";     ws.Cell(row+1,2).Value = data.TotalRequests;
        ws.Cell(row+2,1).Value = "Approved";           ws.Cell(row+2,2).Value = data.Approved;
        ws.Cell(row+3,1).Value = "Rejected";           ws.Cell(row+3,2).Value = data.Rejected;
        ws.Cell(row+4,1).Value = "Pending";            ws.Cell(row+4,2).Value = data.Pending;
        ws.Cell(row+5,1).Value = "Total Days Approved"; ws.Cell(row+5,2).Value = data.TotalDaysApproved;

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Admin dashboard stats ──────────────────────────────────────────────
    public async Task<DashboardStatsDto> GetAdminDashboardStatsAsync(int? companyId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var empQ = _db.Employees.Where(e => e.IsActive);
        if (companyId.HasValue) empQ = empQ.Where(e => e.CompanyId == companyId);
        var empCount = await empQ.CountAsync();
        var empIds   = await empQ.Select(e => e.EmployeeCode).ToListAsync();

        var presentToday = await _db.WebAttendances
            .CountAsync(a => empIds.Contains(a.EmployeeId) && a.AttDate == today && a.Status == "Present");
        var pendingLeaves = await _db.LeaveRequests
            .CountAsync(r => (companyId == null || r.CompanyId == companyId) && r.Status == "Pending");
        var thisMonth = DateTime.UtcNow;
        var payrollRun = await _db.Payslips
            .Where(p => empIds.Contains(p.EmployeeId) && p.Month == thisMonth.Month && p.Year == thisMonth.Year)  // empIds is List<string> (EmployeeCode)
            .SumAsync(p => (decimal?)p.NetPay) ?? 0m;

        // "Active" reflects the employment Status column (Active / Inactive / Resigned…)
        var activeCount = await empQ.CountAsync(e => e.Status == "Active");

        return new DashboardStatsDto {
            TotalEmployees    = empCount,
            ActiveEmployees   = activeCount,
            PresentToday      = presentToday,
            AbsentToday       = Math.Max(0, empCount - presentToday),
            PendingLeaves     = pendingLeaves,
            TotalPayrollThisMonth = payrollRun
        };
    }

    public async Task<DashboardStatsDto> GetSuperAdminDashboardStatsAsync()
    {
        var totalCompanies  = await _db.Companies.CountAsync();
        var totalEmployees  = await _db.Employees.CountAsync(e => e.IsActive);
        var pendingLeaves   = await _db.LeaveRequests.CountAsync(r => r.Status == "Pending");
        var today           = DateOnly.FromDateTime(DateTime.UtcNow);
        var presentToday    = await _db.WebAttendances.CountAsync(a => a.AttDate == today && a.Status == "Present");
        var thisMonth       = DateTime.UtcNow;
        var totalPayroll    = await _db.Payslips
            .Where(p => p.Month == thisMonth.Month && p.Year == thisMonth.Year)
            .SumAsync(p => (decimal?)p.NetPay) ?? 0m;

        var activeEmployees = await _db.Employees.CountAsync(e => e.IsActive && e.Status == "Active");

        return new DashboardStatsDto {
            TotalCompanies = totalCompanies, TotalEmployees = totalEmployees,
            ActiveEmployees = activeEmployees,
            PresentToday   = presentToday, PendingLeaves = pendingLeaves,
            TotalPayrollThisMonth = totalPayroll
        };
    }

    public async Task<DashboardKpiDto> GetDashboardKpisAsync(int? companyId)
    {
        var stats = companyId.HasValue
            ? await GetAdminDashboardStatsAsync(companyId)
            : await GetSuperAdminDashboardStatsAsync();
        return new DashboardKpiDto {
            TotalEmployees  = stats.TotalEmployees,
            PresentToday    = stats.PresentToday,
            PendingLeaves   = stats.PendingLeaves,
            PayrollThisMonth = stats.TotalPayrollThisMonth
        };
    }

    // ── Employee dashboard stats ───────────────────────────────────────────
    public async Task<EmployeeDashboardStatsDto> GetEmployeeDashboardStatsAsync(
        string employeeId, int? companyId)
    {
        var now   = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var emp   = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == employeeId);

        // Attendance today
        var todayAtt = await _db.WebAttendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttDate == today);

        // Hours worked
        decimal? hoursWorked = null;
        if (todayAtt?.CheckIn != null && todayAtt.CheckOut != null)
            hoursWorked = (decimal)(todayAtt.CheckOut.Value - todayAtt.CheckIn.Value).TotalHours;

        // Attendance days this month
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var monthEnd   = monthStart.AddMonths(1).AddDays(-1);
        var attThisMonth = await _db.WebAttendances
            .CountAsync(a => a.EmployeeId == employeeId
                          && a.AttDate >= monthStart && a.AttDate <= monthEnd
                          && a.Status == "Present");

        // Leave stats
        var pendingLeaves = await _db.LeaveRequests
            .CountAsync(r => r.EmployeeId == employeeId && r.Status == "Pending");
        var approvedThisMonth = await _db.LeaveRequests
            .CountAsync(r => r.EmployeeId == employeeId && r.Status == "Approved"
                          && r.StartDate.Month == now.Month && r.StartDate.Year == now.Year);
        var usedThisYear = await _db.LeaveRequests
            .Where(r => r.EmployeeId == employeeId && r.Status == "Approved" && r.StartDate.Year == now.Year)
            .SumAsync(r => (int?)r.TotalDays) ?? 0;

        // Last payslip
        var lastPayslip = await _db.Payslips
            .Where(p => p.EmployeeId == employeeId)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .FirstOrDefaultAsync();

        // Upcoming holidays
        var upcomingHolidays = await _db.HolidayCalendars
            .CountAsync(h => h.IsActive
                          && (h.CompanyId == null || h.CompanyId == companyId)
                          && h.Date >= today && h.Date <= today.AddDays(30));

        return new EmployeeDashboardStatsDto {
            EmployeeId              = employeeId,
            FullName                = emp?.FullName,
            PendingLeaves           = pendingLeaves,
            ApprovedLeavesThisMonth = approvedThisMonth,
            TotalLeavesUsedThisYear = usedThisYear,
            CheckedInToday          = todayAtt?.CheckIn != null,
            TodayCheckInTime        = todayAtt?.CheckIn?.ToString("HH:mm"),
            TodayCheckOutTime       = todayAtt?.CheckOut?.ToString("HH:mm"),
            HoursWorkedToday        = hoursWorked.HasValue ? Math.Round(hoursWorked.Value, 2) : null,
            AttendanceDaysThisMonth = attThisMonth,
            WorkingDaysThisMonth    = monthEnd.Day,
            LastNetPay              = lastPayslip?.NetPay,
            LastPayMonth            = lastPayslip != null
                ? $"{new[]{"","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"}[lastPayslip.Month]} {lastPayslip.Year}"
                : null,
            UpcomingHolidays        = upcomingHolidays
        };
    }
}

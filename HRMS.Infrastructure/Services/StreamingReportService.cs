using System.Diagnostics;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using HRMS.Application.DTOs.Report;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Memory-efficient streaming Excel exports using OpenXmlWriter.
/// Replaces the ClosedXML in-memory approach for large datasets (100k+ rows).
///
/// Key differences from ClosedXML:
///   - Rows are written directly to the ZIP stream; no complete workbook object in RAM.
///   - Memory usage is O(batch) not O(total rows).
///   - Suitable for payroll registers, attendance, and employee reports at enterprise scale.
/// </summary>
public class StreamingReportService : IStreamingReportService
{
    private readonly ApplicationDbContext _db;
    private readonly HrmsMetrics _metrics;
    private readonly ILogger<StreamingReportService> _logger;

    public StreamingReportService(
        ApplicationDbContext db,
        HrmsMetrics metrics,
        ILogger<StreamingReportService> logger)
    {
        _db      = db;
        _metrics = metrics;
        _logger  = logger;
    }

    // ── Attendance (streamed) ──────────────────────────────────────────────
    public async Task<byte[]> ExportAttendanceReportStreamAsync(
        int? companyId, int month, int year, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var from = new DateOnly(year, month, 1);
        var to   = from.AddMonths(1).AddDays(-1);

        // Projected query — only fetch the columns we need (no full entity hydration)
        // BUG FIX: this query was missing the companyId filter entirely, unlike the
        // excRows query below it. For a SuperAdmin explicitly requesting one company's
        // export via ?companyId=X, the EF global query filter on WebAttendance does NOT
        // apply (SuperAdmin callers bypass tenant filtering by design), so every other
        // company's web-punch attendance was silently mixed into the "single company"
        // export. Non-SuperAdmin callers were unaffected only because the global query
        // filter happened to already scope them — this fix makes the scoping explicit
        // and correct for both caller types, matching excRows immediately below.
        var webRows = await _db.WebAttendances
            .AsNoTracking()
            .Where(a => a.AttDate >= from && a.AttDate <= to
                     && (!companyId.HasValue || a.CompanyId == companyId))
            .Select(a => new {
                a.EmployeeId, a.AttDate,
                a.Status, a.CheckIn, a.CheckOut,
                Source = "Web", HoursWorked = (decimal?)null
            })
            .ToListAsync(ct);

        var excRows = await _db.ExcelAttendances
            .AsNoTracking()
            .Where(a => a.AttDate >= from && a.AttDate <= to
                     && (!companyId.HasValue || a.CompanyId == companyId))
            .Select(a => new {
                a.EmployeeId, a.AttDate,
                a.Status, CheckIn = (TimeOnly?)null, CheckOut = (TimeOnly?)null,
                Source = "Excel", HoursWorked = (decimal?)a.HoursWorked
            })
            .ToListAsync(ct);

        var empIds = webRows.Select(r => r.EmployeeId)
            .Concat(excRows.Select(r => r.EmployeeId))
            .Distinct().ToList();

        var empDict = await _db.Employees
            .AsNoTracking()
            .Where(e => empIds.Contains(e.EmployeeCode))
            .Select(e => new { EmployeeId = e.EmployeeCode, e.FullName })
            .ToDictionaryAsync(e => e.EmployeeId, e => e.FullName, ct);

        var allRows = webRows.Cast<dynamic>().Concat(excRows.Cast<dynamic>())
            .OrderBy(r => r.AttDate.ToString())
            .ThenBy(r => (string)r.EmployeeId)
            .ToList();

        var headers = new[] {
            "Employee ID", "Employee Name", "Date", "Status",
            "Source", "Check-In", "Check-Out", "Hours Worked"
        };

        var period = $"{new[]{"","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"}[month]} {year}";
        var bytes  = BuildStreamingXlsx($"Attendance – {period}", "Attendance", headers, allRows,
            r => new object?[] {
                (string)r.EmployeeId,
                empDict.GetValueOrDefault((string)r.EmployeeId),
                ((DateOnly)r.AttDate).ToString("yyyy-MM-dd"),
                (string)r.Status,
                (string)r.Source,
                r.CheckIn?.ToString("HH:mm"),
                r.CheckOut?.ToString("HH:mm"),
                r.HoursWorked?.ToString("F2")
            });

        sw.Stop();
        _metrics.RecordReport("Attendance", allRows.Count, sw.Elapsed.TotalMilliseconds);
        _logger.LogInformation("Streaming attendance export: {Rows} rows in {Ms:F0}ms", allRows.Count, sw.Elapsed.TotalMilliseconds);

        return bytes;
    }

    // ── Payroll Register (streamed) ────────────────────────────────────────
    public async Task<byte[]> ExportPayrollReportStreamAsync(
        int? companyId, int month, int year, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Single efficient query with projection — no N+1
        var query = from p in _db.Payslips.AsNoTracking()
                    join e in _db.Employees.AsNoTracking()
                        on p.EmployeeId equals e.EmployeeCode
                    where p.Month == month && p.Year == year
                          && (!companyId.HasValue || e.CompanyId == companyId)
                    orderby e.Department, e.FullName
                    select new {
                        p.EmployeeId, e.FullName, e.Department, e.Designation,
                        p.GrossEarnings, p.PFEmployee, p.PFEmployer,
                        p.ESI, p.PT, p.TDS, p.NetPay
                    };

        var rows = await query.ToListAsync(ct);

        var headers = new[] {
            "Employee ID", "Name", "Department", "Designation",
            "Gross", "PF(Emp)", "PF(Empl)", "ESI", "PT", "TDS", "Net Pay"
        };

        var period = $"{new[]{"","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"}[month]} {year}";
        var bytes  = BuildStreamingXlsx($"Payroll – {period}", "Payroll", headers, rows,
            r => new object?[] {
                r.EmployeeId, r.FullName, r.Department, r.Designation,
                (double)r.GrossEarnings, (double)r.PFEmployee, (double)r.PFEmployer,
                (double)r.ESI, (double)r.PT, (double)r.TDS, (double)r.NetPay
            });

        sw.Stop();
        _metrics.RecordReport("Payroll", rows.Count, sw.Elapsed.TotalMilliseconds);
        _logger.LogInformation("Streaming payroll export: {Rows} rows in {Ms:F0}ms", rows.Count, sw.Elapsed.TotalMilliseconds);

        return bytes;
    }

    // ── Employee Summary (streamed) ────────────────────────────────────────
    public async Task<byte[]> ExportEmployeeReportStreamAsync(
        int? companyId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var rows = await _db.Employees
            .AsNoTracking()
            .Where(e => !companyId.HasValue || e.CompanyId == companyId)
            .OrderBy(e => e.Department).ThenBy(e => e.FullName)
            .Select(e => new {
                EmployeeId = e.EmployeeCode, e.FullName, e.Department, e.Designation,
                e.DateOfJoining, e.IsActive
            })
            .ToListAsync(ct);

        var headers = new[] {
            "Employee ID", "Full Name", "Department", "Designation",
            "Joining Date", "Status"
        };

        var bytes = BuildStreamingXlsx("Employee Report", "Employees", headers, rows,
            r => new object?[] {
                r.EmployeeId, r.FullName, r.Department, r.Designation,
                r.DateOfJoining?.ToString("yyyy-MM-dd") ?? string.Empty,
                r.IsActive ? "Active" : "Inactive"
            });

        sw.Stop();
        _metrics.RecordReport("Employee", rows.Count, sw.Elapsed.TotalMilliseconds);
        _logger.LogInformation("Streaming employee export: {Rows} rows in {Ms:F0}ms", rows.Count, sw.Elapsed.TotalMilliseconds);

        return bytes;
    }

    // ── Salary Register (streamed) ─────────────────────────────────────────
    public async Task<byte[]> ExportSalaryRegisterStreamAsync(
        int? companyId, int month, int year, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var query = from p in _db.Payslips.AsNoTracking()
                    join e in _db.Employees.AsNoTracking()
                        on p.EmployeeId equals e.EmployeeCode
                    where p.Month == month && p.Year == year
                          && (!companyId.HasValue || e.CompanyId == companyId)
                    orderby e.Department, e.FullName
                    select new {
                        p.EmployeeId, e.FullName, e.Department, e.Designation,
                        e.BankName, e.AccountNumber, e.UAN,
                        p.DaysPresent, p.WorkingDays,
                        p.BasicPay, p.HRA, p.DA, p.Conveyance,
                        p.MedicalAllowance, p.OtherAllowances,
                        p.GrossEarnings, p.PFEmployee, p.PFEmployer,
                        p.ESI, p.PT, p.TDS, p.TotalDeductions, p.NetPay
                    };

        var rows = await query.ToListAsync(ct);

        var headers = new[] {
            "Employee ID","Name","Department","Designation","Bank","Account","UAN",
            "Days Present","Working Days","Basic","HRA","DA","Conveyance","Medical","Other Allow",
            "Gross","PF(Emp)","PF(Empl)","ESI","PT","TDS","Total Deductions","Net Pay"
        };

        var period = $"{new[]{"","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"}[month]} {year}";
        var bytes  = BuildStreamingXlsx($"Salary Register – {period}", "SalaryRegister", headers, rows,
            r => new object?[] {
                r.EmployeeId, r.FullName, r.Department, r.Designation,
                r.BankName, r.AccountNumber, r.UAN,
                r.DaysPresent, r.WorkingDays,
                (double)r.BasicPay, (double)r.HRA, (double)r.DA, (double)r.Conveyance,
                (double)r.MedicalAllowance, (double)r.OtherAllowances,
                (double)r.GrossEarnings, (double)r.PFEmployee, (double)r.PFEmployer,
                (double)r.ESI, (double)r.PT, (double)r.TDS,
                (double)r.TotalDeductions, (double)r.NetPay
            });

        sw.Stop();
        _metrics.RecordReport("SalaryRegister", rows.Count, sw.Elapsed.TotalMilliseconds);
        _logger.LogInformation("Streaming salary register: {Rows} rows in {Ms:F0}ms", rows.Count, sw.Elapsed.TotalMilliseconds);

        return bytes;
    }

    // ── Leave Report (streamed) ────────────────────────────────────────────
    public async Task<byte[]> ExportLeaveReportStreamAsync(
        int? companyId, int month, int year, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var query = from r in _db.LeaveRequests.AsNoTracking()
                    join e in _db.Employees.AsNoTracking()
                        on r.EmployeeId equals e.EmployeeCode
                    join lt in _db.LeaveTypes.AsNoTracking()
                        on r.LeaveTypeId equals lt.Id
                    where (month == 0 || r.StartDate.Month == month)
                          && (year == 0 || r.StartDate.Year == year)
                          && (!companyId.HasValue || e.CompanyId == companyId)
                    orderby r.StartDate descending
                    select new {
                        r.EmployeeId, e.FullName, LeaveType = lt.Name,
                        StartDate = r.StartDate.ToString("yyyy-MM-dd"),
                        EndDate   = r.EndDate.ToString("yyyy-MM-dd"),
                        r.TotalDays, r.Status, r.Reason
                    };

        var rows = await query.ToListAsync(ct);

        var headers = new[] {
            "Employee ID", "Employee Name", "Leave Type",
            "Start Date", "End Date", "Days", "Status", "Reason"
        };

        var period = month > 0
            ? $"{new[]{"","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"}[month]} {year}"
            : $"Year {year}";
        var bytes  = BuildStreamingXlsx($"Leave Report – {period}", "LeaveReport", headers, rows,
            r => new object?[] {
                r.EmployeeId, r.FullName, r.LeaveType,
                r.StartDate, r.EndDate, r.TotalDays, r.Status, r.Reason
            });

        sw.Stop();
        _metrics.RecordReport("Leave", rows.Count, sw.Elapsed.TotalMilliseconds);
        _logger.LogInformation("Streaming leave export: {Rows} rows in {Ms:F0}ms", rows.Count, sw.Elapsed.TotalMilliseconds);

        return bytes;
    }

    // ── Core streaming engine ──────────────────────────────────────────────
    /// <summary>
    /// Writes rows directly to a MemoryStream using OpenXmlWriter.
    /// Each row is serialised to the stream immediately — no complete workbook
    /// object is held in RAM, so memory usage is proportional to the batch size,
    /// not the total row count.
    /// </summary>
    private static byte[] BuildStreamingXlsx<T>(
        string title,
        string sheetName,
        string[] headers,
        IReadOnlyList<T> rows,
        Func<T, object?[]> rowMapper)
    {
        using var ms = new MemoryStream();

        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            // ── Workbook ──
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            // ── Shared strings (minimal — avoids generating large shared-string tables) ──
            var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheets    = workbookPart.Workbook.AppendChild(new Sheets());
            var sheet     = new Sheet {
                Id     = workbookPart.GetIdOfPart(sheetPart),
                SheetId = 1,
                Name   = sheetName.Length > 31 ? sheetName[..31] : sheetName
            };
            sheets.Append(sheet);

            // ── Styles (bold header row) ──
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = BuildStylesheet();

            // ── Stream rows via OpenXmlWriter ──
            using var writer = OpenXmlWriter.Create(sheetPart);
            writer.WriteStartElement(new Worksheet());
            writer.WriteStartElement(new SheetData());

            // Title row
            WriteRow(writer, new object?[] { title }, rowIndex: 1, styleIndex: 1);

            // Header row
            WriteRow(writer, headers.Cast<object?>().ToArray(), rowIndex: 2, styleIndex: 1);

            // Data rows
            for (int i = 0; i < rows.Count; i++)
            {
                var cells = rowMapper(rows[i]);
                WriteRow(writer, cells, rowIndex: i + 3, styleIndex: 0);
            }

            writer.WriteEndElement(); // SheetData
            writer.WriteEndElement(); // Worksheet
        }

        return ms.ToArray();
    }

    private static void WriteRow(OpenXmlWriter writer, object?[] cells, int rowIndex, uint styleIndex)
    {
        writer.WriteStartElement(new Row { RowIndex = (uint)rowIndex });

        for (int i = 0; i < cells.Length; i++)
        {
            var colName = GetColumnName(i + 1);
            var cellRef = $"{colName}{rowIndex}";
            var cell    = new Cell { CellReference = cellRef, StyleIndex = styleIndex };

            var val = cells[i];
            if (val is null)
            {
                cell.DataType = CellValues.String;
                cell.CellValue = new CellValue(string.Empty);
            }
            else if (val is double d)
            {
                cell.DataType   = CellValues.Number;
                cell.CellValue  = new CellValue(d.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (val is decimal dec)
            {
                cell.DataType  = CellValues.Number;
                cell.CellValue = new CellValue(((double)dec).ToString("G", System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (val is int iv)
            {
                cell.DataType  = CellValues.Number;
                cell.CellValue = new CellValue(iv.ToString());
            }
            else
            {
                cell.DataType  = CellValues.InlineString;
                cell.CellValue = new CellValue(val.ToString() ?? string.Empty);
            }

            writer.WriteElement(cell);
        }

        writer.WriteEndElement(); // Row
    }

    private static string GetColumnName(int columnIndex)
    {
        var name = string.Empty;
        while (columnIndex > 0)
        {
            var mod = (columnIndex - 1) % 26;
            name = (char)('A' + mod) + name;
            columnIndex = (columnIndex - mod - 1) / 26;
        }
        return name;
    }

    private static Stylesheet BuildStylesheet()
    {
        var boldFont = new Font(
            new Bold(),
            new FontSize { Val = 11 },
            new Color { Rgb = "FF000000" },
            new FontName { Val = "Calibri" });

        var normalFont = new Font(
            new FontSize { Val = 11 },
            new Color { Rgb = "FF000000" },
            new FontName { Val = "Calibri" });

        var fonts = new Fonts(normalFont, boldFont);
        fonts.Count = 2;

        var fills = new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));
        fills.Count = 2;

        var borders = new Borders(new Border());
        borders.Count = 1;

        var cellFormats = new CellFormats(
            new CellFormat { FontId = 0, FillId = 0, BorderId = 0 },               // 0: Normal
            new CellFormat { FontId = 1, FillId = 0, BorderId = 0, ApplyFont = true }); // 1: Bold
        cellFormats.Count = 2;

        return new Stylesheet(fonts, fills, borders, cellFormats);
    }
    // ── Attendance upload (SAX reader) ─────────────────────────────────────
    /// <inheritdoc/>
    /// <remarks>
    /// Uses OpenXmlReader (SAX-mode) to walk the worksheet element-by-element.
    /// Only one row is held in memory at a time — O(1) peak vs O(n) for ClosedXML XLWorkbook.Load().
    /// </remarks>
    public Task<IReadOnlyList<AttendanceExcelRow>> ReadAttendanceUploadRowsAsync(
        Stream stream, CancellationToken ct = default)
    {
        // Open XML SDK I/O is synchronous; Task.Run avoids blocking the HTTP request thread.
        return Task.Run(() => ReadRowsCore(stream), ct);
    }

    private static IReadOnlyList<AttendanceExcelRow> ReadRowsCore(Stream stream)
    {
        var rows = new List<AttendanceExcelRow>();

        using var doc = SpreadsheetDocument.Open(stream, isEditable: false);
        var workbookPart = doc.WorkbookPart
            ?? throw new InvalidOperationException("The uploaded file has no workbook part.");
        var worksheetPart = workbookPart.WorksheetParts.FirstOrDefault()
            ?? throw new InvalidOperationException("The uploaded file contains no worksheets.");

        // Shared-strings table is loaded once; cell text is stored by index when type == SharedString
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

        using var reader = OpenXmlReader.Create(worksheetPart);
        int rowNumber = 0;

        while (reader.Read())
        {
            if (reader.ElementType != typeof(Row)) continue;
            var element = reader.LoadCurrentElement();
            rowNumber++;
            if (rowNumber == 1) continue; // skip header

            var row   = (Row)element!;
            var cells = row.Elements<Cell>().ToList();

            rows.Add(new AttendanceExcelRow(
                RowNumber:  rowNumber,
                EmployeeId: GetCellText(cells, "A", sharedStrings),
                DateStr:    GetCellText(cells, "B", sharedStrings),
                Status:     GetCellText(cells, "C", sharedStrings),
                HoursStr:   GetCellText(cells, "D", sharedStrings)));
        }

        return rows;
    }

    private static string? GetCellText(
        IReadOnlyList<Cell> cells, string column, SharedStringTable? sharedStrings)
    {
        var cell = cells.FirstOrDefault(c =>
            c.CellReference?.Value?.StartsWith(column, StringComparison.OrdinalIgnoreCase) == true);
        if (cell == null) return null;
        var raw = cell.InnerText?.Trim();
        if (string.IsNullOrEmpty(raw)) return null;
        if (cell.DataType?.Value == CellValues.SharedString
            && sharedStrings != null
            && int.TryParse(raw, out var idx))
        {
            return sharedStrings.ElementAt(idx).InnerText?.Trim();
        }
        return raw;
    }


}

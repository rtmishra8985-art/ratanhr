namespace HRMS.Application.Interfaces;

/// <summary>
/// Memory-efficient Excel export/import interface for large datasets.
/// Uses OpenXmlWriter streaming — safe for 100k+ rows without excessive RAM.
/// </summary>
public interface IStreamingReportService
{
    Task<byte[]> ExportAttendanceReportStreamAsync(int? companyId, int month, int year, CancellationToken ct = default);
    Task<byte[]> ExportPayrollReportStreamAsync(int? companyId, int month, int year, CancellationToken ct = default);
    Task<byte[]> ExportEmployeeReportStreamAsync(int? companyId, CancellationToken ct = default);
    Task<byte[]> ExportSalaryRegisterStreamAsync(int? companyId, int month, int year, CancellationToken ct = default);
    Task<byte[]> ExportLeaveReportStreamAsync(int? companyId, int month, int year, CancellationToken ct = default);

    /// <summary>
    /// Reads attendance upload rows from an Excel stream row-by-row using Open XML SDK SAX reader.
    /// Unlike ClosedXML's XLWorkbook.Load() this approach is O(batch) memory, not O(total rows),
    /// preventing OOM on large company uploads.
    /// </summary>
    /// <param name="stream">The uploaded Excel file stream (positioned at start).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered list of raw row data, skipping the header row (row 1).</returns>
    Task<IReadOnlyList<AttendanceExcelRow>> ReadAttendanceUploadRowsAsync(
        Stream stream, CancellationToken ct = default);
}

/// <summary>
/// Raw row data extracted from an attendance Excel upload.
/// Columns: 1=EmployeeId, 2=Date, 3=Status, 4=HoursWorked.
/// </summary>
public sealed record AttendanceExcelRow(
    int RowNumber,
    string? EmployeeId,
    string? DateStr,
    string? Status,
    string? HoursStr);

using HRMS.Application.DTOs.Payroll;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRMS.Infrastructure.PDF;

public class PayslipPdfGenerator
{
    static PayslipPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // FIX LOW-PDF1: Wrap GeneratePdf() in try-catch so a QuestPDF rendering failure
    // (e.g. missing system font, malformed DTO value) throws an informative exception
    // rather than an unhandled host crash. Callers (PayslipController) should catch
    // this and return 500 with a safe error message.
    public byte[] Generate(PayslipPdfDto dto)
    {
        try
        {
            return Document.Create(container =>
            {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                // ── Header ──────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(dto.CompanyName).Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
                            c.Item().Text("PAYSLIP").FontSize(13).FontColor(Colors.Grey.Darken2);
                            c.Item().Text($"Pay Period: {dto.PayPeriod}").FontSize(11);
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(2).LineColor(Colors.Blue.Darken3);
                });

                // ── Content ─────────────────────────────────────────────
                page.Content().PaddingVertical(12).Column(col =>
                {
                    // Employee info box
                    col.Item().Background(Colors.Blue.Lighten5).Padding(10).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                        void InfoRow(string label, string value)
                        {
                            t.Cell().Text(label).Bold();
                            t.Cell().Text(value);
                        }
                        InfoRow("Employee Name:", dto.EmployeeName);
                        InfoRow("Employee ID:", dto.EmployeeId);
                        InfoRow("Department:", dto.Department);
                        InfoRow("Designation:", dto.Designation);
                        InfoRow("Working Days:", dto.WorkingDays.ToString());
                        InfoRow("Days Present:", dto.DaysPresent.ToString());
                    });

                    col.Item().PaddingTop(16).Text("EARNINGS").Bold().FontSize(12).FontColor(Colors.Green.Darken3);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(2); });
                        t.Header(h =>
                        {
                            h.Cell().Background(Colors.Green.Lighten4).Padding(4).Text("Component").Bold();
                            h.Cell().Background(Colors.Green.Lighten4).Padding(4).Text("Amount (₹)").Bold().AlignRight();
                        });
                        void ERow(string label, decimal amount)
                        {
                            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(label);
                            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(amount.ToString("N2")).AlignRight();
                        }
                        ERow("Basic Pay", dto.BasicPay);
                        ERow("HRA", dto.HRA);
                        ERow("DA", dto.DA);
                        ERow("Conveyance", dto.Conveyance);
                        ERow("Medical Allowance", dto.MedicalAllowance);
                        ERow("Other Allowances", dto.OtherAllowances);
                        t.Cell().Background(Colors.Green.Lighten4).Padding(4).Text("Gross Earnings").Bold();
                        t.Cell().Background(Colors.Green.Lighten4).Padding(4).Text(dto.GrossPay.ToString("N2")).Bold().AlignRight();
                    });

                    col.Item().PaddingTop(16).Text("DEDUCTIONS").Bold().FontSize(12).FontColor(Colors.Red.Darken3);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(2); });
                        t.Header(h =>
                        {
                            h.Cell().Background(Colors.Red.Lighten4).Padding(4).Text("Component").Bold();
                            h.Cell().Background(Colors.Red.Lighten4).Padding(4).Text("Amount (₹)").Bold().AlignRight();
                        });
                        void DRow(string label, decimal amount)
                        {
                            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(label);
                            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(amount.ToString("N2")).AlignRight();
                        }
                        DRow("PF (Employee)", dto.PFDeduction);
                        DRow("ESI", dto.ESIDeduction);
                        DRow("Professional Tax", dto.PTDeduction);
                        DRow("TDS", dto.TDSDeduction);
                        DRow("Other Deductions", dto.OtherDeductions);
                        t.Cell().Background(Colors.Red.Lighten4).Padding(4).Text("Total Deductions").Bold();
                        t.Cell().Background(Colors.Red.Lighten4).Padding(4).Text(dto.TotalDeductions.ToString("N2")).Bold().AlignRight();
                    });

                    // Net Pay
                    col.Item().PaddingTop(20)
                        .Background(Colors.Blue.Darken3).Padding(12)
                        .Row(r =>
                        {
                            r.RelativeItem().Text("NET PAY").Bold().FontSize(14).FontColor(Colors.White);
                            r.RelativeItem().AlignRight()
                                .Text($"₹ {dto.NetPay:N2}").Bold().FontSize(14).FontColor(Colors.White);
                        });
                });

                // ── Footer ─────────────────────────────────────────────
                page.Footer().PaddingTop(8).Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(4).AlignCenter()
                        .Text("This is a computer-generated payslip. No signature required.")
                        .FontColor(Colors.Grey.Darken1).FontSize(8);
                });
            });
            }).GeneratePdf();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"PDF generation failed for employee '{dto?.EmployeeId ?? "unknown"}': {ex.Message}", ex);
        }
    }
}

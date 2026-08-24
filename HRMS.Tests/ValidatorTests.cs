using FluentAssertions;
using FluentValidation.TestHelper;
using HRMS.Application.DTOs;
using HRMS.Application.DTOs.Auth;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.DTOs.Leave;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Validators;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// FluentValidation tests for all command DTOs.
/// Each validator is exercised for: Required, Length, Regex, Range, Email, Phone, Business rules.
/// </summary>
public class ValidatorTests
{
    // ─── LoginDto ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LoginValidator_EmptyEmail_FailsValidation()
    {
        var validator = new LoginValidator();
        var result    = validator.TestValidate(new LoginDto { Email = "", Password = "Test@1234", Portal = "Admin" });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void LoginValidator_InvalidEmail_FailsValidation()
    {
        var validator = new LoginValidator();
        var result    = validator.TestValidate(new LoginDto { Email = "not-an-email", Password = "Test@1234", Portal = "Admin" });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void LoginValidator_InvalidPortal_FailsValidation()
    {
        var validator = new LoginValidator();
        var result    = validator.TestValidate(new LoginDto { Email = "a@b.com", Password = "Test@1234", Portal = "HackerPortal" });
        result.ShouldHaveValidationErrorFor(x => x.Portal);
    }

    [Fact]
    public void LoginValidator_ValidInput_PassesValidation()
    {
        var validator = new LoginValidator();
        var result    = validator.TestValidate(new LoginDto { Email = "a@b.com", Password = "Test@1234", Portal = "Admin" });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void LoginValidator_EmptyPassword_FailsValidation()
    {
        var validator = new LoginValidator();
        var result    = validator.TestValidate(new LoginDto { Email = "a@b.com", Password = "", Portal = "Admin" });
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    // ─── ResetPasswordDto ─────────────────────────────────────────────────────────

    [Fact]
    public void ResetPasswordValidator_PasswordMismatch_FailsValidation()
    {
        var validator = new ResetPasswordValidator();
        var result    = validator.TestValidate(new ResetPasswordDto { NewPassword = "Test@1234", ConfirmPassword = "Different@1234" });
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void ResetPasswordValidator_WeakPassword_FailsValidation()
    {
        var validator = new ResetPasswordValidator();
        var result    = validator.TestValidate(new ResetPasswordDto { NewPassword = "abc", ConfirmPassword = "abc" });
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void ResetPasswordValidator_ValidInput_PassesValidation()
    {
        var validator = new ResetPasswordValidator();
        var result    = validator.TestValidate(new ResetPasswordDto { NewPassword = "Strong@1234", ConfirmPassword = "Strong@1234", Token = "valid-token" });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ResetPasswordValidator_PasswordWithoutUpperCase_FailsValidation()
    {
        var validator = new ResetPasswordValidator();
        var result    = validator.TestValidate(new ResetPasswordDto { NewPassword = "lowercase@1234", ConfirmPassword = "lowercase@1234" });
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void ResetPasswordValidator_PasswordWithoutSpecialChar_FailsValidation()
    {
        var validator = new ResetPasswordValidator();
        var result    = validator.TestValidate(new ResetPasswordDto { NewPassword = "NoSpecial1234", ConfirmPassword = "NoSpecial1234" });
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    // ─── UpdateAttendanceStatusDto ────────────────────────────────────────────────

    [Fact]
    public void UpdateAttendanceStatusValidator_InvalidStatus_FailsValidation()
    {
        var validator = new UpdateAttendanceStatusValidator();
        var result    = validator.TestValidate(new UpdateAttendanceStatusDto { AttendanceId = 1, Status = "INVALID" });
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void UpdateAttendanceStatusValidator_ZeroId_FailsValidation()
    {
        var validator = new UpdateAttendanceStatusValidator();
        var result    = validator.TestValidate(new UpdateAttendanceStatusDto { AttendanceId = 0, Status = "Present" });
        result.ShouldHaveValidationErrorFor(x => x.AttendanceId);
    }

    [Fact]
    public void UpdateAttendanceStatusValidator_NegativeId_FailsValidation()
    {
        var validator = new UpdateAttendanceStatusValidator();
        var result    = validator.TestValidate(new UpdateAttendanceStatusDto { AttendanceId = -1, Status = "Present" });
        result.ShouldHaveValidationErrorFor(x => x.AttendanceId);
    }

    [Theory]
    [InlineData("Present")]
    [InlineData("Half Day")]
    [InlineData("Absent")]
    public void UpdateAttendanceStatusValidator_ValidStatuses_PassValidation(string status)
    {
        var validator = new UpdateAttendanceStatusValidator();
        var result    = validator.TestValidate(new UpdateAttendanceStatusDto { AttendanceId = 1, Status = status });
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    // ─── EditAttendanceDto ────────────────────────────────────────────────────────

    [Fact]
    public void EditAttendanceValidator_EmptyReason_FailsValidation()
    {
        var validator = new EditAttendanceValidator();
        var result    = validator.TestValidate(new EditAttendanceDto { AttendanceId = 1, Reason = "" });
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void EditAttendanceValidator_ShortReason_FailsValidation()
    {
        var validator = new EditAttendanceValidator();
        var result    = validator.TestValidate(new EditAttendanceDto { AttendanceId = 1, Reason = "ab" });
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void EditAttendanceValidator_ValidInput_PassesValidation()
    {
        var validator = new EditAttendanceValidator();
        var result    = validator.TestValidate(new EditAttendanceDto { AttendanceId = 1, Status = "Present", Reason = "Valid reason for edit" });
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ─── CreateShiftDto ───────────────────────────────────────────────────────────

    [Fact]
    public void CreateShiftValidator_InvalidTimeFormat_FailsValidation()
    {
        var validator = new CreateShiftValidator();
        var result    = validator.TestValidate(new CreateShiftDto { Name = "Day", StartTime = "25:00", EndTime = "33:00" });
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void CreateShiftValidator_ValidInput_PassesValidation()
    {
        var validator = new CreateShiftValidator();
        var result    = validator.TestValidate(new CreateShiftDto { Name = "Day Shift", StartTime = "09:00", EndTime = "18:00", CompanyId = 1 });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateShiftValidator_EmptyName_FailsValidation()
    {
        var validator = new CreateShiftValidator();
        var result    = validator.TestValidate(new CreateShiftDto { Name = "", StartTime = "09:00", EndTime = "18:00" });
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    // ─── ApplyLeaveDto ────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyLeaveValidator_EndDateBeforeStart_FailsValidation()
    {
        var validator = new ApplyLeaveValidator();
        var result    = validator.TestValidate(new ApplyLeaveDto
        {
            EmployeeId  = "E001",
            LeaveTypeId = 1,
            StartDate   = "2025-07-10",
            EndDate     = "2025-07-05",
            Reason      = "Vacation"
        });
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void ApplyLeaveValidator_ValidInput_PassesValidation()
    {
        var validator = new ApplyLeaveValidator();
        var result    = validator.TestValidate(new ApplyLeaveDto
        {
            EmployeeId  = "E001",
            LeaveTypeId = 1,
            StartDate   = "2026-12-01",
            EndDate     = "2026-12-05",
            Reason      = "Annual vacation"
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ApplyLeaveValidator_EmptyReason_FailsValidation()
    {
        var validator = new ApplyLeaveValidator();
        var result    = validator.TestValidate(new ApplyLeaveDto
        {
            EmployeeId  = "E001",
            LeaveTypeId = 1,
            StartDate   = "2025-07-01",
            EndDate     = "2025-07-02",
            Reason      = ""
        });
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    // ─── CreateLeaveTypeDto ───────────────────────────────────────────────────────

    [Fact]
    public void CreateLeaveTypeValidator_ZeroQuota_FailsValidation()
    {
        var validator = new CreateLeaveTypeValidator();
        var result    = validator.TestValidate(new CreateLeaveTypeDto { Name = "Annual", Quota = 0 });
        result.ShouldHaveValidationErrorFor(x => x.Quota);
    }

    [Fact]
    public void CreateLeaveTypeValidator_NegativeQuota_FailsValidation()
    {
        var validator = new CreateLeaveTypeValidator();
        var result    = validator.TestValidate(new CreateLeaveTypeDto { Name = "Annual", Quota = -5 });
        result.ShouldHaveValidationErrorFor(x => x.Quota);
    }

    [Fact]
    public void CreateLeaveTypeValidator_ExcessiveQuota_FailsValidation()
    {
        // Business rule: quota cannot exceed 365 days
        var validator = new CreateLeaveTypeValidator();
        var result    = validator.TestValidate(new CreateLeaveTypeDto { Name = "Annual", Quota = 400 });
        result.ShouldHaveValidationErrorFor(x => x.Quota);
    }

    // ─── GeneratePayslipDto ───────────────────────────────────────────────────────

    [Fact]
    public void GeneratePayslipValidator_InvalidMonth_FailsValidation()
    {
        var validator = new GeneratePayslipValidator();
        var result    = validator.TestValidate(new GeneratePayslipDto { EmployeeId = "E001", Month = 13, Year = 2025, BasicPay = 50000, WorkingDays = 26, DaysPresent = 26 });
        result.ShouldHaveValidationErrorFor(x => x.Month);
    }

    [Fact]
    public void GeneratePayslipValidator_MonthZero_FailsValidation()
    {
        var validator = new GeneratePayslipValidator();
        var result    = validator.TestValidate(new GeneratePayslipDto { EmployeeId = "E001", Month = 0, Year = 2025, BasicPay = 50000, WorkingDays = 26, DaysPresent = 26 });
        result.ShouldHaveValidationErrorFor(x => x.Month);
    }

    [Fact]
    public void GeneratePayslipValidator_DaysPresentExceedsWorkingDays_FailsValidation()
    {
        var validator = new GeneratePayslipValidator();
        var result    = validator.TestValidate(new GeneratePayslipDto { EmployeeId = "E001", Month = 6, Year = 2025, BasicPay = 50000, WorkingDays = 26, DaysPresent = 30 });
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void GeneratePayslipValidator_NegativeBasicPay_FailsValidation()
    {
        var validator = new GeneratePayslipValidator();
        var result    = validator.TestValidate(new GeneratePayslipDto { EmployeeId = "E001", Month = 6, Year = 2025, BasicPay = -1000, WorkingDays = 26, DaysPresent = 26 });
        result.ShouldHaveValidationErrorFor(x => x.BasicPay);
    }

    [Fact]
    public void GeneratePayslipValidator_ValidInput_PassesValidation()
    {
        var validator = new GeneratePayslipValidator();
        var result    = validator.TestValidate(new GeneratePayslipDto { EmployeeId = "E001", Month = 6, Year = 2025, BasicPay = 50000, WorkingDays = 26, DaysPresent = 26 });
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ─── BulkPayrollDto ───────────────────────────────────────────────────────────

    [Fact]
    public void BulkPayrollValidator_InvalidMonth_FailsValidation()
    {
        var validator = new BulkPayrollValidator();
        var result    = validator.TestValidate(new BulkPayrollDto { CompanyId = 1, Month = 0, Year = 2025, WorkingDays = 26 });
        result.ShouldHaveValidationErrorFor(x => x.Month);
    }

    [Fact]
    public void BulkPayrollValidator_ZeroWorkingDays_FailsValidation()
    {
        var validator = new BulkPayrollValidator();
        var result    = validator.TestValidate(new BulkPayrollDto { CompanyId = 1, Month = 6, Year = 2025, WorkingDays = 0 });
        result.ShouldHaveValidationErrorFor(x => x.WorkingDays);
    }

    // ─── CreateBonusDto ───────────────────────────────────────────────────────────

    [Fact]
    public void CreateBonusValidator_ZeroAmount_FailsValidation()
    {
        var validator = new CreateBonusValidator();
        var result    = validator.TestValidate(new CreateBonusDto { EmployeeId = "E001", CompanyId = 1, Amount = 0 });
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void CreateBonusValidator_EmptyEmployeeId_FailsValidation()
    {
        var validator = new CreateBonusValidator();
        var result    = validator.TestValidate(new CreateBonusDto { EmployeeId = "", CompanyId = 1, Amount = 5000 });
        result.ShouldHaveValidationErrorFor(x => x.EmployeeId);
    }

    // ─── CreateDeductionDto ───────────────────────────────────────────────────────

    [Fact]
    public void CreateDeductionValidator_MissingDeductionType_FailsValidation()
    {
        var validator = new CreateDeductionValidator();
        var result    = validator.TestValidate(new CreateDeductionDto { EmployeeId = "E001", CompanyId = 1, Amount = 1000, DeductionType = "" });
        result.ShouldHaveValidationErrorFor(x => x.DeductionType);
    }

    // ─── LeaveBalanceAdjustmentDto ────────────────────────────────────────────────

    [Fact]
    public void LeaveBalanceAdjustmentValidator_ZeroDays_FailsValidation()
    {
        var validator = new LeaveBalanceAdjustmentValidator();
        var result    = validator.TestValidate(new LeaveBalanceAdjustmentDto { EmployeeId = "E001", LeaveTypeId = 1, Days = 0 });
        result.ShouldHaveValidationErrorFor(x => x.Days);
    }

    [Fact]
    public void LeaveBalanceAdjustmentValidator_EmptyEmployeeId_FailsValidation()
    {
        var validator = new LeaveBalanceAdjustmentValidator();
        var result    = validator.TestValidate(new LeaveBalanceAdjustmentDto { EmployeeId = "", LeaveTypeId = 1, Days = 5 });
        result.ShouldHaveValidationErrorFor(x => x.EmployeeId);
    }

    [Fact]
    public void LeaveBalanceAdjustmentValidator_ValidInput_PassesValidation()
    {
        var validator = new LeaveBalanceAdjustmentValidator();
        var result    = validator.TestValidate(new LeaveBalanceAdjustmentDto { EmployeeId = "E001", LeaveTypeId = 1, Days = 5 });
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ─── LeaveCarryForwardDto ─────────────────────────────────────────────────────

    [Fact]
    public void LeaveCarryForwardValidator_ToYearNotGreater_FailsValidation()
    {
        var validator = new LeaveCarryForwardValidator();
        var result    = validator.TestValidate(new LeaveCarryForwardDto { FromYear = 2025, ToYear = 2024 });
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void LeaveCarryForwardValidator_SameYear_FailsValidation()
    {
        var validator = new LeaveCarryForwardValidator();
        var result    = validator.TestValidate(new LeaveCarryForwardDto { FromYear = 2025, ToYear = 2025 });
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void LeaveCarryForwardValidator_ValidInput_PassesValidation()
    {
        var validator = new LeaveCarryForwardValidator();
        var result    = validator.TestValidate(new LeaveCarryForwardDto { FromYear = 2025, ToYear = 2026 });
        result.ShouldNotHaveAnyValidationErrors();
    }
}

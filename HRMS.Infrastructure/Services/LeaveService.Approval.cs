// LeaveService.Approval.cs — idempotent leave approval (Phase 2 fix P2-LEAVE-IDEMPOTENT)

using HRMS.Application.Common;
using HRMS.Domain.Entities.Leave;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public partial class LeaveService
{
    /// <summary>
    /// Approves a pending leave request and deducts its balance once.
    /// LeaveRequest stores status as a string and has no LeaveBalance navigation
    /// property, so the balance is loaded separately.
    /// </summary>
    public async Task<ServiceResult> ApproveLeaveAsync(int leaveRequestId, int approvedByUserId)
    {
        var request = await _db.LeaveRequests
            .FirstOrDefaultAsync(r => r.Id == leaveRequestId);

        if (request is null)
            return ServiceResult.Fail($"Leave request {leaveRequestId} not found.");

        // Idempotency: an already-approved request must not deduct again.
        if (request.Status == "Approved")
            return ServiceResult.Ok("Leave request is already approved.");

        if (request.Status != "Pending")
            return ServiceResult.Fail(
                $"Cannot approve a leave request with status '{request.Status}'. " +
                "Only Pending requests can be approved.");

        var daysRequested =
            (request.EndDate.DayNumber - request.StartDate.DayNumber) + 1;
        var balance = await _db.LeaveBalances.FirstOrDefaultAsync(b =>
            b.EmployeeId == request.EmployeeId &&
            b.LeaveTypeId == request.LeaveTypeId &&
            b.Year == request.StartDate.Year);

        if (balance is null || balance.AvailableDays < daysRequested)
            return ServiceResult.Fail(
                $"Insufficient leave balance. Requested {daysRequested} day(s); " +
                $"available {balance?.AvailableDays ?? 0} day(s).");

        request.Status = "Approved";
        request.ApprovedByUserId = approvedByUserId;
        request.DecidedAt = DateTime.UtcNow;
        request.TotalDays = daysRequested;

        balance.AvailableDays -= daysRequested;
        balance.UsedDays += daysRequested;
        balance.PendingDays = Math.Max(0, balance.PendingDays - daysRequested);
        balance.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Leave request approved.");
    }

    /// <summary>Rejects a pending leave request without deducting leave balance.</summary>
    public async Task<ServiceResult> RejectLeaveAsync(
        int leaveRequestId, int rejectedByUserId, string reason)
    {
        var request = await _db.LeaveRequests
            .FirstOrDefaultAsync(r => r.Id == leaveRequestId);

        if (request is null)
            return ServiceResult.Fail($"Leave request {leaveRequestId} not found.");

        if (request.Status != "Pending")
            return ServiceResult.Fail(
                $"Cannot reject a leave request with status '{request.Status}'.");

        request.Status = "Rejected";
        request.ApprovedByUserId = rejectedByUserId;
        request.ApproverRemarks = reason;
        request.DecidedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Leave request rejected.");
    }
}
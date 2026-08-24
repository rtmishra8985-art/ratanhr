using HRMS.Application.DTOs.Sales;
using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Sales;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class SalesService : ISalesService
{
    private readonly ApplicationDbContext _db;

    public SalesService(ApplicationDbContext db) => _db = db;

    private static PagedResult<T> Page<T>(
        IEnumerable<T> source, int page, int pageSize, string? sortBy,
        string? sortDirection, string defaultSort,
        IReadOnlyDictionary<string, Func<T, object?>> sortColumns)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var items = source.ToList();
        var key = string.IsNullOrWhiteSpace(sortBy) ? defaultSort : sortBy.Trim();
        if (!sortColumns.TryGetValue(key, out var selector))
            selector = sortColumns[defaultSort];
        items = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            ? items.OrderBy(selector).ToList()
            : items.OrderByDescending(selector).ToList();
        return PagedResult<T>.Create(
            items.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            items.Count, page, pageSize);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string FormatTime(TimeSpan? t) =>
        t.HasValue ? $"{t.Value.Hours:D2}:{t.Value.Minutes:D2}" : string.Empty;

    private static TimeSpan? ParseTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return TimeSpan.TryParse(s, out var ts) ? ts : (TimeSpan?)null;
    }

    private async Task<string> NextLeadNoAsync(int companyId)
    {
        var count = await _db.SalesLeads.CountAsync(l => l.CompanyId == companyId) + 1;
        return $"LEAD-{count:D4}";
    }

    private async Task<string> NextCustomerCodeAsync(int companyId)
    {
        var count = await _db.SalesCustomers.CountAsync(c => c.CompanyId == companyId) + 1;
        return $"CUST-{count:D4}";
    }

    private async Task<string> NextQuotationNoAsync(int companyId)
    {
        var count = await _db.SalesQuotations.CountAsync(q => q.CompanyId == companyId) + 1;
        return $"QT-{count:D4}";
    }

    private LeadListDto MapLeadList(SalesLead l, string? ownerName = null) => new()
    {
        Id = l.Id, LeadNo = l.LeadNo, CompanyName = l.CompanyName,
        ContactPerson = l.ContactPerson, Mobile = l.Mobile, Email = l.Email,
        City = l.City, LeadSource = l.LeadSource, Industry = l.Industry,
        EmployeeOwnerId = l.EmployeeOwnerId, OwnerName = ownerName,
        Priority = l.Priority, Status = l.Status, ExpectedValue = l.ExpectedValue,
        NextFollowUpDate = l.NextFollowUpDate, CreatedAt = l.CreatedAt,
    };

    private LeadDetailDto MapLeadDetail(SalesLead l, string? ownerName = null) => new()
    {
        Id = l.Id, LeadNo = l.LeadNo, CompanyName = l.CompanyName,
        ContactPerson = l.ContactPerson, Mobile = l.Mobile, Email = l.Email,
        City = l.City, State = l.State, Country = l.Country, Address = l.Address,
        LeadSource = l.LeadSource, Industry = l.Industry,
        EmployeeOwnerId = l.EmployeeOwnerId, OwnerName = ownerName,
        Priority = l.Priority, Status = l.Status, Remarks = l.Remarks,
        ExpectedValue = l.ExpectedValue, NextFollowUpDate = l.NextFollowUpDate,
        CreatedAt = l.CreatedAt, UpdatedAt = l.UpdatedAt,
    };

    // ── Dashboard ─────────────────────────────────────────────────────────

    public async Task<object> GetSalesDashboardAsync(int? companyId)
    {
        var today = DateTime.UtcNow.Date;

        var todaysLeads   = await _db.SalesLeads.CountAsync(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted && l.CreatedAt.Date == today);
        var todaysFollowUps = await _db.SalesFollowUps.CountAsync(f => (!companyId.HasValue || f.CompanyId == companyId.Value) && !f.IsDeleted && f.ReminderDate.Date == today);
        var meetings      = await _db.SalesMeetings.CountAsync(m => (!companyId.HasValue || m.CompanyId == companyId.Value) && !m.IsDeleted && m.MeetingDate.Date >= today);
        var closedDeals   = await _db.SalesLeads.CountAsync(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted && l.Status == "Won");
        var pendingTasks  = await _db.SalesTasks.CountAsync(t => (!companyId.HasValue || t.CompanyId == companyId.Value) && !t.IsDeleted && t.Status == "Pending");

        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthlyRevenue = await _db.SalesQuotations
            .Where(q => (!companyId.HasValue || q.CompanyId == companyId.Value) && !q.IsDeleted && q.Status == "Accepted" && q.CreatedAt >= monthStart)
            .SumAsync(q => (decimal?)q.TotalAmount) ?? 0m;

        var pipelineValue = await _db.SalesLeads
            .Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted && l.Status != "Won" && l.Status != "Lost")
            .SumAsync(l => l.ExpectedValue) ?? 0m;

        var totalLeads = await _db.SalesLeads.CountAsync(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted);
        var wonLeads   = closedDeals;
        var conversionPct = totalLeads > 0 ? Math.Round((double)wonLeads / totalLeads * 100, 1) : 0.0;

        var leadsByStatus = await _db.SalesLeads
            .Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted)
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var funnelOrder = new[] { "New", "Contacted", "Qualified", "Proposal Sent", "Negotiation", "Won", "Lost" };
        var funnel = funnelOrder.Select(s => new {
            Status = s,
            Count  = leadsByStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0
        }).ToList();

        var monthly = await _db.SalesLeads
            .Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted && l.Status == "Won"
                     && l.UpdatedAt >= DateTime.UtcNow.AddMonths(-6))
            .GroupBy(l => new { l.UpdatedAt.Year, l.UpdatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        return new
        {
            todaysLeads, todaysFollowUps, meetings, closedDeals, pendingTasks,
            monthlyRevenue, pipelineValue, conversionPct,
            leadsByStatus, funnel, monthlyDeals = monthly,
        };
    }

    // ── Leads ─────────────────────────────────────────────────────────────

    public async Task<(List<LeadListDto> Items, int Total)> ListLeadsAsync(
        int? companyId, int page, int pageSize, string? status = null, string? search = null)
    {
        var q = _db.SalesLeads.Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(l => l.CompanyName.Contains(search) || l.ContactPerson.Contains(search) || l.Mobile.Contains(search));

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.CreatedAt)
                           .Skip((page - 1) * pageSize).Take(pageSize)
                           .ToListAsync();

        // Resolve owner names
        var ownerIds = items.Where(l => l.EmployeeOwnerId != null).Select(l => l.EmployeeOwnerId!).Distinct().ToList();
        var owners   = await _db.Employees
            .Where(e => ownerIds.Contains(e.EmployeeCode))
            .ToDictionaryAsync(e => e.EmployeeCode, e => e.FullName);

        var dtos = items.Select(l => MapLeadList(l,
            l.EmployeeOwnerId != null && owners.TryGetValue(l.EmployeeOwnerId, out var n) ? n : null)).ToList();

        return (dtos, total);
    }

    public async Task<LeadDetailDto?> GetLeadAsync(int id, int? companyId)
    {
        var l = await _db.SalesLeads.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value) && !x.IsDeleted);
        if (l is null) return null;
        string? ownerName = null;
        if (l.EmployeeOwnerId != null)
        {
            var emp = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == l.EmployeeOwnerId);
            ownerName = emp?.FullName;
        }
        return MapLeadDetail(l, ownerName);
    }

    public async Task<LeadListDto> CreateLeadAsync(CreateLeadDto dto, int companyId, int userId)
    {
        // Duplicate mobile check within company
        var dup = await _db.SalesLeads.AnyAsync(l => l.CompanyId == companyId && !l.IsDeleted && l.Mobile == dto.Mobile);
        if (dup) throw new InvalidOperationException("A lead with this mobile number already exists.");

        var lead = new SalesLead
        {
            CompanyId = companyId, LeadNo = await NextLeadNoAsync(companyId),
            CompanyName = dto.CompanyName, ContactPerson = dto.ContactPerson,
            Mobile = dto.Mobile, Email = dto.Email, City = dto.City, State = dto.State,
            Country = dto.Country, Address = dto.Address, LeadSource = dto.LeadSource,
            Industry = dto.Industry, EmployeeOwnerId = dto.EmployeeOwnerId,
            Priority = dto.Priority, Status = dto.Status, Remarks = dto.Remarks,
            ExpectedValue = dto.ExpectedValue, NextFollowUpDate = dto.NextFollowUpDate,
            CreatedByUserId = userId,
        };
        _db.SalesLeads.Add(lead);
        await _db.SaveChangesAsync();
        return (await ListLeadsAsync(companyId, 1, 1)).Items.FirstOrDefault() ?? MapLeadList(lead);
    }

    public async Task<LeadListDto> UpdateLeadAsync(int id, UpdateLeadDto dto, int companyId)
    {
        var lead = await _db.SalesLeads.FirstOrDefaultAsync(l => l.Id == id && l.CompanyId == companyId && !l.IsDeleted)
            ?? throw new KeyNotFoundException("Lead not found.");

        lead.CompanyName = dto.CompanyName; lead.ContactPerson = dto.ContactPerson;
        lead.Mobile = dto.Mobile; lead.Email = dto.Email; lead.City = dto.City;
        lead.State = dto.State; lead.Country = dto.Country; lead.Address = dto.Address;
        lead.LeadSource = dto.LeadSource; lead.Industry = dto.Industry;
        lead.EmployeeOwnerId = dto.EmployeeOwnerId; lead.Priority = dto.Priority;
        lead.Status = dto.Status; lead.Remarks = dto.Remarks;
        lead.ExpectedValue = dto.ExpectedValue; lead.NextFollowUpDate = dto.NextFollowUpDate;
        lead.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapLeadList(lead);
    }

    public async Task<bool> UpdateLeadStatusAsync(int id, string status, int companyId)
    {
        var lead = await _db.SalesLeads.FirstOrDefaultAsync(l => l.Id == id && l.CompanyId == companyId && !l.IsDeleted);
        if (lead is null) return false;
        lead.Status = status; lead.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteLeadAsync(int id, int companyId)
    {
        var lead = await _db.SalesLeads.FirstOrDefaultAsync(l => l.Id == id && l.CompanyId == companyId && !l.IsDeleted);
        if (lead is null) return false;
        lead.IsDeleted = true; lead.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    // ── Customers ─────────────────────────────────────────────────────────

    private async Task<Dictionary<string, string>> GetEmpNamesAsync(IEnumerable<string> ids)
        => await _db.Employees
            .Where(e => ids.Contains(e.EmployeeCode))
            .ToDictionaryAsync(e => e.EmployeeCode, e => e.FullName);

    private CustomerListDto MapCustomerList(SalesCustomer c, string? spName = null) => new()
    {
        Id = c.Id, CustomerCode = c.CustomerCode, CompanyName = c.CompanyName,
        ContactPerson = c.ContactPerson, ContactPhone = c.ContactPhone, ContactEmail = c.ContactEmail,
        AssignedSalesPersonId = c.AssignedSalesPersonId, SalesPersonName = spName,
        SalesLeadId = c.SalesLeadId, IsActive = c.IsActive, CreatedAt = c.CreatedAt,
    };

    public async Task<(List<CustomerListDto> Items, int Total)> ListCustomersAsync(
        int? companyId, int page, int pageSize, string? search = null)
    {
        var q = _db.SalesCustomers.Where(c => (!companyId.HasValue || c.CompanyId == companyId.Value) && !c.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.CompanyName.Contains(search) || c.ContactPerson.Contains(search) || c.ContactPhone.Contains(search));

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(c => c.CreatedAt)
                           .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var spIds = items.Where(c => c.AssignedSalesPersonId != null).Select(c => c.AssignedSalesPersonId!).Distinct().ToList();
        var sps   = spIds.Any() ? await GetEmpNamesAsync(spIds) : new();
        var dtos  = items.Select(c => MapCustomerList(c,
            c.AssignedSalesPersonId != null && sps.TryGetValue(c.AssignedSalesPersonId, out var n) ? n : null)).ToList();
        return (dtos, total);
    }

    public async Task<CustomerDetailDto?> GetCustomerAsync(int id, int? companyId)
    {
        var c = await _db.SalesCustomers.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value) && !x.IsDeleted);
        if (c is null) return null;
        string? spName = null;
        if (c.AssignedSalesPersonId != null)
        {
            var emp = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == c.AssignedSalesPersonId);
            spName = emp?.FullName;
        }
        return new CustomerDetailDto
        {
            Id = c.Id, CustomerCode = c.CustomerCode, CompanyName = c.CompanyName,
            Gst = c.Gst, Pan = c.Pan, BillingAddress = c.BillingAddress, ShippingAddress = c.ShippingAddress,
            ContactPerson = c.ContactPerson, ContactPhone = c.ContactPhone, ContactEmail = c.ContactEmail,
            AssignedSalesPersonId = c.AssignedSalesPersonId, SalesPersonName = spName,
            SalesLeadId = c.SalesLeadId, IsActive = c.IsActive,
            CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt,
        };
    }

    private SalesCustomer BuildCustomer(CreateCustomerDto dto, int companyId, int userId, string code) => new()
    {
        CompanyId = companyId, CustomerCode = code, Gst = dto.Gst, Pan = dto.Pan,
        CompanyName = dto.CompanyName, BillingAddress = dto.BillingAddress, ShippingAddress = dto.ShippingAddress,
        ContactPerson = dto.ContactPerson, ContactPhone = dto.ContactPhone, ContactEmail = dto.ContactEmail,
        AssignedSalesPersonId = dto.AssignedSalesPersonId, SalesLeadId = dto.SalesLeadId,
        IsActive = dto.IsActive, CreatedByUserId = userId,
    };

    public async Task<CustomerListDto> CreateCustomerAsync(CreateCustomerDto dto, int companyId, int userId)
    {
        var c = BuildCustomer(dto, companyId, userId, await NextCustomerCodeAsync(companyId));
        _db.SalesCustomers.Add(c); await _db.SaveChangesAsync();
        return MapCustomerList(c);
    }

    public async Task<CustomerListDto> ConvertLeadToCustomerAsync(int leadId, CreateCustomerDto dto, int companyId, int userId)
    {
        var lead = await _db.SalesLeads.FirstOrDefaultAsync(l => l.Id == leadId && l.CompanyId == companyId && !l.IsDeleted)
            ?? throw new KeyNotFoundException("Lead not found.");
        dto.SalesLeadId = leadId;
        var c = BuildCustomer(dto, companyId, userId, await NextCustomerCodeAsync(companyId));
        _db.SalesCustomers.Add(c);
        // Move lead status to Won
        lead.Status = "Won"; lead.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapCustomerList(c);
    }

    public async Task<CustomerListDto> UpdateCustomerAsync(int id, UpdateCustomerDto dto, int companyId)
    {
        var c = await _db.SalesCustomers.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted)
            ?? throw new KeyNotFoundException("Customer not found.");
        c.Gst = dto.Gst; c.Pan = dto.Pan; c.CompanyName = dto.CompanyName;
        c.BillingAddress = dto.BillingAddress; c.ShippingAddress = dto.ShippingAddress;
        c.ContactPerson = dto.ContactPerson; c.ContactPhone = dto.ContactPhone; c.ContactEmail = dto.ContactEmail;
        c.AssignedSalesPersonId = dto.AssignedSalesPersonId; c.IsActive = dto.IsActive;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapCustomerList(c);
    }

    public async Task<bool> DeleteCustomerAsync(int id, int companyId)
    {
        var c = await _db.SalesCustomers.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (c is null) return false;
        c.IsDeleted = true; c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    // ── Follow-Ups ────────────────────────────────────────────────────────

    public async Task<List<FollowUpListDto>> ListFollowUpsAsync(int? companyId, int? leadId = null, string? status = null)
    {
        var q = _db.SalesFollowUps.Where(f => (!companyId.HasValue || f.CompanyId == companyId.Value) && !f.IsDeleted);
        if (leadId.HasValue) q = q.Where(f => f.SalesLeadId == leadId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(f => f.Status == status);

        var items = await q.OrderByDescending(f => f.ReminderDate).ToListAsync();
        var leadIds = items.Select(f => f.SalesLeadId).Distinct().ToList();
        var leads   = await _db.SalesLeads.Where(l => leadIds.Contains(l.Id))
                               .ToDictionaryAsync(l => l.Id, l => l.CompanyName);

        return items.Select(f => new FollowUpListDto
        {
            Id = f.Id, SalesLeadId = f.SalesLeadId,
            LeadCompanyName = leads.TryGetValue(f.SalesLeadId, out var cn) ? cn : null,
            Notes = f.Notes, ReminderDate = f.ReminderDate,
            ReminderTime = FormatTime(f.ReminderTime),
            Mode = f.Mode, Status = f.Status, CreatedAt = f.CreatedAt,
        }).ToList();
    }

    public async Task<PagedResult<FollowUpListDto>> ListFollowUpsPagedAsync(
        int? companyId, int? leadId = null, string? status = null,
        int page = 1, int pageSize = 25, string? search = null,
        string? sortBy = null, string? sortDirection = "desc")
    {
        var items = await ListFollowUpsAsync(companyId, leadId, status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            items = items.Where(x =>
                x.Notes.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.LeadCompanyName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.Status.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return Page(items, page, pageSize, sortBy, sortDirection, "ReminderDate",
            new Dictionary<string, Func<FollowUpListDto, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["ReminderDate"] = x => x.ReminderDate,
                ["CreatedAt"] = x => x.CreatedAt,
                ["Status"] = x => x.Status
            });
    }

    public async Task<FollowUpListDto> CreateFollowUpAsync(CreateFollowUpDto dto, int companyId, int userId)
    {
        var fu = new SalesFollowUp
        {
            CompanyId = companyId, SalesLeadId = dto.SalesLeadId, Notes = dto.Notes,
            ReminderDate = dto.ReminderDate, ReminderTime = ParseTime(dto.ReminderTime),
            Mode = dto.Mode, Status = dto.Status, CreatedByUserId = userId,
        };
        _db.SalesFollowUps.Add(fu);
        // Update lead's next follow-up date
        var lead = await _db.SalesLeads.FirstOrDefaultAsync(l => l.Id == dto.SalesLeadId && l.CompanyId == companyId && !l.IsDeleted);
        if (lead != null && dto.Status == "Pending") {
            if (lead.NextFollowUpDate == null || dto.ReminderDate < lead.NextFollowUpDate)
                lead.NextFollowUpDate = dto.ReminderDate;
        }
        await _db.SaveChangesAsync();
        return (await ListFollowUpsAsync(companyId, dto.SalesLeadId)).FirstOrDefault(x => x.Id == fu.Id) ?? new();
    }

    public async Task<FollowUpListDto> UpdateFollowUpAsync(int id, UpdateFollowUpDto dto, int companyId)
    {
        var fu = await _db.SalesFollowUps.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted)
            ?? throw new KeyNotFoundException("Follow-up not found.");
        fu.Notes = dto.Notes; fu.ReminderDate = dto.ReminderDate;
        fu.ReminderTime = ParseTime(dto.ReminderTime); fu.Mode = dto.Mode; fu.Status = dto.Status;
        fu.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await ListFollowUpsAsync(companyId, fu.SalesLeadId)).FirstOrDefault(x => x.Id == fu.Id) ?? new();
    }

    public async Task<bool> DeleteFollowUpAsync(int id, int companyId)
    {
        var fu = await _db.SalesFollowUps.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (fu is null) return false;
        fu.IsDeleted = true; fu.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    // ── Meetings ──────────────────────────────────────────────────────────

    private async Task<(Dictionary<int, string> Leads, Dictionary<int, string> Custs)> GetMeetingRefNamesAsync(
        IEnumerable<SalesMeeting> items)
    {
        var lids = items.Where(m => m.SalesLeadId.HasValue).Select(m => m.SalesLeadId!.Value).Distinct().ToList();
        var cids = items.Where(m => m.SalesCustomerId.HasValue).Select(m => m.SalesCustomerId!.Value).Distinct().ToList();
        var ld = lids.Any() ? await _db.SalesLeads.Where(l => lids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.CompanyName) : new();
        var cd = cids.Any() ? await _db.SalesCustomers.Where(c => cids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.CompanyName) : new();
        return (ld, cd);
    }

    public async Task<List<MeetingListDto>> ListMeetingsAsync(int? companyId, int? leadId = null, int? customerId = null)
    {
        var q = _db.SalesMeetings.Where(m => (!companyId.HasValue || m.CompanyId == companyId.Value) && !m.IsDeleted);
        if (leadId.HasValue)     q = q.Where(m => m.SalesLeadId == leadId.Value);
        if (customerId.HasValue) q = q.Where(m => m.SalesCustomerId == customerId.Value);
        var items = await q.OrderByDescending(m => m.MeetingDate).ToListAsync();
        var (ld, cd) = await GetMeetingRefNamesAsync(items);
        return items.Select(m => new MeetingListDto
        {
            Id = m.Id, SalesLeadId = m.SalesLeadId,
            LeadCompanyName = m.SalesLeadId.HasValue && ld.TryGetValue(m.SalesLeadId.Value, out var ln) ? ln : null,
            SalesCustomerId = m.SalesCustomerId,
            CustomerCompanyName = m.SalesCustomerId.HasValue && cd.TryGetValue(m.SalesCustomerId.Value, out var cn) ? cn : null,
            Title = m.Title, MeetingDate = m.MeetingDate, MeetingTime = FormatTime(m.MeetingTime),
            Location = m.Location, MeetingType = m.MeetingType, Status = m.Status, CreatedAt = m.CreatedAt,
        }).ToList();
    }

    public async Task<PagedResult<MeetingListDto>> ListMeetingsPagedAsync(
        int? companyId, int? leadId = null, int? customerId = null,
        int page = 1, int pageSize = 25, string? search = null,
        string? sortBy = null, string? sortDirection = "desc")
    {
        var items = await ListMeetingsAsync(companyId, leadId, customerId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            items = items.Where(x =>
                x.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Location.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.LeadCompanyName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.CustomerCompanyName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.Status.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return Page(items, page, pageSize, sortBy, sortDirection, "MeetingDate",
            new Dictionary<string, Func<MeetingListDto, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["MeetingDate"] = x => x.MeetingDate,
                ["CreatedAt"] = x => x.CreatedAt,
                ["Status"] = x => x.Status
            });
    }

    public async Task<MeetingDetailDto?> GetMeetingAsync(int id, int? companyId)
    {
        var m = await _db.SalesMeetings.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value) && !x.IsDeleted);
        if (m is null) return null;
        string? ln = m.SalesLeadId.HasValue
            ? (await _db.SalesLeads.FirstOrDefaultAsync(l => l.Id == m.SalesLeadId.Value))?.CompanyName : null;
        string? cn = m.SalesCustomerId.HasValue
            ? (await _db.SalesCustomers.FirstOrDefaultAsync(c => c.Id == m.SalesCustomerId.Value))?.CompanyName : null;
        return new MeetingDetailDto
        {
            Id = m.Id, SalesLeadId = m.SalesLeadId, LeadCompanyName = ln,
            SalesCustomerId = m.SalesCustomerId, CustomerCompanyName = cn,
            Title = m.Title, MeetingDate = m.MeetingDate, MeetingTime = FormatTime(m.MeetingTime),
            Location = m.Location, GoogleMapUrl = m.GoogleMapUrl, MeetingType = m.MeetingType,
            Outcome = m.Outcome, Notes = m.Notes, Status = m.Status,
            CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt,
        };
    }

    public async Task<MeetingListDto> CreateMeetingAsync(CreateMeetingDto dto, int companyId, int userId)
    {
        var m = new SalesMeeting
        {
            CompanyId = companyId, SalesLeadId = dto.SalesLeadId, SalesCustomerId = dto.SalesCustomerId,
            Title = dto.Title, MeetingDate = dto.MeetingDate,
            MeetingTime = ParseTime(dto.MeetingTime) ?? TimeSpan.Zero,
            Location = dto.Location, GoogleMapUrl = dto.GoogleMapUrl,
            MeetingType = dto.MeetingType, Outcome = dto.Outcome, Notes = dto.Notes,
            Status = dto.Status, CreatedByUserId = userId,
        };
        _db.SalesMeetings.Add(m); await _db.SaveChangesAsync();
        return (await ListMeetingsAsync(companyId)).First(x => x.Id == m.Id);
    }

    public async Task<MeetingListDto> UpdateMeetingAsync(int id, UpdateMeetingDto dto, int companyId)
    {
        var m = await _db.SalesMeetings.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted)
            ?? throw new KeyNotFoundException("Meeting not found.");
        m.SalesLeadId = dto.SalesLeadId; m.SalesCustomerId = dto.SalesCustomerId;
        m.Title = dto.Title; m.MeetingDate = dto.MeetingDate;
        m.MeetingTime = ParseTime(dto.MeetingTime) ?? m.MeetingTime;
        m.Location = dto.Location; m.GoogleMapUrl = dto.GoogleMapUrl;
        m.MeetingType = dto.MeetingType; m.Outcome = dto.Outcome; m.Notes = dto.Notes;
        m.Status = dto.Status; m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await ListMeetingsAsync(companyId)).First(x => x.Id == m.Id);
    }

    public async Task<bool> DeleteMeetingAsync(int id, int companyId)
    {
        var m = await _db.SalesMeetings.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (m is null) return false;
        m.IsDeleted = true; m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    // ── Field Visits ──────────────────────────────────────────────────────

    public async Task<List<VisitListDto>> ListVisitsAsync(int? companyId, int? leadId = null, int? customerId = null)
    {
        var q = _db.SalesVisits.Where(v => (!companyId.HasValue || v.CompanyId == companyId.Value) && !v.IsDeleted);
        if (leadId.HasValue)     q = q.Where(v => v.SalesLeadId == leadId.Value);
        if (customerId.HasValue) q = q.Where(v => v.SalesCustomerId == customerId.Value);
        var items = await q.OrderByDescending(v => v.CheckInTime).ToListAsync();

        var lids = items.Where(v => v.SalesLeadId.HasValue).Select(v => v.SalesLeadId!.Value).Distinct().ToList();
        var cids = items.Where(v => v.SalesCustomerId.HasValue).Select(v => v.SalesCustomerId!.Value).Distinct().ToList();
        var eids = items.Select(v => v.VisitedEmployeeId).Distinct().ToList();

        var ld = lids.Any() ? await _db.SalesLeads.Where(l => lids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.CompanyName) : new();
        var cd = cids.Any() ? await _db.SalesCustomers.Where(c => cids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.CompanyName) : new();
        var ed = eids.Any() ? await _db.Employees.Where(e => eids.Contains(e.EmployeeCode)).ToDictionaryAsync(e => e.EmployeeCode, e => e.FullName) : new();

        return items.Select(v => new VisitListDto
        {
            Id = v.Id, SalesLeadId = v.SalesLeadId,
            LeadCompanyName     = v.SalesLeadId.HasValue     && ld.TryGetValue(v.SalesLeadId.Value, out var ln) ? ln : null,
            SalesCustomerId     = v.SalesCustomerId,
            CustomerCompanyName = v.SalesCustomerId.HasValue && cd.TryGetValue(v.SalesCustomerId.Value, out var cn) ? cn : null,
            VisitedEmployeeId   = v.VisitedEmployeeId,
            EmployeeName = ed.TryGetValue(v.VisitedEmployeeId, out var en) ? en : null,
            CheckInAddress = v.CheckInAddress, CheckInTime = v.CheckInTime,
            CheckOutTime = v.CheckOutTime, DurationMinutes = v.DurationMinutes,
            DistanceKm = v.DistanceKm, Status = v.Status, CreatedAt = v.CreatedAt,
        }).ToList();
    }

    public async Task<PagedResult<VisitListDto>> ListVisitsPagedAsync(
        int? companyId, int? leadId = null, int? customerId = null,
        int page = 1, int pageSize = 25, string? search = null,
        string? sortBy = null, string? sortDirection = "desc")
    {
        var items = await ListVisitsAsync(companyId, leadId, customerId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            items = items.Where(x =>
                x.CheckInAddress.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.EmployeeName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.LeadCompanyName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.CustomerCompanyName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.Status.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return Page(items, page, pageSize, sortBy, sortDirection, "CheckInTime",
            new Dictionary<string, Func<VisitListDto, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["CheckInTime"] = x => x.CheckInTime,
                ["CreatedAt"] = x => x.CreatedAt,
                ["Status"] = x => x.Status
            });
    }

    public async Task<VisitListDto> CheckInAsync(CheckInDto dto, int companyId, int userId)
    {
        var v = new SalesVisit
        {
            CompanyId = companyId, SalesLeadId = dto.SalesLeadId, SalesCustomerId = dto.SalesCustomerId,
            VisitedEmployeeId = dto.VisitedEmployeeId, CheckInLatitude = dto.Latitude,
            CheckInLongitude = dto.Longitude, CheckInAddress = dto.Address,
            CheckInPhotoPath = dto.PhotoPath, CheckInTime = DateTime.UtcNow,
            Status = "CheckedIn", CreatedByUserId = userId,
        };
        _db.SalesVisits.Add(v); await _db.SaveChangesAsync();
        return (await ListVisitsAsync(companyId)).First(x => x.Id == v.Id);
    }

    public async Task<bool> CheckOutAsync(int id, CheckOutDto dto, int companyId)
    {
        var v = await _db.SalesVisits.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (v is null) return false;
        v.CheckOutTime = DateTime.UtcNow; v.DistanceKm = dto.DistanceKm; v.Notes = dto.Notes;
        if (v.CheckInTime.HasValue)
            v.DurationMinutes = (int)(v.CheckOutTime.Value - v.CheckInTime.Value).TotalMinutes;
        v.Status = "CheckedOut"; v.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteVisitAsync(int id, int companyId)
    {
        var v = await _db.SalesVisits.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (v is null) return false;
        v.IsDeleted = true; v.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    // ── Tasks ─────────────────────────────────────────────────────────────

    public async Task<List<SalesTaskListDto>> ListTasksAsync(int? companyId, int? leadId = null, int? customerId = null, string? status = null)
    {
        var q = _db.SalesTasks.Where(t => (!companyId.HasValue || t.CompanyId == companyId.Value) && !t.IsDeleted);
        if (leadId.HasValue)     q = q.Where(t => t.SalesLeadId == leadId.Value);
        if (customerId.HasValue) q = q.Where(t => t.SalesCustomerId == customerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(t => t.Status == status);
        var items = await q.OrderByDescending(t => t.CreatedAt).ToListAsync();

        var lids = items.Where(t => t.SalesLeadId.HasValue).Select(t => t.SalesLeadId!.Value).Distinct().ToList();
        var cids = items.Where(t => t.SalesCustomerId.HasValue).Select(t => t.SalesCustomerId!.Value).Distinct().ToList();
        var eids = items.Where(t => t.AssignedToEmployeeId != null).Select(t => t.AssignedToEmployeeId!).Distinct().ToList();

        var ld = lids.Any() ? await _db.SalesLeads.Where(l => lids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.CompanyName) : new();
        var cd = cids.Any() ? await _db.SalesCustomers.Where(c => cids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.CompanyName) : new();
        var ed = eids.Any() ? await _db.Employees.Where(e => eids.Contains(e.EmployeeCode)).ToDictionaryAsync(e => e.EmployeeCode, e => e.FullName) : new();

        return items.Select(t => new SalesTaskListDto
        {
            Id = t.Id, SalesLeadId = t.SalesLeadId,
            LeadCompanyName     = t.SalesLeadId.HasValue     && ld.TryGetValue(t.SalesLeadId.Value, out var ln) ? ln : null,
            SalesCustomerId     = t.SalesCustomerId,
            CustomerCompanyName = t.SalesCustomerId.HasValue && cd.TryGetValue(t.SalesCustomerId.Value, out var cn) ? cn : null,
            Title = t.Title, AssignedToEmployeeId = t.AssignedToEmployeeId,
            AssigneeName = t.AssignedToEmployeeId != null && ed.TryGetValue(t.AssignedToEmployeeId, out var en) ? en : null,
            Priority = t.Priority, Status = t.Status, Deadline = t.Deadline, ReminderDate = t.ReminderDate,
            CreatedAt = t.CreatedAt,
        }).ToList();
    }

    public async Task<PagedResult<SalesTaskListDto>> ListTasksPagedAsync(
        int? companyId, int? leadId = null, int? customerId = null, string? status = null,
        int page = 1, int pageSize = 25, string? search = null,
        string? sortBy = null, string? sortDirection = "desc")
    {
        var items = await ListTasksAsync(companyId, leadId, customerId, status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            items = items.Where(x =>
                x.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.AssigneeName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.LeadCompanyName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.CustomerCompanyName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.Priority.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Status.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return Page(items, page, pageSize, sortBy, sortDirection, "CreatedAt",
            new Dictionary<string, Func<SalesTaskListDto, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["CreatedAt"] = x => x.CreatedAt,
                ["Deadline"] = x => x.Deadline,
                ["Priority"] = x => x.Priority,
                ["Status"] = x => x.Status
            });
    }

    public async Task<SalesTaskListDto> CreateTaskAsync(CreateSalesTaskDto dto, int companyId, int userId)
    {
        var t = new SalesTask
        {
            CompanyId = companyId, SalesLeadId = dto.SalesLeadId, SalesCustomerId = dto.SalesCustomerId,
            Title = dto.Title, Description = dto.Description,
            AssignedToEmployeeId = dto.AssignedToEmployeeId,
            Priority = dto.Priority, Status = dto.Status, Deadline = dto.Deadline,
            ReminderDate = dto.ReminderDate, CreatedByUserId = userId,
        };
        _db.SalesTasks.Add(t); await _db.SaveChangesAsync();
        return (await ListTasksAsync(companyId)).First(x => x.Id == t.Id);
    }

    public async Task<SalesTaskListDto> UpdateTaskAsync(int id, UpdateSalesTaskDto dto, int companyId)
    {
        var t = await _db.SalesTasks.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted)
            ?? throw new KeyNotFoundException("Task not found.");
        t.SalesLeadId = dto.SalesLeadId; t.SalesCustomerId = dto.SalesCustomerId;
        t.Title = dto.Title; t.Description = dto.Description;
        t.AssignedToEmployeeId = dto.AssignedToEmployeeId; t.Priority = dto.Priority;
        t.Status = dto.Status; t.Deadline = dto.Deadline; t.ReminderDate = dto.ReminderDate;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await ListTasksAsync(companyId)).First(x => x.Id == t.Id);
    }

    public async Task<bool> UpdateTaskStatusAsync(int id, string status, int companyId)
    {
        var t = await _db.SalesTasks.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (t is null) return false;
        t.Status = status; t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteTaskAsync(int id, int companyId)
    {
        var t = await _db.SalesTasks.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (t is null) return false;
        t.IsDeleted = true; t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    // ── Quotations ────────────────────────────────────────────────────────

    private QuotationListDto MapQuotation(SalesQuotation q, string? ln = null, string? cn = null) => new()
    {
        Id = q.Id, QuotationNumber = q.QuotationNumber, SalesLeadId = q.SalesLeadId,
        LeadCompanyName = ln, SalesCustomerId = q.SalesCustomerId, CustomerCompanyName = cn,
        Amount = q.Amount, Tax = q.Tax, Discount = q.Discount, TotalAmount = q.TotalAmount,
        Status = q.Status, ValidUntil = q.ValidUntil, CreatedAt = q.CreatedAt,
    };

    public async Task<List<QuotationListDto>> ListQuotationsAsync(int? companyId, int? leadId = null, int? customerId = null)
    {
        var q = _db.SalesQuotations.Where(x => (!companyId.HasValue || x.CompanyId == companyId.Value) && !x.IsDeleted);
        if (leadId.HasValue)     q = q.Where(x => x.SalesLeadId == leadId.Value);
        if (customerId.HasValue) q = q.Where(x => x.SalesCustomerId == customerId.Value);
        var items = await q.OrderByDescending(x => x.CreatedAt).ToListAsync();

        var lids = items.Where(x => x.SalesLeadId.HasValue).Select(x => x.SalesLeadId!.Value).Distinct().ToList();
        var cids = items.Where(x => x.SalesCustomerId.HasValue).Select(x => x.SalesCustomerId!.Value).Distinct().ToList();
        var ld = lids.Any() ? await _db.SalesLeads.Where(l => lids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.CompanyName) : new();
        var cd = cids.Any() ? await _db.SalesCustomers.Where(c => cids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.CompanyName) : new();

        return items.Select(x => MapQuotation(x,
            x.SalesLeadId.HasValue     && ld.TryGetValue(x.SalesLeadId.Value, out var ln) ? ln : null,
            x.SalesCustomerId.HasValue && cd.TryGetValue(x.SalesCustomerId.Value, out var cn) ? cn : null)).ToList();
    }

    public async Task<QuotationListDto?> GetQuotationAsync(int id, int? companyId)
        => (await ListQuotationsAsync(companyId)).FirstOrDefault(x => x.Id == id);

    public async Task<QuotationListDto> CreateQuotationAsync(CreateQuotationDto dto, int companyId, int userId)
    {
        var total = dto.Amount + dto.Tax - dto.Discount;
        var qt = new SalesQuotation
        {
            CompanyId = companyId, QuotationNumber = await NextQuotationNoAsync(companyId),
            SalesLeadId = dto.SalesLeadId, SalesCustomerId = dto.SalesCustomerId,
            Amount = dto.Amount, Tax = dto.Tax, Discount = dto.Discount, TotalAmount = total,
            Status = dto.Status, ValidUntil = dto.ValidUntil, Notes = dto.Notes,
            CreatedByUserId = userId,
        };
        _db.SalesQuotations.Add(qt); await _db.SaveChangesAsync();
        return (await ListQuotationsAsync(companyId)).First(x => x.Id == qt.Id);
    }

    public async Task<QuotationListDto> UpdateQuotationAsync(int id, UpdateQuotationDto dto, int companyId)
    {
        var qt = await _db.SalesQuotations.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted)
            ?? throw new KeyNotFoundException("Quotation not found.");
        qt.SalesLeadId = dto.SalesLeadId; qt.SalesCustomerId = dto.SalesCustomerId;
        qt.Amount = dto.Amount; qt.Tax = dto.Tax; qt.Discount = dto.Discount;
        qt.TotalAmount = dto.Amount + dto.Tax - dto.Discount;
        qt.Status = dto.Status; qt.ValidUntil = dto.ValidUntil; qt.Notes = dto.Notes;
        qt.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await ListQuotationsAsync(companyId)).First(x => x.Id == qt.Id);
    }

    public async Task<bool> UpdateQuotationStatusAsync(int id, string status, int companyId)
    {
        var qt = await _db.SalesQuotations.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (qt is null) return false;
        qt.Status = status; qt.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteQuotationAsync(int id, int companyId)
    {
        var qt = await _db.SalesQuotations.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (qt is null) return false;
        qt.IsDeleted = true; qt.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    // ── Reports ───────────────────────────────────────────────────────────

    public async Task<object> GetLeadReportAsync(int? companyId, DateTime? from, DateTime? to)
    {
        var q = _db.SalesLeads.Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted);
        if (from.HasValue) q = q.Where(l => l.CreatedAt >= from.Value);
        if (to.HasValue)   q = q.Where(l => l.CreatedAt <= to.Value);
        var byStatus = await q.GroupBy(l => l.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
        var bySource = await q.GroupBy(l => l.LeadSource).Select(g => new { Source = g.Key, Count = g.Count() }).ToListAsync();
        var total    = await q.CountAsync();
        return new { total, byStatus, bySource };
    }

    public async Task<object> GetConversionReportAsync(int? companyId, DateTime? from, DateTime? to)
    {
        var q = _db.SalesLeads.Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted);
        if (from.HasValue) q = q.Where(l => l.CreatedAt >= from.Value);
        if (to.HasValue)   q = q.Where(l => l.CreatedAt <= to.Value);
        var total   = await q.CountAsync();
        var won     = await q.CountAsync(l => l.Status == "Won");
        var lost    = await q.CountAsync(l => l.Status == "Lost");
        var convPct = total > 0 ? Math.Round((double)won / total * 100, 2) : 0.0;
        return new { total, won, lost, conversionPercent = convPct };
    }

    public async Task<object> GetPerformanceReportAsync(int? companyId, DateTime? from, DateTime? to)
    {
        var q = _db.SalesLeads.Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted);
        if (from.HasValue) q = q.Where(l => l.CreatedAt >= from.Value);
        if (to.HasValue)   q = q.Where(l => l.CreatedAt <= to.Value);
        var byOwner = await q
            .GroupBy(l => l.EmployeeOwnerId)
            .Select(g => new { EmployeeId = g.Key, Total = g.Count(), Won = g.Count(x => x.Status == "Won") })
            .ToListAsync();
        return new { byOwner };
    }

    public async Task<object> GetVisitReportAsync(int? companyId, DateTime? from, DateTime? to)
    {
        var q = _db.SalesVisits.Where(v => (!companyId.HasValue || v.CompanyId == companyId.Value) && !v.IsDeleted);
        if (from.HasValue) q = q.Where(v => v.CheckInTime >= from.Value);
        if (to.HasValue)   q = q.Where(v => v.CheckInTime <= to.Value);
        var total    = await q.CountAsync();
        var byEmp    = await q.GroupBy(v => v.VisitedEmployeeId).Select(g => new { EmployeeId = g.Key, Visits = g.Count() }).ToListAsync();
        var avgDur   = await q.Where(v => v.DurationMinutes.HasValue).AverageAsync(v => (double?)v.DurationMinutes) ?? 0;
        return new { total, byEmployee = byEmp, averageDurationMinutes = Math.Round(avgDur, 1) };
    }

    public async Task<object> GetRevenueReportAsync(int? companyId, DateTime? from, DateTime? to)
    {
        var q = _db.SalesQuotations.Where(x => (!companyId.HasValue || x.CompanyId == companyId.Value) && !x.IsDeleted && x.Status == "Accepted");
        if (from.HasValue) q = q.Where(x => x.CreatedAt >= from.Value);
        if (to.HasValue)   q = q.Where(x => x.CreatedAt <= to.Value);
        var total = await q.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        var byMonth = await q
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Revenue = g.Sum(x => x.TotalAmount) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();
        return new { totalRevenue = total, byMonth };
    }

    public async Task<object> GetPipelineReportAsync(int? companyId)
    {
        var statuses = new[] { "New", "Contacted", "Qualified", "Proposal Sent", "Negotiation" };
        var pipeline = await _db.SalesLeads
            .Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted && statuses.Contains(l.Status))
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Value = g.Sum(x => x.ExpectedValue) })
            .ToListAsync();
        var total = await _db.SalesLeads
            .Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted && statuses.Contains(l.Status))
            .SumAsync(l => l.ExpectedValue) ?? 0m;
        return new { pipeline, totalPipelineValue = total };
    }

    // ── Lead Assignment ───────────────────────────────────────────────────

    public async Task<LeadListDto> AssignLeadAsync(int leadId, AssignLeadDto dto, int companyId, int assignedByUserId)
    {
        var lead = await _db.SalesLeads.FirstOrDefaultAsync(l => l.Id == leadId && l.CompanyId == companyId && !l.IsDeleted)
            ?? throw new KeyNotFoundException("Lead not found.");

        var previous = lead.EmployeeOwnerId;
        lead.EmployeeOwnerId = dto.AssignedToEmployeeId;
        lead.UpdatedAt = DateTime.UtcNow;

        _db.SalesLeadAssignments.Add(new HRMS.Domain.Entities.Sales.SalesLeadAssignment
        {
            CompanyId               = companyId,
            SalesLeadId             = leadId,
            AssignedToEmployeeId    = dto.AssignedToEmployeeId,
            AssignedByUserId        = assignedByUserId,
            ReassignedFromEmployeeId = previous,
            ActionType              = string.IsNullOrWhiteSpace(previous) ? "Assigned" : "Reassigned",
            Remarks                 = dto.Remarks,
            AssignedAt              = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return MapLeadList(lead);
    }

    public async Task<LeadListDto> ReassignLeadAsync(int leadId, ReassignLeadDto dto, int companyId, int assignedByUserId)
    {
        var lead = await _db.SalesLeads.FirstOrDefaultAsync(l => l.Id == leadId && l.CompanyId == companyId && !l.IsDeleted)
            ?? throw new KeyNotFoundException("Lead not found.");

        var previous = lead.EmployeeOwnerId;
        lead.EmployeeOwnerId = dto.NewAssignedToEmployeeId;
        lead.UpdatedAt = DateTime.UtcNow;

        _db.SalesLeadAssignments.Add(new HRMS.Domain.Entities.Sales.SalesLeadAssignment
        {
            CompanyId                = companyId,
            SalesLeadId              = leadId,
            AssignedToEmployeeId     = dto.NewAssignedToEmployeeId,
            AssignedByUserId         = assignedByUserId,
            ReassignedFromEmployeeId = previous,
            ActionType               = "Reassigned",
            Remarks                  = dto.Remarks,
            AssignedAt               = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return MapLeadList(lead);
    }

    public async Task<int> BulkAssignLeadsAsync(BulkAssignLeadsDto dto, int companyId, int assignedByUserId)
    {
        var leads = await _db.SalesLeads
            .Where(l => dto.LeadIds.Contains(l.Id) && l.CompanyId == companyId && !l.IsDeleted)
            .ToListAsync();

        var assignments = new List<HRMS.Domain.Entities.Sales.SalesLeadAssignment>();
        foreach (var lead in leads)
        {
            var previous = lead.EmployeeOwnerId;
            lead.EmployeeOwnerId = dto.AssignedToEmployeeId;
            lead.UpdatedAt = DateTime.UtcNow;
            assignments.Add(new HRMS.Domain.Entities.Sales.SalesLeadAssignment
            {
                CompanyId                = companyId,
                SalesLeadId              = lead.Id,
                AssignedToEmployeeId     = dto.AssignedToEmployeeId,
                AssignedByUserId         = assignedByUserId,
                ReassignedFromEmployeeId = previous,
                ActionType               = string.IsNullOrWhiteSpace(previous) ? "Assigned" : "Reassigned",
                Remarks                  = dto.Remarks,
                AssignedAt               = DateTime.UtcNow
            });
        }
        _db.SalesLeadAssignments.AddRange(assignments);
        await _db.SaveChangesAsync();
        return leads.Count;
    }

    public async Task<List<LeadAssignmentHistoryDto>> GetLeadAssignmentHistoryAsync(int leadId, int? companyId)
    {
        var lead = await _db.SalesLeads.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leadId && (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted)
            ?? throw new KeyNotFoundException("Lead not found.");

        var rows = await _db.SalesLeadAssignments.AsNoTracking()
            .Where(a => a.SalesLeadId == leadId && (!companyId.HasValue || a.CompanyId == companyId.Value) && !a.IsDeleted)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();

        // resolve names
        var empIds = rows.SelectMany(r => new[] { r.AssignedToEmployeeId, r.ReassignedFromEmployeeId })
            .Where(x => x != null).Distinct().ToList();
        var empNames = await _db.Set<HRMS.Domain.Entities.Employee.Employee>().AsNoTracking()
            .Where(e => empIds.Contains(e.EmployeeCode))
            .Select(e => new { EmployeeId = e.EmployeeCode, Name = e.FullName })
            .ToDictionaryAsync(e => e.EmployeeId, e => e.Name);

        var userIds = rows.Select(r => r.AssignedByUserId).Distinct().ToList();
        var userNames = await _db.Set<HRMS.Domain.Entities.Authentication.User>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FullName ?? u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        return rows.Select(r => new LeadAssignmentHistoryDto
        {
            Id                       = r.Id,
            SalesLeadId              = r.SalesLeadId,
            LeadNo                   = lead.LeadNo,
            AssignedToEmployeeId     = r.AssignedToEmployeeId,
            AssigneeName             = r.AssignedToEmployeeId != null && empNames.TryGetValue(r.AssignedToEmployeeId, out var an) ? an : null,
            AssignedByUserId         = r.AssignedByUserId,
            AssignedByName           = userNames.TryGetValue(r.AssignedByUserId, out var bn) ? bn! : null,
            ReassignedFromEmployeeId = r.ReassignedFromEmployeeId,
            ReassignedFromName       = r.ReassignedFromEmployeeId != null && empNames.TryGetValue(r.ReassignedFromEmployeeId, out var rn) ? rn : null,
            ActionType               = r.ActionType,
            Remarks                  = r.Remarks,
            AssignedAt               = r.AssignedAt
        }).ToList();
    }

    public async Task<PagedResult<QuotationListDto>> ListQuotationsPagedAsync(
        int? companyId, int? leadId = null, int? customerId = null,
        int page = 1, int pageSize = 25, string? search = null,
        string? sortBy = null, string? sortDirection = "desc")
    {
        var items = await ListQuotationsAsync(companyId, leadId, customerId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            items = items.Where(x =>
                x.QuotationNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.LeadCompanyName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.CustomerCompanyName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.Status.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return Page(items, page, pageSize, sortBy, sortDirection, "CreatedAt",
            new Dictionary<string, Func<QuotationListDto, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["CreatedAt"] = x => x.CreatedAt,
                ["ValidUntil"] = x => x.ValidUntil,
                ["TotalAmount"] = x => x.TotalAmount,
                ["Status"] = x => x.Status
            });
    }

    public async Task<(List<LeadListDto> Items, int Total)> GetMyAssignedLeadsAsync(
        string employeeId, int? companyId, int page, int pageSize)
    {
        var q = _db.SalesLeads.AsNoTracking()
            .Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted && l.EmployeeOwnerId == employeeId);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return (items.Select(l => MapLeadList(l)).ToList(), total);
    }

    public async Task<(List<LeadListDto> Items, int Total)> GetUnassignedLeadsAsync(
        int? companyId, int page, int pageSize)
    {
        var q = _db.SalesLeads.AsNoTracking()
            .Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted
                && (l.EmployeeOwnerId == null || l.EmployeeOwnerId == string.Empty));
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return (items.Select(l => MapLeadList(l)).ToList(), total);
    }

    public async Task<(List<LeadListDto> Items, int Total)> GetTeamLeadsAsync(
        string managerId, int? companyId, int page, int pageSize)
    {
        // Return all company-level assigned leads visible to the manager.
        // (ReportingManager FK is not present in the current Employee entity;
        //  hierarchy filtering can be enabled when that column is added.)
        var q = _db.SalesLeads.AsNoTracking()
            .Where(l => (!companyId.HasValue || l.CompanyId == companyId.Value) && !l.IsDeleted
                && l.EmployeeOwnerId != null);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return (items.Select(l => MapLeadList(l)).ToList(), total);
    }
}

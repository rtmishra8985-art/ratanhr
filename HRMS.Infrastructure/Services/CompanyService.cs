using HRMS.Application.Common;
using HRMS.Application.DTOs.Company;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Security;
using HRMS.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Company = HRMS.Domain.Entities.Company.Company;

namespace HRMS.Infrastructure.Services;

public class CompanyService : ICompanyService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;

    public CompanyService(ApplicationDbContext db, IFileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<int> CreateAsync(CreateCompanyDto dto)
    {
        // Company email must be unique across the platform.
        if (!string.IsNullOrWhiteSpace(dto.EmailAddress) &&
            await _db.Companies.AnyAsync(c => c.EmailAddress == dto.EmailAddress))
            throw new InvalidOperationException(
                $"A company with email '{dto.EmailAddress}' already exists.");

        var company = new Company
        {
            CompanyName = dto.CompanyName,
            CompanyFounderName = dto.CompanyFounderName,
            PhoneNumber = dto.PhoneNumber,
            EmailAddress = dto.EmailAddress,
            IndustryType = dto.IndustryType,
            BusinessType = dto.BusinessType,
            CIN = dto.CIN,
            TIN = dto.TIN,
            PAN = dto.PAN,
            TAN = dto.TAN,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City = dto.City,
            StateProvince = dto.StateProvince,
            Country = dto.Country ?? string.Empty,
            PostalCode = dto.PostalCode
        };
        _db.Companies.Add(company);
        await _db.SaveChangesAsync();
        return company.Id;
    }

    public async Task<bool> UpdateAsync(int id, CreateCompanyDto dto)
    {
        var co = await _db.Companies.FindAsync(id);
        if (co == null) return false;
        co.CompanyName = dto.CompanyName;
        co.CompanyFounderName = dto.CompanyFounderName;
        co.PhoneNumber = dto.PhoneNumber;
        co.EmailAddress = dto.EmailAddress;
        co.IndustryType = dto.IndustryType;
        co.BusinessType = dto.BusinessType;
        co.CIN = dto.CIN;
        co.TIN = dto.TIN;
        co.PAN = dto.PAN;
        co.TAN = dto.TAN;
        co.AddressLine1 = dto.AddressLine1;
        co.AddressLine2 = dto.AddressLine2;
        co.City = dto.City;
        co.StateProvince = dto.StateProvince;
        co.Country = dto.Country ?? string.Empty;
        co.PostalCode = dto.PostalCode;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateLogoAsync(int id, IFormFile logo)
    {
        var co = await _db.Companies.FindAsync(id);
        if (co == null) return false;
        // Item 9: company logos are images only — explicit profile, never subfolder inference.
        var path = await _storage.SaveFileAsync(logo, "logo", UploadProfile.Image);
        co.LogoPath = path;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<CompanyDto?> GetByIdAsync(int id)
    {
        var co = await _db.Companies.FindAsync(id);
        if (co == null) return null;
        var count = await _db.Employees.CountAsync(e => e.CompanyId == id);
        return Map(co, count);
    }

    public async Task<List<CompanyDto>> GetAllAsync()
    {
        var companies = await _db.Companies.OrderByDescending(c => c.CreatedAt).ToListAsync();

        // FIX (HIGH-N1): Single GroupBy query replaces per-company CountAsync calls.
        // Previously: 1 + N queries for N companies. Now: 2 queries regardless of N.
        var countsByCompany = await _db.Employees
            .GroupBy(e => e.CompanyId)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count).ConfigureAwait(false);

        return companies
            .Select(co => Map(co, countsByCompany.TryGetValue(co.Id, out var c) ? c : 0))
            .ToList();
    }

    public async Task<PagedResult<CompanyDto>> GetAllPagedAsync(int page, int pageSize)
    {
        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total    = await _db.Companies.CountAsync().ConfigureAwait(false);
        var companies = await _db.Companies
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync().ConfigureAwait(false);

        // FIX (HIGH-N1): Single GroupBy query for the current page only.
        // Previously: 1 + pageSize queries. Now: 2 queries regardless of page size.
        var pageIds = companies.Select(c => c.Id).ToList();
        // Employee.CompanyId is now a non-nullable int (FIX CRIT-1) — no null guard needed.
        var countsByCompany = await _db.Employees
            .Where(e => pageIds.Contains(e.CompanyId))
            .GroupBy(e => e.CompanyId)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count).ConfigureAwait(false);

        var result = companies
            .Select(co => Map(co, countsByCompany.TryGetValue(co.Id, out var c) ? c : 0))
            .ToList();
        return PagedResult<CompanyDto>.Create(result, total, page, pageSize);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var co = await _db.Companies.FindAsync(id);
        if (co == null) return false;
        // Refuse to delete a company that still owns employee records.
        if (await _db.Employees.AnyAsync(e => e.CompanyId == id)) return false;
        _db.Companies.Remove(co);
        await _db.SaveChangesAsync();
        return true;
    }

    private static CompanyDto Map(Company co, int empCount) => new()
    {
        Id = co.Id,
        CompanyName = co.CompanyName,
        CompanyFounderName = co.CompanyFounderName,
        PhoneNumber = co.PhoneNumber,
        EmailAddress = co.EmailAddress,
        IndustryType = co.IndustryType,
        BusinessType = co.BusinessType,
        CIN = co.CIN,
        TIN = co.TIN,
        PAN = co.PAN,
        TAN = co.TAN,
        AddressLine1 = co.AddressLine1,
        AddressLine2 = co.AddressLine2,
        City = co.City,
        StateProvince = co.StateProvince,
        Country = co.Country,
        PostalCode = co.PostalCode,
        LogoPath = co.LogoPath,
        CreatedAt = co.CreatedAt,
        EmployeeCount = empCount
    };
}

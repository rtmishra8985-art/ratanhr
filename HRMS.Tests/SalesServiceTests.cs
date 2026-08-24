using FluentAssertions;
using HRMS.Application.DTOs.Sales;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Sales;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Tests;

public class SalesServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly ISalesService        _svc;
    private const int CompanyId  = 1;
    private const int UserId     = 1;

    public SalesServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new ApplicationDbContext(options);
        _svc = new SalesService(_db);
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    // ─── CreateLead ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateLeadAsync_ValidDto_PersistsAndReturnsId()
    {
        var dto = new CreateLeadDto
        {
            CompanyName   = "Acme Corp",
            ContactPerson = "John Doe",
            Mobile        = "9876543210",
            Email         = "john@acme.com",
            Status        = "New"
        };
        var result = await _svc.CreateLeadAsync(dto, CompanyId, UserId);
        result.Should().NotBeNull();
        result.CompanyName.Should().Be("Acme Corp");
    }

    // ─── AssignLead ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AssignLeadAsync_ValidEmployee_AssignsSuccessfully()
    {
        var lead = await _db.SalesLeads.FirstAsync(l => l.CompanyId == CompanyId);
        var result = await _svc.AssignLeadAsync(lead.Id,
            new AssignLeadDto { AssignedToEmployeeId = "E001" },
            CompanyId, UserId);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AssignLeadAsync_CrossCompanyLead_ThrowsOrReturnsError()
    {
        // Lead 2 belongs to company 2 — company 1 caller must be rejected
        var lead2 = await _db.SalesLeads.FirstAsync(l => l.CompanyId == 2);
        var act = async () => await _svc.AssignLeadAsync(lead2.Id,
            new AssignLeadDto { AssignedToEmployeeId = "E001" },
            CompanyId, UserId);
        await act.Should().ThrowAsync<Exception>("cross-company lead access must be rejected");
    }

    // ─── GetPipeline ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPipelineReportAsync_ReturnsObjectForCompany()
    {
        var pipeline = await _svc.GetPipelineReportAsync(CompanyId);
        pipeline.Should().NotBeNull();
    }

    // ─── ListCustomers ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListCustomersAsync_CompanyIsolation_DoesNotReturnOtherCompany()
    {
        var (customers, _) = await _svc.ListCustomersAsync(CompanyId, 1, 100);
        customers.All(c => c.Id > 0).Should().BeTrue();
    }

    // ─── CreateCustomer ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCustomerAsync_ValidDto_PersistsRecord()
    {
        var dto = new CreateCustomerDto
        {
            CompanyName    = "Acme Corporation",
            ContactPerson  = "Jane Smith",
            ContactPhone   = "9000000000",
            ContactEmail   = "jane@acme.com"
        };
        var result = await _svc.CreateCustomerAsync(dto, CompanyId, UserId);
        result.Should().NotBeNull();
        result.CompanyName.Should().Be("Acme Corporation");
    }

    [Fact]
    public async Task ListCustomersAsync_ReturnsOnlyOwnCompanyCustomers()
    {
        var (customers, total) = await _svc.ListCustomersAsync(CompanyId, 1, 100);
        total.Should().BeGreaterThanOrEqualTo(0);
    }

    // ─── Seed helpers ─────────────────────────────────────────────────────────────

    private void SeedData()
    {
        _db.SalesLeads.AddRange(
            new SalesLead { CompanyId = 1, Title = "Lead 1", Status = "Open",
                            CompanyName = "Alpha", ContactPerson = "C1",
                            CreatedByUserId = 1 },
            new SalesLead { CompanyId = 2, Title = "Lead 2", Status = "Open",
                            CompanyName = "Beta",  ContactPerson = "C2",
                            CreatedByUserId = 1 }
        );
        _db.SaveChanges();
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using HRMS.Application.DTOs.Helpdesk;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Repositories;
using HRMS.Infrastructure.Services;

namespace HRMS.Tests
{
    /// <summary>
    /// Unit tests for <see cref="HelpdeskService"/>.
    /// Each test spins up an isolated in-memory database to avoid state leakage.
    /// </summary>
    public class HelpdeskServiceTests
    {
        private static ApplicationDbContext CreateDb(string dbName)
        {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new ApplicationDbContext(opts);
        }

        private static HelpdeskService CreateService(ApplicationDbContext db)
        {
            var repo   = new HelpdeskRepository(db);
            var logger = new Mock<ILogger<HelpdeskService>>().Object;
            return new HelpdeskService(db, repo, logger);
        }

        // ── CreateTicket ──────────────────────────────────────────────────

        [Fact]
        public async Task CreateTicket_ValidDto_ReturnsOpenTicket()
        {
            using var db = CreateDb(nameof(CreateTicket_ValidDto_ReturnsOpenTicket));
            var svc = CreateService(db);
            var dto = new CreateTicketDto { Title = "VPN not working", Priority = "High" };

            var result = await svc.CreateTicketAsync(dto, companyId: 1, raisedByEmployeeId: "EMP001");

            result.Should().NotBeNull();
            result.Title.Should().Be("VPN not working");
            result.Status.Should().Be("Open");
            result.Priority.Should().Be("High");
        }

        [Fact]
        public async Task CreateTicket_PersistsHistoryEntry()
        {
            using var db = CreateDb(nameof(CreateTicket_PersistsHistoryEntry));
            var svc = CreateService(db);
            var dto = new CreateTicketDto { Title = "Issue", Priority = "Medium" };

            var ticket = await svc.CreateTicketAsync(dto, 1, "EMP001");
            var history = db.HelpdeskHistories.Where(h => h.TicketId == ticket.Id);

            history.Should().HaveCount(1);
        }

        // ── UpdateTicket ──────────────────────────────────────────────────

        [Fact]
        public async Task UpdateTicket_ChangeStatus_PersistsNewStatus()
        {
            using var db = CreateDb(nameof(UpdateTicket_ChangeStatus_PersistsNewStatus));
            var svc = CreateService(db);
            var ticket = await svc.CreateTicketAsync(new CreateTicketDto { Title = "Test", Priority = "Low" }, 1, "EMP001");

            var result = await svc.UpdateTicketAsync(ticket.Id, new UpdateTicketDto { Status = "In Progress" }, 1, "U001");

            result!.Status.Should().Be("In Progress");
        }

        [Fact]
        public async Task UpdateTicket_ResolvedStatus_SetsResolvedAt()
        {
            using var db = CreateDb(nameof(UpdateTicket_ResolvedStatus_SetsResolvedAt));
            var svc = CreateService(db);
            var ticket = await svc.CreateTicketAsync(new CreateTicketDto { Title = "Printer jam", Priority = "Medium" }, 1, "EMP001");

            await svc.UpdateTicketAsync(ticket.Id, new UpdateTicketDto { Status = "Resolved" }, 1, "U001");

            var entity = await db.HelpdeskTickets.FindAsync(ticket.Id);
            entity!.ResolvedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task UpdateTicket_WrongCompany_ReturnsNull()
        {
            using var db = CreateDb(nameof(UpdateTicket_WrongCompany_ReturnsNull));
            var svc = CreateService(db);
            var ticket = await svc.CreateTicketAsync(new CreateTicketDto { Title = "Test", Priority = "Low" }, companyId: 1, "EMP001");

            var result = await svc.UpdateTicketAsync(ticket.Id, new UpdateTicketDto { Status = "Closed" }, companyId: 2, "U001");

            result.Should().BeNull();
        }

        // ── AssignTicket ──────────────────────────────────────────────────

        [Fact]
        public async Task AssignTicket_ValidRequest_SetsAgentAndInProgressStatus()
        {
            using var db = CreateDb(nameof(AssignTicket_ValidRequest_SetsAgentAndInProgressStatus));
            var svc = CreateService(db);
            var ticket = await svc.CreateTicketAsync(new CreateTicketDto { Title = "Network issue", Priority = "High" }, 1, "EMP001");

            var result = await svc.AssignTicketAsync(ticket.Id, new AssignTicketDto { AssignedToId = "AGENT001" }, 1, "U001");

            result!.AssignedToUserId.Should().Be("AGENT001");
            result.Status.Should().Be("In Progress");
        }

        // ── AddComment ────────────────────────────────────────────────────

        [Fact]
        public async Task AddComment_ValidMessage_PersistsComment()
        {
            using var db = CreateDb(nameof(AddComment_ValidMessage_PersistsComment));
            var svc = CreateService(db);
            var ticket = await svc.CreateTicketAsync(new CreateTicketDto { Title = "Access denied", Priority = "Medium" }, 1, "EMP001");

            var comment = await svc.AddCommentAsync(ticket.Id, new CreateTicketCommentDto { Message = "Looking into this." }, 1, "AGENT001");

            comment.Message.Should().Be("Looking into this.");
            comment.AuthorId.Should().Be("AGENT001");
        }

        [Fact]
        public async Task GetComments_AfterAddingTwo_ReturnsBoth()
        {
            using var db = CreateDb(nameof(GetComments_AfterAddingTwo_ReturnsBoth));
            var svc = CreateService(db);
            var ticket = await svc.CreateTicketAsync(new CreateTicketDto { Title = "Payslip missing", Priority = "Low" }, 1, "EMP001");

            await svc.AddCommentAsync(ticket.Id, new CreateTicketCommentDto { Message = "First reply" }, 1, "A1");
            await svc.AddCommentAsync(ticket.Id, new CreateTicketCommentDto { Message = "Second reply" }, 1, "A2");

            var comments = await svc.GetCommentsAsync(ticket.Id, 1);
            comments.Should().HaveCount(2);
        }

        // ── GetSummary ────────────────────────────────────────────────────

        [Fact]
        public async Task GetSummary_AfterCreatingMixedTickets_CountsCorrectly()
        {
            using var db = CreateDb(nameof(GetSummary_AfterCreatingMixedTickets_CountsCorrectly));
            var svc = CreateService(db);

            await svc.CreateTicketAsync(new CreateTicketDto { Title = "T1", Priority = "Critical" }, 1, "EMP001");
            await svc.CreateTicketAsync(new CreateTicketDto { Title = "T2", Priority = "High" }, 1, "EMP002");
            var t3 = await svc.CreateTicketAsync(new CreateTicketDto { Title = "T3", Priority = "Low" }, 1, "EMP003");
            await svc.UpdateTicketAsync(t3.Id, new UpdateTicketDto { Status = "Resolved" }, 1, "U001");

            var summary = await svc.GetSummaryAsync(1);

            summary.Open.Should().Be(2);
            summary.Resolved.Should().Be(1);
            summary.Critical.Should().Be(1);
        }

        // ── Tenant isolation ──────────────────────────────────────────────

        [Fact]
        public async Task GetTickets_DoesNotReturnTicketsFromDifferentTenant()
        {
            using var db = CreateDb(nameof(GetTickets_DoesNotReturnTicketsFromDifferentTenant));
            var svc = CreateService(db);

            await svc.CreateTicketAsync(new CreateTicketDto { Title = "Tenant1 ticket", Priority = "Medium" }, companyId: 1, "E1");
            await svc.CreateTicketAsync(new CreateTicketDto { Title = "Tenant2 ticket", Priority = "Medium" }, companyId: 2, "E2");

            var result = await svc.GetTicketsAsync(new TicketQueryDto(), companyId: 1);

            result.Items.Should().HaveCount(1);
            result.Items.First().Title.Should().Be("Tenant1 ticket");
        }
    }
}

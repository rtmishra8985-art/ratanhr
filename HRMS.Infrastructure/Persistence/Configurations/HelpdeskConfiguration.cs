using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRMS.Domain.Entities.Helpdesk;

namespace HRMS.Infrastructure.Persistence.Configurations
{
    public class HelpdeskTicketConfiguration : IEntityTypeConfiguration<HelpdeskTicket>
    {
        public void Configure(EntityTypeBuilder<HelpdeskTicket> builder)
        {
            builder.ToTable("helpdesk_tickets");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Title).IsRequired().HasMaxLength(300);
            builder.Property(t => t.Description).HasMaxLength(5000);
            builder.Property(t => t.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Open");
            builder.Property(t => t.Priority).IsRequired().HasMaxLength(20).HasDefaultValue("Medium");
            builder.Property(t => t.RaisedByEmployeeId).HasMaxLength(50);
            builder.Property(t => t.AssignedToUserId).HasMaxLength(50);

            builder.HasIndex(t => new { t.CompanyId, t.Status });
            builder.HasIndex(t => new { t.CompanyId, t.Priority });
            builder.HasIndex(t => t.RaisedByEmployeeId);
            builder.HasIndex(t => t.AssignedToUserId);

            builder.HasOne(t => t.Category)
                   .WithMany(c => c.Tickets)
                   .HasForeignKey(t => t.CategoryId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasMany(t => t.Comments)
                   .WithOne(c => c.Ticket)
                   .HasForeignKey(c => c.TicketId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.History)
                   .WithOne(h => h.Ticket)
                   .HasForeignKey(h => h.TicketId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class HelpdeskCategoryConfiguration : IEntityTypeConfiguration<HelpdeskCategory>
    {
        public void Configure(EntityTypeBuilder<HelpdeskCategory> builder)
        {
            builder.ToTable("helpdesk_categories");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
            builder.Property(c => c.Description).HasMaxLength(500);
            builder.HasIndex(c => new { c.CompanyId, c.Name });
        }
    }

    public class HelpdeskCommentConfiguration : IEntityTypeConfiguration<HelpdeskComment>
    {
        public void Configure(EntityTypeBuilder<HelpdeskComment> builder)
        {
            builder.ToTable("helpdesk_comments");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.AuthorId).IsRequired().HasMaxLength(50);
            builder.Property(c => c.Message).IsRequired().HasMaxLength(5000);
        }
    }

    public class HelpdeskHistoryConfiguration : IEntityTypeConfiguration<HelpdeskHistory>
    {
        public void Configure(EntityTypeBuilder<HelpdeskHistory> builder)
        {
            builder.ToTable("helpdesk_history");
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Action).IsRequired().HasMaxLength(100);
            builder.Property(h => h.OldValue).HasMaxLength(200);
            builder.Property(h => h.NewValue).HasMaxLength(200);
            builder.Property(h => h.PerformedByUserId).HasMaxLength(50);
        }
    }
}

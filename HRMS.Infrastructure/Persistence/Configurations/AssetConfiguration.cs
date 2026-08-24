using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRMS.Domain.Entities.Assets;

namespace HRMS.Infrastructure.Persistence.Configurations
{
    public class AssetConfiguration : IEntityTypeConfiguration<Asset>
    {
        public void Configure(EntityTypeBuilder<Asset> builder)
        {
            builder.ToTable("assets");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.AssetCode).IsRequired().HasMaxLength(50);
            builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
            builder.Property(a => a.Description).HasMaxLength(1000);
            builder.Property(a => a.SerialNumber).HasMaxLength(100);
            builder.Property(a => a.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Available");
            builder.Property(a => a.Location).HasMaxLength(200);
            builder.Property(a => a.AssignedToEmployeeId).HasMaxLength(50);
            builder.Property(a => a.PurchasePrice).HasColumnType("decimal(18,2)");
            builder.Property(a => a.CurrentValue).HasColumnType("decimal(18,2)");

            builder.HasIndex(a => new { a.CompanyId, a.AssetCode }).IsUnique();
            builder.HasIndex(a => new { a.CompanyId, a.Status });

            builder.HasOne(a => a.Category)
                   .WithMany(c => c.Assets)
                   .HasForeignKey(a => a.CategoryId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasMany(a => a.History)
                   .WithOne(h => h.Asset)
                   .HasForeignKey(h => h.AssetId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class AssetCategoryConfiguration : IEntityTypeConfiguration<AssetCategory>
    {
        public void Configure(EntityTypeBuilder<AssetCategory> builder)
        {
            builder.ToTable("asset_categories");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
            builder.Property(c => c.Description).HasMaxLength(500);
            builder.HasIndex(c => new { c.CompanyId, c.Name });
        }
    }

    public class AssetHistoryConfiguration : IEntityTypeConfiguration<AssetHistory>
    {
        public void Configure(EntityTypeBuilder<AssetHistory> builder)
        {
            builder.ToTable("asset_history");
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Action).IsRequired().HasMaxLength(50);
            builder.Property(h => h.EmployeeId).HasMaxLength(50);
            builder.Property(h => h.Notes).HasMaxLength(500);
            builder.Property(h => h.PerformedByUserId).HasMaxLength(50);
        }
    }
}

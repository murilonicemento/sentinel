using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskCatalog.Domain.EventTypes;

namespace RiskCatalog.Infrastructure.DatabaseConfigurations;

public class SeverityCriterionConfiguration : IEntityTypeConfiguration<SeverityCriterion>
{
    public void Configure(EntityTypeBuilder<SeverityCriterion> builder)
    {
        builder.ToTable("severity_criterion");
        builder.HasKey(sc => sc.Id);
        builder.Property(et => et.Id)
            .IsRequired()
            .HasColumnType("uniqueidentifier")
            .HasColumnName("id");
        builder.Property(sc => sc.EventTypeId)
            .IsRequired()
            .HasColumnType("uniqueidentifier");
        builder.Property(sc => sc.SeverityId)
            .IsRequired()
            .HasColumnType("uniqueidentifier");
        builder.Property(sc => sc.MinValue)
            .IsRequired()
            .HasColumnType("double");
        builder.Property(sc => sc.MaxValue)
            .IsRequired()
            .HasColumnType("double");
        builder.Property(sc => sc.Unit)
            .IsRequired()
            .HasColumnType("nvarchar(50)");
        builder.Property(sc => sc.Version)
            .IsRequired()
            .HasColumnType("int");

        builder.HasOne(sc => sc.EventType).WithMany(et => et.SeverityCriteria).HasForeignKey(sc => sc.EventTypeId);
        builder.HasOne(sc => sc.Severity).WithMany(et => et.SeverityCriteria).HasForeignKey(sc => sc.SeverityId);
    }
}
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
            .HasColumnType("uniqueidentifier")
            .HasColumnName("event_type_id");
        builder.Property(sc => sc.SeverityId)
            .IsRequired()
            .HasColumnType("uniqueidentifier")
            .HasColumnName("severity_id");
        builder.Property(sc => sc.MinValue)
            .IsRequired()
            .HasColumnType("double")
            .HasColumnName("min_value");
        builder.Property(sc => sc.MaxValue)
            .IsRequired()
            .HasColumnType("double")
            .HasColumnName("max_value");
        builder.Property(sc => sc.Unit)
            .IsRequired()
            .HasColumnType("nvarchar(50)")
            .HasColumnName("unit");
        builder.Property(sc => sc.Version)
            .IsRequired()
            .HasColumnType("int")
            .HasColumnName("version");

        builder.HasOne(sc => sc.EventType).WithMany(et => et.SeverityCriteria).HasForeignKey(sc => sc.EventTypeId);
        builder.HasOne(sc => sc.Severity).WithMany(et => et.SeverityCriteria).HasForeignKey(sc => sc.SeverityId);
    }
}
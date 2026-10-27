using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskCatalog.Domain.EventTypes;

namespace RiskCatalog.Infrastructure.Persistence.DatabaseConfigurations;

public class SeverityCriterionConfiguration : IEntityTypeConfiguration<SeverityCriterion>
{
    public void Configure(EntityTypeBuilder<SeverityCriterion> builder)
    {
        builder.ToTable("severity_criterion");
        builder.HasKey(sc => sc.Id);
        builder.Property(et => et.Id)
            .IsRequired()
            .HasColumnType("uuid")
            .HasColumnName("id");
        builder.Property(sc => sc.EventTypeId)
            .IsRequired()
            .HasColumnType("uuid")
            .HasColumnName("event_type_id");
        builder.Property(sc => sc.SeverityId)
            .IsRequired()
            .HasColumnType("uuid")
            .HasColumnName("severity_id");
        builder.Property(sc => sc.MinValue)
            .IsRequired()
            .HasColumnType("double precision")
            .HasColumnName("min_value");
        builder.Property(sc => sc.MaxValue)
            .IsRequired()
            .HasColumnType("double precision")
            .HasColumnName("max_value");
        builder.Property(sc => sc.Unit)
            .IsRequired()
            .HasColumnType("varchar(50)")
            .HasColumnName("unit");
        builder.Property(sc => sc.Version)
            .IsRequired()
            .HasColumnType("integer")
            .HasColumnName("version");

        builder.HasOne(sc => sc.EventType).WithMany(et => et.SeverityCriteria).HasForeignKey(sc => sc.EventTypeId);
        builder.HasOne(sc => sc.Severity).WithMany(et => et.SeverityCriteria).HasForeignKey(sc => sc.SeverityId);
    }
}
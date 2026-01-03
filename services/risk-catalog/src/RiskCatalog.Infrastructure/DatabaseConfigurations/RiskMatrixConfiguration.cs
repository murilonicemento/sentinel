using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Infrastructure.DatabaseConfigurations;

public class RiskMatrixConfiguration : IEntityTypeConfiguration<RiskMatrix>
{
    public void Configure(EntityTypeBuilder<RiskMatrix> builder)
    {
        builder.ToTable("risk_matrix");
        builder.HasKey(rm => rm.Id);
        builder.Property(et => et.Id)
            .IsRequired()
            .HasColumnType("uniqueidentifier")
            .HasColumnName("id");
        builder.Property(rm => rm.EventTypeId)
            .IsRequired()
            .HasColumnType("uniqueidentifier")
            .HasColumnName("event_type_id");
        builder.Property(rm => rm.SeverityLevel)
            .IsRequired()
            .HasColumnType("varchar(50)")
            .HasColumnName("severity_level");
        builder.Property(rm => rm.RiskLevel)
            .IsRequired()
            .HasColumnType("varchar(50)")
            .HasColumnName("risk_level");
        builder.Property(rm => rm.Version)
            .IsRequired()
            .HasColumnType("varchar(50)")
            .HasColumnName("version");

        builder.HasOne(rm => rm.EventType).WithMany(rm => rm.RiskMatrix).HasForeignKey(rm => rm.EventTypeId);
    }
}
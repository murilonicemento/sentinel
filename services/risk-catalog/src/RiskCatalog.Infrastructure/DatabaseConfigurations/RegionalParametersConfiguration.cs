using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Infrastructure.DatabaseConfigurations;

public class RegionalParametersConfiguration : IEntityTypeConfiguration<RegionalParameter>
{
    public void Configure(EntityTypeBuilder<RegionalParameter> builder)
    {
        builder.ToTable("regional_parameter");
        builder.HasKey(rp => rp.Id);
        builder.Property(rp => rp.RegionId)
            .IsRequired()
            .HasColumnType("uniqueidentifier")
            .HasColumnName("region_id");
        builder.Property(rp => rp.AdjustmentFactor)
            .IsRequired()
            .HasColumnType("double")
            .HasColumnName("adjustment_factor");
        builder.Property(rp => rp.Description)
            .IsRequired()
            .HasColumnType("nvarchar(255)")
            .HasColumnName("description");
    }
}
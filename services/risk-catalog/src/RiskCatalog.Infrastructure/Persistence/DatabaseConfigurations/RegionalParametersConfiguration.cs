using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Infrastructure.Persistence.DatabaseConfigurations;

public class RegionalParametersConfiguration : IEntityTypeConfiguration<RegionalParameter>
{
    public void Configure(EntityTypeBuilder<RegionalParameter> builder)
    {
        builder.ToTable("regional_parameter");
        builder.HasKey(rp => rp.Id);
        builder.Property(et => et.Id)
            .IsRequired()
            .HasColumnType("uuid")
            .HasColumnName("id");
        builder.Property(rp => rp.AdjustmentFactor)
            .IsRequired()
            .HasColumnType("double precision")
            .HasColumnName("adjustment_factor");
        builder.Property(rp => rp.Description)
            .IsRequired()
            .HasColumnType("varchar(255)")
            .HasColumnName("description");
    }
}
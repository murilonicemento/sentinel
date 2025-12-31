using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskCatalog.Domain.EventTypes;

namespace RiskCatalog.Infrastructure.DatabaseConfigurations;

public class SeverityConfiguration : IEntityTypeConfiguration<Severity>
{
    public void Configure(EntityTypeBuilder<Severity> builder)
    {
        builder.ToTable("severity");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Level)
            .IsRequired()
            .HasColumnType("nvarchar(50)")
            .HasColumnName("level");
        builder.Property(s => s.Description)
            .IsRequired()
            .HasColumnType("nvarchar(255)")
            .HasColumnName("description");
    }
}
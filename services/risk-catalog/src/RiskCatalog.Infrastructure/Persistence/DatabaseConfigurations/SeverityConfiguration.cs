using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskCatalog.Domain.EventTypes;

namespace RiskCatalog.Infrastructure.Persistence.DatabaseConfigurations;

public class SeverityConfiguration : IEntityTypeConfiguration<Severity>
{
    public void Configure(EntityTypeBuilder<Severity> builder)
    {
        builder.ToTable("severity");
        builder.HasKey(s => s.Id);
        builder.Property(et => et.Id)
            .IsRequired()
            .HasColumnType("uniqueidentifier")
            .HasColumnName("id");
        builder.Property(s => s.Level)
            .IsRequired()
            .HasColumnType("integer")
            .HasColumnName("level");
        builder.Property(s => s.Description)
            .IsRequired()
            .HasColumnType("nvarchar(255)")
            .HasColumnName("description");
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskCatalog.Domain.EventTypes;

namespace RiskCatalog.Infrastructure.DatabaseConfigurations;

public class EventTypeConfiguration : IEntityTypeConfiguration<EventType>
{
    public void Configure(EntityTypeBuilder<EventType> builder)
    {
        builder.ToTable("event_types");
        builder.HasKey(et => et.Id);
        builder.Property(et => et.Code)
            .IsRequired()
            .HasColumnType("nvarchar(50)")
            .HasColumnName("code");
        builder.Property(et => et.Name)
            .IsRequired()
            .HasColumnType("nvarchar(100)")
            .HasColumnName("name");
        builder.Property(et => et.Description)
            .IsRequired()
            .HasColumnType("nvarchar(255)")
            .HasColumnName("description");
        builder.Property(et => et.IsActive)
            .IsRequired()
            .HasColumnType("boolean")
            .HasColumnName("is_active")
            .HasDefaultValue(true);
    }
}
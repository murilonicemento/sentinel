using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskCatalog.Domain.EventTypes;

namespace RiskCatalog.Infrastructure.Persistence.DatabaseConfigurations;

public class EventTypeConfiguration : IEntityTypeConfiguration<EventType>
{
    public void Configure(EntityTypeBuilder<EventType> builder)
    {
        builder.ToTable("event_type");
        builder.HasKey(et => et.Id);
        builder.Property(et => et.Id)
            .IsRequired()
            .HasColumnType("uuid")
            .HasColumnName("id");
        builder.Property(et => et.Code)
            .IsRequired()
            .HasColumnType("varchar(50)")
            .HasColumnName("code");
        builder.Property(et => et.Name)
            .IsRequired()
            .HasColumnType("varchar(100)")
            .HasColumnName("name");
        builder.Property(et => et.Description)
            .IsRequired()
            .HasColumnType("varchar(255)")
            .HasColumnName("description");
        builder.Property(et => et.IsActive)
            .IsRequired()
            .HasColumnType("boolean")
            .HasColumnName("is_active")
            .HasDefaultValue(true);
    }
}
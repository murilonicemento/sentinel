using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Domain.Enums;
using AlertOrchestrator.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AlertOrchestrator.Infrastructure.Persistence.EntityConfigurations;

public sealed class AlertWindowConfiguration : IEntityTypeConfiguration<AlertWindow>
{
    public void Configure(EntityTypeBuilder<AlertWindow> builder)
    {
        builder.ToTable("alert_windows");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Region)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.RiskType)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => RiskType.FromString(v));

        builder.Property(x => x.TenantId)
            .HasMaxLength(50);

        builder.Property(x => x.OpenedAt)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.Threshold)
            .IsRequired();

        builder.Property(x => x.TriggeredAt);

        builder.Property(x => x.ClosedAt);
        builder.Property(x => x.ClosedBy).HasMaxLength(100);
        builder.Property(x => x.CloseReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.Region, x.RiskType, x.Status });
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasIndex(x => x.TenantId);

        builder.OwnsMany(x => x.Signals, signals =>
        {
            signals.ToTable("alert_signals");

            signals.HasKey("Id");

            signals.Property<Guid>("Id")
                .ValueGeneratedOnAdd();

            signals.Property(x => x.Source)
                .IsRequired()
                .HasConversion<string>();

            signals.Property(x => x.Timestamp)
                .IsRequired();

            signals.Property(x => x.EventId)
                .IsRequired();

            signals.Property(x => x.RiskScore)
                .IsRequired();

            signals.Property(x => x.RiskType)
                .IsRequired()
                .HasMaxLength(50)
                .HasConversion(
                    v => v.ToString(),
                    v => RiskType.FromString(v));

            signals.Property(x => x.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

            signals.HasIndex(x => x.EventId)
                .IsUnique();

            signals.WithOwner()
                .HasForeignKey("AlertWindowId");
        });
    }
}

using System.Text.Json;
using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Domain.Enums;
using AlertOrchestrator.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlertOrchestrator.Infrastructure.Persistence.EntityConfigurations;

public sealed class AlertWindowConfiguration : IEntityTypeConfiguration<AlertWindow>
{
    public void Configure(EntityTypeBuilder<AlertWindow> builder)
    {
        builder.ToTable("alert_windows");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.Region)
            .HasColumnName("region")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.RiskType)
            .HasColumnName("risk_type")
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => RiskType.FromString(v));

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasMaxLength(50);

        builder.Property(x => x.OpenedAt)
            .HasColumnName("opened_at")
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(x => x.Threshold)
            .HasColumnName("threshold")
            .IsRequired();

        builder.Property(x => x.TriggeredAt)
            .HasColumnName("triggered_at");

        builder.Property(x => x.FinalRiskScore)
            .HasColumnName("final_risk_score");

        builder.Property(x => x.CurrentEscalationLevel)
            .HasColumnName("current_escalation_level")
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(AlertEscalationLevel.Level1)
            .HasConversion<string>();

        builder.Property(x => x.ConfirmedAt)
            .HasColumnName("confirmed_at");

        builder.Property(x => x.ClosedAt)
            .HasColumnName("closed_at");

        builder.Property(x => x.ClosedBy)
            .HasColumnName("closed_by")
            .HasMaxLength(100);

        builder.Property(x => x.CloseReason)
            .HasColumnName("close_reason")
            .HasMaxLength(500);

        // Optimistic concurrency on the Postgres system column xmin (no schema change).
        builder.Property<uint>("Version")
            .IsRowVersion();

        // Index names match platform/init-db/postgres/2-schema.sql, where the schema originated.
        builder.HasIndex(x => new { x.Region, x.RiskType, x.Status })
            .HasDatabaseName("idx_alert_windows_region_risk_status");
        builder.HasIndex(x => x.Status)
            .HasDatabaseName("idx_alert_windows_status");
        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("idx_alert_windows_expires_at");
        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("idx_alert_windows_tenant_id");
        builder.HasIndex(x => x.ConfirmedAt)
            .HasDatabaseName("idx_alert_windows_confirmed_at")
            .HasFilter("confirmed_at IS NULL");

        builder.OwnsMany(x => x.Signals, signals =>
        {
            signals.ToTable("alert_signals");

            signals.HasKey("Id");

            signals.Property<Guid>("Id")
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            signals.Property(x => x.Source)
                .HasColumnName("source")
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion<string>();

            signals.Property(x => x.Timestamp)
                .HasColumnName("timestamp")
                .IsRequired();

            signals.Property(x => x.EventId)
                .HasColumnName("event_id")
                .IsRequired();

            signals.Property(x => x.RiskScore)
                .HasColumnName("risk_score")
                .IsRequired();

            signals.Property(x => x.RiskType)
                .HasColumnName("risk_type")
                .IsRequired()
                .HasMaxLength(50)
                .HasConversion(
                    v => v.ToString(),
                    v => RiskType.FromString(v));

            signals.Property(x => x.Metadata)
                .HasColumnName("metadata")
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ??
                         new Dictionary<string, string>());

            signals.HasIndex(x => x.EventId)
                .IsUnique()
                .HasDatabaseName("alert_signals_event_id_key");

            signals.WithOwner()
                .HasForeignKey("AlertWindowId");

            signals.Property("AlertWindowId")
                .HasColumnName("alert_window_id");
        });
    }
}
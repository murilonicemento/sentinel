using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Catalog;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.ValueObjects;

namespace AuditCompliance.UnitTests.Support;

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }
}

public static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static AuditRecord Record(Guid? tenantId = null, string eventId = "evt-1")
    {
        return AuditRecord.Create(eventId, AuditEventCatalog.TenantCreated, 1, "tenants-billing", Now.AddMinutes(-1),
            Now, tenantId ?? TenantId, actor: Actor.Create(ActorTypeEnum.User, "user-1"));
    }

    public static AuditRecordFilter Filter()
    {
        return AuditRecordFilter.Create(TenantId, Now.AddDays(-1), Now);
    }

    public static AuditExport Export(ExportStatusEnum status, int attempts = 1, DateTimeOffset? expiresAt = null,
        string? fileId = null)
    {
        return AuditExport.Rehydrate(Guid.NewGuid(), Filter(), ExportFormatEnum.Csv, "user-1", status, Now, null,
            expiresAt, attempts, fileId, null, null);
    }
}

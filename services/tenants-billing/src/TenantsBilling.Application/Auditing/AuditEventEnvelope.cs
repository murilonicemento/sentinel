using System.Diagnostics;
using System.Globalization;
using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;

namespace TenantsBilling.Application.Auditing;

/// <summary>Who performed an audited action: <c>User</c>, <c>Service</c> or <c>System</c>, and its id.</summary>
public sealed record AuditActor(string Type, string Id)
{
    public static readonly AuditActor System = new("System", "tenants-billing");
}

public sealed record AuditResource(string Type, string Id);

/// <summary>
/// Payload of <c>audit-events</c> (v1), consumed by audit-compliance (ADR-0001, X-24). tenants-billing reports the
/// creation of a tenant (<c>TenantCreated</c>) and each change of its status (<c>TenantUpdated</c>).
/// </summary>
public sealed record AuditEventEnvelope(
    string EventId,
    string EventType,
    int EventVersion,
    string SourceService,
    string OccurredAt,
    string TenantId,
    string? CorrelationId,
    string? CausationId,
    string Action,
    AuditActor Actor,
    AuditResource Resource,
    object? Metadata,
    object? BeforeState,
    object? AfterState)
{
    private const string Source = "tenants-billing";

    public static AuditEventEnvelope TenantCreated(Tenant tenant, AuditActor actor) => new(
        $"tenant-created:{tenant.Id:N}",
        "TenantCreated",
        1,
        Source,
        Iso(tenant.CreatedAt),
        tenant.Id.ToString(),
        CurrentTraceId(),
        null,
        "tenant.create",
        actor,
        new AuditResource("Tenant", tenant.Id.ToString()),
        null,
        null,
        StateOf(tenant));

    /// <summary>A status change. Each change has its own id: the tenant and the instant it was updated.</summary>
    public static AuditEventEnvelope TenantStatusChanged(Tenant tenant, TenantStatus previousStatus, AuditActor actor) =>
        new(
            $"tenant-status:{tenant.Id:N}:{Utc(tenant.UpdatedAt).Ticks.ToString(CultureInfo.InvariantCulture)}",
            "TenantUpdated",
            1,
            Source,
            Iso(tenant.UpdatedAt),
            tenant.Id.ToString(),
            CurrentTraceId(),
            null,
            "tenant.status.change",
            actor,
            new AuditResource("Tenant", tenant.Id.ToString()),
            null,
            new { Status = previousStatus.ToString() },
            new { Status = tenant.Status.ToString() });

    private static object StateOf(Tenant tenant) => new
    {
        tenant.Name,
        tenant.PlanId,
        Status = tenant.Status.ToString(),
        tenant.Region,
        tenant.TimeZone
    };

    // audit-compliance requires an explicit offset; values read from the database come back without a kind.
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string Iso(DateTime value) =>
        Utc(value).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    private static string? CurrentTraceId() => Activity.Current?.TraceId.ToString();
}

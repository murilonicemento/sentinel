using System.Text.Json;
using TenantsBilling.Application.Auditing;
using TenantsBilling.Application.Services;
using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;
using TenantsBilling.Domain.Events;
using TenantsBilling.Infrastructure.InMemory;

namespace TenantsBilling.UnitTests;

/// <summary>
/// <c>audit-events</c> as audit-compliance reads it (X-24). The samples are the ones audit-compliance's
/// <c>TenantsBillingContractTests</c> ingests.
/// </summary>
public class AuditEventContractTests
{
    public const string TenantCreatedSample = """
        {"EventId":"tenant-created:11111111111111111111111111111111","EventType":"TenantCreated","EventVersion":1,"SourceService":"tenants-billing","OccurredAt":"2026-03-10T11:59:00.0000000Z","TenantId":"11111111-1111-1111-1111-111111111111","CorrelationId":null,"CausationId":null,"Action":"tenant.create","Actor":{"Type":"User","Id":"user-42"},"Resource":{"Type":"Tenant","Id":"11111111-1111-1111-1111-111111111111"},"Metadata":null,"BeforeState":null,"AfterState":{"Name":"Acme","PlanId":"22222222-2222-2222-2222-222222222222","Status":"Active","Region":"global","TimeZone":"America/Sao_Paulo"}}
        """;

    public const string TenantUpdatedSample = """
        {"EventId":"tenant-status:11111111111111111111111111111111:639087407400000000","EventType":"TenantUpdated","EventVersion":1,"SourceService":"tenants-billing","OccurredAt":"2026-03-10T11:59:00.0000000Z","TenantId":"11111111-1111-1111-1111-111111111111","CorrelationId":null,"CausationId":null,"Action":"tenant.status.change","Actor":{"Type":"User","Id":"user-42"},"Resource":{"Type":"Tenant","Id":"11111111-1111-1111-1111-111111111111"},"Metadata":null,"BeforeState":{"Status":"Active"},"AfterState":{"Status":"Suspended"}}
        """;

    private static readonly AuditActor User = new("User", "user-42");

    private static Tenant Tenant(TenantStatus status, DateTimeKind kind = DateTimeKind.Utc) => Domain.Aggregates.Tenant
        .Rehydrate(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Acme",
            Guid.Parse("22222222-2222-2222-2222-222222222222"), status, "global", "America/Sao_Paulo",
            new DateTime(2026, 3, 10, 11, 59, 0, kind), new DateTime(2026, 3, 10, 11, 59, 0, kind));

    [Fact]
    public void TenantCreated_SerializesToTheContract()
    {
        Assert.Equal(TenantCreatedSample.Trim(),
            JsonSerializer.Serialize(AuditEventEnvelope.TenantCreated(Tenant(TenantStatus.Active), User)));
    }

    [Fact]
    public void TenantStatusChanged_SerializesToTheContract()
    {
        Assert.Equal(TenantUpdatedSample.Trim(), JsonSerializer.Serialize(
            AuditEventEnvelope.TenantStatusChanged(Tenant(TenantStatus.Suspended), TenantStatus.Active, User)));
    }

    [Fact]
    public void TimestampsReadFromTheDatabase_StillCarryTheUtcOffset()
    {
        var audit = AuditEventEnvelope.TenantCreated(Tenant(TenantStatus.Active, DateTimeKind.Unspecified), User);

        Assert.Equal("2026-03-10T11:59:00.0000000Z", audit.OccurredAt);
    }
}

public class TenantAuditTests
{
    private readonly InMemoryTenantRepository _tenants = new();
    private readonly InMemoryPlanRepository _plans = new();
    private readonly RecordingAuditPublisher _audit = new();
    private readonly AuditActor _actor = new("User", "admin-7");

    private TenantManagementService Service() => new(_tenants, _plans, new NullPublisher(),
        new InMemoryTenantUsageRepository(), _audit, new FixedActor(_actor));

    private async Task<Tenant> CreateAsync()
    {
        var plan = new Plan(Guid.NewGuid(), "Pro", 1000, 200, 80, 25);
        await _plans.AddAsync(plan);
        return await Service().CreateTenantAsync(new CreateTenantCommand($"tenant-{Guid.NewGuid():N}", plan.Id));
    }

    [Fact]
    public async Task CreateTenant_AuditsTheCreationWithTheActorAndTheNewState()
    {
        var tenant = await CreateAsync();

        var audit = Assert.Single(_audit.Published);
        Assert.Equal(("TenantCreated", "tenant.create", tenant.Id.ToString(), _actor),
            (audit.EventType, audit.Action, audit.TenantId, audit.Actor));
        Assert.Equal(new AuditResource("Tenant", tenant.Id.ToString()), audit.Resource);
        Assert.Contains("\"Status\":\"Active\"", JsonSerializer.Serialize(audit.AfterState));
    }

    [Fact]
    public async Task ChangeStatus_AuditsTheStatusBeforeAndAfter()
    {
        var tenant = await CreateAsync();

        await Service().ChangeStatusAsync(tenant.Id, TenantStatus.Suspended);

        var audit = _audit.Published[^1];
        Assert.Equal(("TenantUpdated", "tenant.status.change", _actor), (audit.EventType, audit.Action, audit.Actor));
        Assert.Equal("""{"Status":"Active"}""", JsonSerializer.Serialize(audit.BeforeState));
        Assert.Equal("""{"Status":"Suspended"}""", JsonSerializer.Serialize(audit.AfterState));
    }

    [Fact]
    public async Task EachStatusChange_HasItsOwnAuditId()
    {
        var tenant = await CreateAsync();

        await Service().ChangeStatusAsync(tenant.Id, TenantStatus.Suspended);
        await Service().ChangeStatusAsync(tenant.Id, TenantStatus.Active);

        var ids = _audit.Published.Skip(1).Select(a => a.EventId).ToList();
        Assert.Equal(2, ids.Distinct().Count());
    }

    [Fact]
    public async Task ChangeStatus_ToTheCurrentStatusOrNotAllowed_AuditsNothing()
    {
        var tenant = await CreateAsync();
        await Service().ChangeStatusAsync(tenant.Id, TenantStatus.Canceled);
        var published = _audit.Published.Count;

        await Service().ChangeStatusAsync(tenant.Id, TenantStatus.Canceled);
        await Assert.ThrowsAnyAsync<Exception>(() => Service().ChangeStatusAsync(tenant.Id, TenantStatus.Active));

        Assert.Equal(published, _audit.Published.Count);
    }

    private sealed class NullPublisher : ITenantEventPublisher
    {
        public Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

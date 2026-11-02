using AuditCompliance.Application.Ingestion;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AuditCompliance.UnitTests.Application;

/// <summary>
/// <c>audit-events</c> from tenants-billing (X-24). Each sample is the one tenants-billing's
/// <c>AuditEventContractTests</c> serializes.
/// </summary>
public class TenantsBillingContractTests
{
    private static readonly Dictionary<string, string> Samples = new()
    {
        ["TenantCreated"] = """
            {"EventId":"tenant-created:11111111111111111111111111111111","EventType":"TenantCreated","EventVersion":1,"SourceService":"tenants-billing","OccurredAt":"2026-03-10T11:59:00.0000000Z","TenantId":"11111111-1111-1111-1111-111111111111","CorrelationId":null,"CausationId":null,"Action":"tenant.create","Actor":{"Type":"User","Id":"user-42"},"Resource":{"Type":"Tenant","Id":"11111111-1111-1111-1111-111111111111"},"Metadata":null,"BeforeState":null,"AfterState":{"Name":"Acme","PlanId":"22222222-2222-2222-2222-222222222222","Status":"Active","Region":"global","TimeZone":"America/Sao_Paulo"}}
            """,
        ["TenantUpdated"] = """
            {"EventId":"tenant-status:11111111111111111111111111111111:639087407400000000","EventType":"TenantUpdated","EventVersion":1,"SourceService":"tenants-billing","OccurredAt":"2026-03-10T11:59:00.0000000Z","TenantId":"11111111-1111-1111-1111-111111111111","CorrelationId":null,"CausationId":null,"Action":"tenant.status.change","Actor":{"Type":"User","Id":"user-42"},"Resource":{"Type":"Tenant","Id":"11111111-1111-1111-1111-111111111111"},"Metadata":null,"BeforeState":{"Status":"Active"},"AfterState":{"Status":"Suspended"}}
            """
    };

    [Theory]
    [InlineData("TenantCreated", "tenant.create")]
    [InlineData("TenantUpdated", "tenant.status.change")]
    public async Task EventFromTenantsBilling_IsPersistedWithItsActorAndResource(string eventType, string action)
    {
        AuditRecord? persisted = null;
        var repository = new Mock<IAuditRecordRepository>();
        repository.Setup(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<AuditRecord, CancellationToken>((record, _) => persisted = record)
            .ReturnsAsync(AppendResultEnum.Appended);
        var hasher = new Mock<IIntegrityHasher>();
        hasher.Setup(h => h.Compute(It.IsAny<AuditRecord>())).Returns("sha256:test");
        var handler = new IngestAuditEventHandler(repository.Object, hasher.Object, new Mock<IAuditMetrics>().Object,
            new FixedTimeProvider(TestData.Now), NullLogger<IngestAuditEventHandler>.Instance);

        var outcome = await handler.HandleAsync(
            new IncomingAuditMessage(Samples[eventType], "audit-events", 0, 1, null), CancellationToken.None);

        Assert.Equal(IngestionStatusEnum.Persisted, outcome.Status);
        Assert.NotNull(persisted);
        Assert.Equal(eventType, persisted!.EventType);
        Assert.Equal(action, persisted.Action);
        Assert.Equal("tenants-billing", persisted.SourceService);
        Assert.Equal(TestData.TenantId, persisted.TenantId);
        Assert.Equal(ActorTypeEnum.User, persisted.Actor!.Type);
        Assert.Equal("user-42", persisted.Actor.Id);
        Assert.Equal("Tenant", persisted.Resource!.Type);
        Assert.Equal(TestData.Now.AddMinutes(-1), persisted.OccurredAt);
    }
}

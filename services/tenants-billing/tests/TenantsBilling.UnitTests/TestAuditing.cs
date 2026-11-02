using TenantsBilling.Application.Auditing;

namespace TenantsBilling.UnitTests;

internal sealed class RecordingAuditPublisher : IAuditEventPublisher
{
    public List<AuditEventEnvelope> Published { get; } = new();

    public Task PublishAsync(AuditEventEnvelope auditEvent, CancellationToken cancellationToken = default)
    {
        Published.Add(auditEvent);
        return Task.CompletedTask;
    }
}

internal sealed class FixedActor(AuditActor? actor = null) : ICurrentActor
{
    public AuditActor Get() => actor ?? new AuditActor("User", "user-1");
}

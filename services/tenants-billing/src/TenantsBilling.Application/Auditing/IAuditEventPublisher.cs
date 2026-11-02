namespace TenantsBilling.Application.Auditing;

/// <summary>Sends audited actions to audit-compliance (<c>audit-events</c>).</summary>
public interface IAuditEventPublisher
{
    Task PublishAsync(AuditEventEnvelope auditEvent, CancellationToken cancellationToken = default);
}

/// <summary>The identity performing the current operation, taken from the validated token.</summary>
public interface ICurrentActor
{
    AuditActor Get();
}

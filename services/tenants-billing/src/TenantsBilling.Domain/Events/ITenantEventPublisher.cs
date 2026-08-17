namespace TenantsBilling.Domain.Events;

public interface ITenantEventPublisher
{
    Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default);
}

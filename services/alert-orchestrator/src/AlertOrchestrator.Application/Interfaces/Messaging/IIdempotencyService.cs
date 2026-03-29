namespace AlertOrchestrator.Application.Interfaces.Messaging;

public interface IIdempotencyService
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

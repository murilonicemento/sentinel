namespace Ingestion.Application.Interfaces.Publishers;

public interface IPublisher : IAsyncDisposable
{
    public Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default);
}
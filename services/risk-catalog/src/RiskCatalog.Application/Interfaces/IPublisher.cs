namespace RiskCatalog.Application.Interfaces;

public interface IPublisher : IAsyncDisposable
{
    Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default);
}


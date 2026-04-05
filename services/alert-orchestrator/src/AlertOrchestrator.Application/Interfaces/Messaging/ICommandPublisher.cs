namespace AlertOrchestrator.Application.Interfaces.Messaging;

public interface ICommandPublisher
{
    Task PublishAsync<T>(T command, CancellationToken cancellationToken = default) where T : class;
}
namespace AlertOrchestrator.Application.Interfaces.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class;
}

public interface ICommandPublisher
{
    Task PublishAsync<T>(T command, CancellationToken cancellationToken = default) where T : class;
}

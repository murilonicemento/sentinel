namespace RiskEvaluation.Application.Interfaces.Messaging;

public interface IEventPublisher
{
    public Task PublishAsync<T>(T @event) where T : class;
}
namespace RiskEvaluation.Application.Interfaces;

public interface IEventPublisher
{
    public Task PublishAsync<T>(T @event) where T : class;
}
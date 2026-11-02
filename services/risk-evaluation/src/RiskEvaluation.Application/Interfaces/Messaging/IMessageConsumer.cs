namespace RiskEvaluation.Application.Interfaces.Messaging;

public interface IMessageConsumer : IAsyncDisposable
{
    public Task StartConsumingAsync(CancellationToken cancellationToken);
}
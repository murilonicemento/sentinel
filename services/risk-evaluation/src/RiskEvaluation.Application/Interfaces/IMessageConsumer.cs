namespace RiskEvaluation.Application.Interfaces;

public interface IMessageConsumer : IAsyncDisposable
{
    public Task StartConsumingAsync(CancellationToken cancellationToken);
}
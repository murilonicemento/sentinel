namespace RiskEvaluation.Application.Interfaces;

public interface IMessageConsumer
{
    Task StartConsumingAsync(CancellationToken cancellationToken);
}
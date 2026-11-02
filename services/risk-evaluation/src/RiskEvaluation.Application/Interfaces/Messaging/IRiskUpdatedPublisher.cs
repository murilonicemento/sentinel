using RiskEvaluation.Application.Contracts;

namespace RiskEvaluation.Application.Interfaces.Messaging;

public interface IRiskUpdatedPublisher
{
    Task PublishAsync(RiskUpdatedEvent riskUpdated, CancellationToken cancellationToken = default);
}

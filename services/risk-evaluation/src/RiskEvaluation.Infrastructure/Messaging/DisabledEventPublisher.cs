using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Interfaces.Messaging;

namespace RiskEvaluation.Infrastructure.Messaging;

/// <summary>
/// Used when <c>Kafka:PublishRiskEvents</c> is false: <c>risk-events</c> is deprecated (sunset 2027-01-31, X-24) and
/// <c>risk-updated</c> carries what alert-orchestrator needs. The evaluation is still stored and sent on
/// <c>risk-updated</c>; only <c>RiskEvaluatedEvent</c> / <c>HighRiskDetectedEvent</c> are not published.
/// </summary>
public sealed class DisabledEventPublisher : IEventPublisher
{
    private readonly ILogger<DisabledEventPublisher> _logger;

    public DisabledEventPublisher(ILogger<DisabledEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(T @event) where T : class
    {
        _logger.LogDebug("{EventType} not published: risk-events is disabled (Kafka:PublishRiskEvents=false)",
            typeof(T).Name);
        return Task.CompletedTask;
    }
}

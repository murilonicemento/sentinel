using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Interfaces;

namespace RiskEvaluation.Infrastructure.HostedServices;

public class MessageConsumerHostedService : BackgroundService
{
    private readonly IMessageConsumer _consumer;
    private readonly ILogger<MessageConsumerHostedService> _logger;

    public MessageConsumerHostedService(IMessageConsumer consumer, ILogger<MessageConsumerHostedService> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting message consumer");
        await _consumer.StartConsumingAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping message consumer");

        await _consumer.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
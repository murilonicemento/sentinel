using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.Interfaces.Messaging;

namespace RiskEvaluation.Infrastructure.HostedServices;

public class MessageConsumerHostedService : BackgroundService
{
    private readonly IMessageConsumer _consumer;
    private readonly ILogger<MessageConsumerHostedService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public MessageConsumerHostedService(IMessageConsumer consumer, ILogger<MessageConsumerHostedService> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _consumer.StartConsumingAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Kafka consumer stopping due to cancellation request");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming message from Kafka");
            }

            await Task.Delay(_interval, cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping message consumer");

        await _consumer.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
using RiskEvaluation.Application.Interfaces;

namespace RiskEvaluation.Api;

public class MessageConsumerHostedService : IHostedService
{
    private readonly IMessageConsumer _consumer;
    private readonly ILogger<MessageConsumerHostedService> _logger;
    private Task? _consumerTask;
    private CancellationTokenSource? _cts;

    public MessageConsumerHostedService(IMessageConsumer consumer, ILogger<MessageConsumerHostedService> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting message consumer");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumerTask = _consumer.StartConsumingAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping message consumer");
        if (_cts != null)
        {
            _cts.Cancel();
        }
        if (_consumerTask != null)
        {
            await _consumerTask.WaitAsync(cancellationToken);
        }
    }
}
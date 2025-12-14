using Ingestion.Application.Interfaces.Publishers;
using Ingestion.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ingestion.Infrastructure.Write.HostedServices;

public class OutboxHostedService : BackgroundService
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IPublisher _kafkaPublisher;
    private readonly ILogger<OutboxHostedService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);

    public OutboxHostedService(
        IOutboxRepository outboxRepository,
        IPublisher kafkaPublisher,
        ILogger<OutboxHostedService> logger
    )
    {
        _outboxRepository = outboxRepository;
        _kafkaPublisher = kafkaPublisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var pending = await _outboxRepository.GetPending();

            foreach (var row in pending)
            {
                try
                {
                    await _kafkaPublisher.PublishAsync(row.OutboxType, row.Payload, cancellationToken);
                    await _outboxRepository.UpdateProcessed(row.Id);
                    _logger.LogInformation("Outbox message published with success with id {id}.", row.Id);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to publish outbox {outboxId}", row.Id);
                }
            }

            await Task.Delay(_interval, cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _kafkaPublisher.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
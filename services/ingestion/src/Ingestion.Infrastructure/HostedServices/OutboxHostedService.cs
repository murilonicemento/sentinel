using Ingestion.Application.Interfaces.Publishers;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ingestion.Infrastructure.HostedServices;

public class OutboxHostedService : IHostedService
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IPublisher _kafkaPublisher;
    private readonly ILogger<OutboxHostedService> _logger;

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

    public async Task StartAsync(CancellationToken cancellationToken)
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
    }

    public async Task StopAsync(CancellationToken cancellationToken) =>
        await _kafkaPublisher.DisposeAsync();
}
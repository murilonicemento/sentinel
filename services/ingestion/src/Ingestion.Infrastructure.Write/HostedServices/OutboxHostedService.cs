using System.Diagnostics;
using Ingestion.Application.Interfaces.Publishers;
using Ingestion.Application.Interfaces.Repositories;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Outbox;
using Ingestion.Infrastructure.Write.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ingestion.Infrastructure.Write.HostedServices;

/// <summary>
/// Relays pending outbox rows to Kafka. Each batch runs in one transaction: the rows stay locked
/// (<c>FOR UPDATE SKIP LOCKED</c>) while they are published and marked, so a second instance skips them. A row whose
/// publication fails stays pending and is retried on the next pass; event ids are deterministic, so consumers absorb a
/// row published twice (e.g. a crash between publish and commit).
/// </summary>
public class OutboxHostedService : BackgroundService
{
    private static readonly ActivitySource Source = new(KafkaTracing.ActivitySourceName);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPublisher _kafkaPublisher;
    private readonly ILogger<OutboxHostedService> _logger;
    private readonly TimeSpan _interval;

    public OutboxHostedService(
        IServiceScopeFactory scopeFactory,
        IPublisher kafkaPublisher,
        IConfiguration configuration,
        ILogger<OutboxHostedService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _kafkaPublisher = kafkaPublisher;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(configuration.GetValue("Outbox:IntervalSeconds", 10));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RelayPendingAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // The database or broker is unavailable: keep the host up and try again on the next pass.
                _logger.LogError(exception, "Outbox relay pass failed; retrying in {Interval}", _interval);
            }

            await Task.Delay(_interval, cancellationToken);
        }
    }

    private async Task RelayPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await unitOfWork.ExecuteAsync(async ct =>
        {
            foreach (var row in await outbox.GetPendingAsync())
            {
                using var relay = StartRelayActivity(row);
                try
                {
                    await _kafkaPublisher.PublishAsync(row.OutboxType, row.Payload, ct);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    relay?.SetStatus(ActivityStatusCode.Error, "Publication failed");
                    _logger.LogError(exception, "Failed to publish outbox {OutboxId}; it stays pending", row.Id);
                    continue;
                }

                await outbox.UpdateProcessedAsync(row.Id);
                _logger.LogInformation("Outbox message {OutboxId} published to {Topic}", row.Id, row.OutboxType);
            }
        }, cancellationToken);
    }

    /// <summary>Continues the trace of the request that wrote the row, when it was recorded.</summary>
    private static Activity? StartRelayActivity(OutboxRow row)
    {
        ActivityContext.TryParse(row.TraceParent, null, isRemote: true, out var parent);
        var activity = Source.StartActivity("outbox relay", ActivityKind.Internal, parent);
        activity?.SetTag("messaging.destination.name", row.OutboxType);
        activity?.SetTag("outbox.id", row.Id.ToString());
        return activity;
    }
}

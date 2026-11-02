using Ingestion.Application.Errors;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Application.Interfaces.Services;
using Ingestion.Infrastructure.Write.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ingestion.Infrastructure.Write.HostedServices;

/// <summary>
/// Polls each enabled source on its own interval (<c>Polling:&lt;Source&gt;:IntervalMinutes</c>) for every
/// subscription. A failing subscription or a rejected reading is logged and counted without stopping the others;
/// the next poll asks for an overlapping window, and readings already ingested are skipped by their provider id.
/// </summary>
public class SensorPollingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<PollingSourceOptions> options,
    PollingMetrics metrics,
    TimeProvider timeProvider,
    ILogger<SensorPollingHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, DateTime> _nextPoll = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Sensor polling started for {Sources}", string.Join(", ",
            PollingSources.All.Where(source => options.Get(source).Enabled)));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollDueSourcesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Sensor polling pass failed");
            }

            await Task.Delay(Tick, timeProvider, stoppingToken);
        }
    }

    /// <summary>One pass: polls the sources whose interval has elapsed.</summary>
    public async Task PollDueSourcesAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var scope = scopeFactory.CreateAsyncScope();
        var collections = scope.ServiceProvider.GetRequiredService<ISensorCollectionService>();

        foreach (var client in scope.ServiceProvider.GetServices<ISensorPollingClient>())
        {
            var source = options.Get(client.Source);
            if (!source.Enabled || _nextPoll.GetValueOrDefault(client.Source) > now) continue;

            _nextPoll[client.Source] = now.AddMinutes(source.IntervalMinutes);
            var window = new Application.DTO.PollingWindow(now.AddHours(-source.LookbackHours), now);
            foreach (var subscription in source.Subscriptions)
                await PollSubscriptionAsync(client, subscription, window, collections, cancellationToken);
        }
    }

    private async Task PollSubscriptionAsync(ISensorPollingClient client,
        Application.DTO.PollingSubscription subscription, Application.DTO.PollingWindow window,
        ISensorCollectionService collections, CancellationToken cancellationToken)
    {
        List<Application.DTO.SensorCollectionDTO> readings;
        try
        {
            readings = await client.FetchAsync(subscription, window, cancellationToken);
            metrics.Request(client.Source, success: true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException ||
                                          !cancellationToken.IsCancellationRequested)
        {
            metrics.Request(client.Source, success: false);
            logger.LogWarning(exception, "Polling {Source} failed for data source {DataSourceId}", client.Source,
                subscription.DataSourceId);
            return;
        }

        foreach (var reading in readings)
        {
            try
            {
                await collections.ProcessPolledCollectionAsync(reading, cancellationToken);
                metrics.Reading(client.Source, "processed");
            }
            catch (Exception exception) when (exception is ArgumentException or IIngestionError)
            {
                // The data source does not accept this reading (unit, permissions, missing data source): a
                // configuration problem, reported but not retried until the configuration changes.
                metrics.Reading(client.Source, "rejected");
                logger.LogWarning("Polled {Source} reading {SourceEventId} rejected: {Reason}", client.Source,
                    reading.SourceEventId, exception.Message);
            }
            catch (Exception exception) when (exception is not OperationCanceledException ||
                                              !cancellationToken.IsCancellationRequested)
            {
                // Stores unavailable: the claim was released, and the next poll's window covers this reading again.
                metrics.Reading(client.Source, "failed");
                logger.LogError(exception, "Polled {Source} reading {SourceEventId} could not be stored",
                    client.Source, reading.SourceEventId);
            }
        }
    }
}

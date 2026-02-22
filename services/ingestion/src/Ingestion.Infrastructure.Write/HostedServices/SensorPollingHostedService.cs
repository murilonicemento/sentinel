using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Application.Interfaces.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ingestion.Infrastructure.Write.HostedServices;

public class SensorPollingHostedService : BackgroundService
{
    private readonly ISensorPollingClient _sensorPollingClient;
    private readonly ISensorCollectionService _sensorCollectionService;
    private readonly ILogger<SensorPollingHostedService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public SensorPollingHostedService(
        ISensorPollingClient sensorPollingClient,
        ISensorCollectionService collectionService,
        ILogger<SensorPollingHostedService> logger
    )
    {
        _sensorPollingClient = sensorPollingClient;
        _sensorCollectionService = collectionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sensor Polling Worker iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var collections = await _sensorPollingClient.FetchAsync(stoppingToken);

                if (collections.Count > 0)
                {
                    foreach (var result in collections)
                    {
                        await _sensorCollectionService.ProcessSensorCollection<object>(
                            result.DataSourceId,
                            result.TenantId,
                            result.CollectedAt,
                            result.Payload,
                            result.Samples,
                            result.Domain.ToString(),
                            stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while polling sensor collection.");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Sensor Polling Worker ended.");
    }
}
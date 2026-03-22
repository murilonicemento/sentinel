using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Application.Interfaces.Services;
using Ingestion.Application.Services;
using Ingestion.Infrastructure.Write.HttpClients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ingestion.Infrastructure.Write.HostedServices;

public class SensorPollingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnumerable<ISensorPollingClient> _sensorPollingClients;
    private readonly ILogger<SensorPollingHostedService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public SensorPollingHostedService(
        IServiceScopeFactory scopeFactory,
        ISensorPollingClientFactory sensorPollingClientFactory,
        ILogger<SensorPollingHostedService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _sensorPollingClients = sensorPollingClientFactory.GetAllClients();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sensor Polling Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var client in _sensorPollingClients)
                {
                    var collections = await client.FetchAsync(stoppingToken);

                    if (collections.Count <= 0) continue;

                    using var scope = _scopeFactory.CreateScope();
                    var sensorCollectionService = scope.ServiceProvider.GetRequiredService<ISensorCollectionService>();

                    foreach (var result in collections)
                    {
                        await sensorCollectionService.ProcessSensorCollection<object>(
                            result.DataSourceId,
                            result.TenantId,
                            result.CollectedAt.GetValueOrDefault(DateTime.UtcNow),
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
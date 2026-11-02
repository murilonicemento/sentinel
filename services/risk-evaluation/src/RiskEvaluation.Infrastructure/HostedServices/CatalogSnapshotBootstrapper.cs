using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Interfaces.HttpClients;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Infrastructure.HostedServices;

/// <summary>
/// At startup, copies the latest recorded catalog version when its snapshot is missing (for example after a deploy that
/// introduced snapshots). Failures are logged and left to the next publication; they never block the host.
/// </summary>
public sealed class CatalogSnapshotBootstrapper : BackgroundService
{
    private readonly ILogger<CatalogSnapshotBootstrapper> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public CatalogSnapshotBootstrapper(IServiceScopeFactory scopeFactory, ILogger<CatalogSnapshotBootstrapper> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var versions = scope.ServiceProvider.GetRequiredService<ICatalogVersionRepository>();
            var snapshots = scope.ServiceProvider.GetRequiredService<ICatalogSnapshotRepository>();

            var latestVersion = await versions.GetLatestAsync(stoppingToken);
            if (latestVersion is null) return;

            var snapshot = await snapshots.GetLatestAsync(stoppingToken);
            if (snapshot is not null && snapshot.Version >= latestVersion.Version) return;

            var fetched = await scope.ServiceProvider.GetRequiredService<IRiskCatalogClient>()
                .FetchSnapshotAsync(latestVersion.Version, stoppingToken);
            await snapshots.SaveAsync(fetched, stoppingToken);
            _logger.LogInformation("Copied risk catalog version {Version} at startup", fetched.Version);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Could not copy the latest risk catalog version at startup; evaluations use the previous snapshot");
        }
    }
}

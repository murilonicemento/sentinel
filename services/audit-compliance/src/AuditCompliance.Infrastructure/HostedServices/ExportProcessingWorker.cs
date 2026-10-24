using AuditCompliance.Application.Exports;
using AuditCompliance.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditCompliance.Infrastructure.HostedServices;

public sealed class ExportProcessingWorker : BackgroundService
{
    private const int MaxExportsPerCycle = 10;

    private readonly ILogger<ExportProcessingWorker> _logger;
    private readonly ExportOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    public ExportProcessingWorker(IServiceScopeFactory scopeFactory, IOptions<ExportOptions> options,
        ILogger<ExportProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));

        do
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Export processing cycle failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ProcessAuditExportsHandler>();

        for (var i = 0; i < MaxExportsPerCycle; i++)
            if (!await handler.ProcessNextAsync(cancellationToken))
                break;

        await handler.ExpireFilesAsync(cancellationToken);
    }
}

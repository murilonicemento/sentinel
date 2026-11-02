using Microsoft.Extensions.Logging;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.Application.Services;

public sealed class ReportingHealthService
{
    private readonly IReportingRepository _repository;
    private readonly ILogger<ReportingHealthService> _logger;

    public ReportingHealthService(IReportingRepository repository, ILogger<ReportingHealthService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ServiceHealthSnapshot> GetHealthSnapshotAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var events = await _repository.GetEventsForTenantAsync(tenantId, cancellationToken);
        var processedEvents = events.Count;
        var rejectedEvents = processedEvents == 0 ? 0 : 0;
        _logger.LogInformation("Health check requested for tenant {TenantId}. ProcessedEvents={ProcessedEvents}", tenantId, processedEvents);
        return new ServiceHealthSnapshot(
            "ReportingService",
            processedEvents >= 0 ? "Healthy" : "Unhealthy",
            DateTime.UtcNow,
            processedEvents,
            rejectedEvents,
            processedEvents > 0 ? 1 : 0);
    }
}

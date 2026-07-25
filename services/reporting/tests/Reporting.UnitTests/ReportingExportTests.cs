using Reporting.Application.Services;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.UnitTests;

public class ReportingExportTests
{
    [Fact]
    public async Task ExportAsync_ReturnsCsvForFilteredEvents()
    {
        var repository = new FakeReportingRepository();
        var sut = new ReportingQueryService(repository);

        await repository.ProcessEventAsync(new ReportingEvent("evt-1", "AlertTriggered", "tenant-a", "north", "High", "sms", 0.82, "Success"));
        await repository.ProcessEventAsync(new ReportingEvent("evt-2", "NotificationSent", "tenant-a", "south", "Medium", "push", 0.51, "Success"));

        var csv = await sut.ExportAsync("tenant-a", new ReportFilter(null, "north"), "csv");

        Assert.Contains("AlertTriggered", csv);
        Assert.DoesNotContain("NotificationSent", csv);
    }

    private sealed class FakeReportingRepository : IReportingRepository
    {
        private readonly List<ReportingEvent> _events = [];

        public Task ProcessEventAsync(ReportingEvent reportingEvent, CancellationToken cancellationToken = default)
        {
            _events.Add(reportingEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ReportingEvent>> GetEventsForTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<ReportingEvent>>(_events
                .Where(e => string.Equals(e.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                .ToArray());
        }
    }
}

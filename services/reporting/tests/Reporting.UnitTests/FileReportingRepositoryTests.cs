using Reporting.Domain.Entities;
using Reporting.Infrastructure.Persistence;

namespace Reporting.UnitTests;

public class FileReportingRepositoryTests
{
    [Fact]
    public async Task ProcessEventAsync_PersistsEventAndReloadsFromDisk()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"reporting-repo-{Guid.NewGuid():N}.json");

        try
        {
            var repository = new FileReportingRepository(filePath);
            var reportingEvent = new ReportingEvent(
                "evt-1",
                "AlertTriggered",
                "tenant-a",
                "north",
                "High",
                "sms",
                0.82,
                "Success");

            await repository.ProcessEventAsync(reportingEvent);

            var reloadedRepository = new FileReportingRepository(filePath);
            var events = await reloadedRepository.GetEventsForTenantAsync("tenant-a");

            var persistedEvent = Assert.Single(events);
            Assert.Equal("evt-1", persistedEvent.EventId);
            Assert.Equal("tenant-a", persistedEvent.TenantId);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ProcessEventAsync_IgnoresDuplicateEventId()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"reporting-repo-{Guid.NewGuid():N}.json");

        try
        {
            var repository = new FileReportingRepository(filePath);
            await repository.ProcessEventAsync(new ReportingEvent(
                "evt-dup",
                "AlertTriggered",
                "tenant-a",
                "north",
                "High",
                "sms",
                0.82,
                "Success"));

            await repository.ProcessEventAsync(new ReportingEvent(
                "evt-dup",
                "NotificationSent",
                "tenant-a",
                "north",
                "High",
                "sms",
                0.91,
                "Failed"));

            var events = await repository.GetEventsForTenantAsync("tenant-a");
            var persistedEvent = Assert.Single(events);
            Assert.Equal("AlertTriggered", persistedEvent.EventType);
            Assert.Equal("Success", persistedEvent.Status);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}

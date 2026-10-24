using System.Collections.Concurrent;
using System.Text.Json;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.Infrastructure.Persistence;

public sealed class FileReportingRepository : IReportingRepository
{
    private readonly string _filePath;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, List<ReportingEvent>> _tenantEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _processedEventIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public FileReportingRepository(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(AppContext.BaseDirectory, "reporting-events.json")
            : filePath;
    }

    public Task ProcessEventAsync(ReportingEvent reportingEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportingEvent);

        if (string.IsNullOrWhiteSpace(reportingEvent.TenantId))
        {
            throw new ArgumentException("A tenant identifier is required.", nameof(reportingEvent));
        }

        EnsureLoaded();

        lock (_syncRoot)
        {
            if (!_processedEventIds.Add(reportingEvent.EventId))
            {
                return Task.CompletedTask;
            }

            if (!_tenantEvents.TryGetValue(reportingEvent.TenantId, out var tenantEvents))
            {
                tenantEvents = [];
                _tenantEvents[reportingEvent.TenantId] = tenantEvents;
            }

            tenantEvents.Add(reportingEvent);
            Persist();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<ReportingEvent>> GetEventsForTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Task.FromResult<IReadOnlyCollection<ReportingEvent>>(Array.Empty<ReportingEvent>());
        }

        EnsureLoaded();

        lock (_syncRoot)
        {
            if (_tenantEvents.TryGetValue(tenantId, out var tenantEvents))
            {
                return Task.FromResult<IReadOnlyCollection<ReportingEvent>>(tenantEvents.ToArray());
            }

            return Task.FromResult<IReadOnlyCollection<ReportingEvent>>(Array.Empty<ReportingEvent>());
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_loaded)
            {
                return;
            }

            if (File.Exists(_filePath))
            {
                var raw = File.ReadAllText(_filePath);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var events = JsonSerializer.Deserialize<List<ReportingEvent>>(raw, CreateJsonOptions()) ?? [];
                    foreach (var evt in events)
                    {
                        _processedEventIds.Add(evt.EventId);
                        if (!_tenantEvents.TryGetValue(evt.TenantId, out var tenantEvents))
                        {
                            tenantEvents = [];
                            _tenantEvents[evt.TenantId] = tenantEvents;
                        }

                        tenantEvents.Add(evt);
                    }
                }
            }

            _loaded = true;
        }
    }

    private void Persist()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        var events = _tenantEvents.Values.SelectMany(x => x).ToList();
        var payload = JsonSerializer.Serialize(events, CreateJsonOptions());
        File.WriteAllText(tempPath, payload);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static JsonSerializerOptions CreateJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}

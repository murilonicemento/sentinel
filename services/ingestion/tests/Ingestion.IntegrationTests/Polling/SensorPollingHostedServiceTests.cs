using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Application.Interfaces.Services;
using Ingestion.Infrastructure.Write.HostedServices;
using Ingestion.Infrastructure.Write.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ingestion.IntegrationTests.Polling;

public class SensorPollingHostedServiceTests
{
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly RecordingCollections _collections = new();

    private static PollingSubscription Subscription(string name) => new()
    {
        TenantId = Guid.NewGuid(),
        DataSourceId = Guid.NewGuid(),
        Latitude = -23.5,
        Longitude = -46.6,
        StationId = name
    };

    private SensorPollingHostedService Service(Dictionary<string, PollingSourceOptions> sources,
        params ISensorPollingClient[] clients)
    {
        var services = new ServiceCollection().AddSingleton<ISensorCollectionService>(_collections);
        foreach (var client in clients) services.AddSingleton(client);
        var monitor = new StaticOptionsMonitor(sources);
        return new SensorPollingHostedService(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            monitor, new PollingMetrics(), _time, NullLogger<SensorPollingHostedService>.Instance);
    }

    private static PollingSourceOptions Enabled(int intervalMinutes, params PollingSubscription[] subscriptions) =>
        new() { Enabled = true, IntervalMinutes = intervalMinutes, LookbackHours = 6, Subscriptions = [.. subscriptions] };

    [Fact]
    public async Task EachSource_IsPolledOnItsOwnInterval_WithALookbackWindow()
    {
        var earthquake = new FakeClient(PollingSources.Earthquake);
        var rainfall = new FakeClient(PollingSources.Rainfall);
        var service = Service(new()
        {
            [PollingSources.Earthquake] = Enabled(15, Subscription("a")),
            [PollingSources.Rainfall] = Enabled(180, Subscription("b"))
        }, earthquake, rainfall);

        await service.PollDueSourcesAsync(default);
        _time.Advance(TimeSpan.FromMinutes(16));
        await service.PollDueSourcesAsync(default);

        Assert.Equal(2, earthquake.Windows.Count);
        Assert.Single(rainfall.Windows);
        Assert.Equal(TimeSpan.FromHours(6), earthquake.Windows[0].To - earthquake.Windows[0].From);
    }

    [Fact]
    public async Task DisabledSource_IsNeverCalled()
    {
        var fire = new FakeClient(PollingSources.Fire);

        await Service(new() { [PollingSources.Fire] = new PollingSourceOptions() }, fire).PollDueSourcesAsync(default);

        Assert.Empty(fire.Windows);
    }

    [Fact]
    public async Task AFailingSubscription_DoesNotStopTheOthers()
    {
        var failing = Subscription("failing");
        var healthy = Subscription("healthy");
        var client = new FakeClient(PollingSources.Earthquake, failFor: failing.StationId);

        await Service(new() { [PollingSources.Earthquake] = Enabled(15, failing, healthy) }, client)
            .PollDueSourcesAsync(default);

        Assert.Equal(["evt-healthy"], _collections.Processed);
    }

    [Fact]
    public async Task ARejectedReading_DoesNotStopTheRestOfTheBatch()
    {
        _collections.RejectedIds.Add("evt-first");
        var client = new FakeClient(PollingSources.Earthquake) { ReadingsPerPoll = ["evt-first", "evt-second"] };

        await Service(new() { [PollingSources.Earthquake] = Enabled(15, Subscription("a")) }, client)
            .PollDueSourcesAsync(default);

        Assert.Equal(["evt-second"], _collections.Processed);
    }

    private sealed class FakeClient(string source, string? failFor = null) : ISensorPollingClient
    {
        public string Source => source;
        public List<PollingWindow> Windows { get; } = [];
        public string[]? ReadingsPerPoll { get; init; }

        public Task<List<SensorCollectionDTO>> FetchAsync(PollingSubscription subscription, PollingWindow window,
            CancellationToken cancellationToken)
        {
            Windows.Add(window);
            if (subscription.StationId == failFor) throw new HttpRequestException("provider down");
            var ids = ReadingsPerPoll ?? [$"evt-{subscription.StationId}"];
            return Task.FromResult(ids.Select(id => new SensorCollectionDTO
            {
                TenantId = subscription.TenantId,
                DataSourceId = subscription.DataSourceId,
                CollectedAt = window.To,
                SourceEventId = id
            }).ToList());
        }
    }

    private sealed class RecordingCollections : ISensorCollectionService
    {
        public List<string> Processed { get; } = [];
        public HashSet<string> RejectedIds { get; } = [];

        public Task<Guid> ProcessSensorCollection(Guid dataSourceId, Guid tenantId, DateTime collectedAt,
            string payload, List<SampleSensorDTO> samples, string domain, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Guid> ProcessPolledCollectionAsync(SensorCollectionDTO collection,
            CancellationToken cancellationToken)
        {
            if (RejectedIds.Contains(collection.SourceEventId!))
                throw new ArgumentException("Invalid unit type for measurement type: Rainfall");
            Processed.Add(collection.SourceEventId!);
            return Task.FromResult(collection.DataSourceId);
        }
    }

    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class StaticOptionsMonitor(Dictionary<string, PollingSourceOptions> sources)
        : IOptionsMonitor<PollingSourceOptions>
    {
        public PollingSourceOptions CurrentValue => new();
        public PollingSourceOptions Get(string? name) => sources.GetValueOrDefault(name ?? "") ?? new();
        public IDisposable? OnChange(Action<PollingSourceOptions, string?> listener) => null;
    }
}

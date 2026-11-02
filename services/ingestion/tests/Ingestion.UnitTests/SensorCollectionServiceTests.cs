using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using Ingestion.Application.Interfaces.Deduplicators;
using Ingestion.Application.Interfaces.Providers;
using Ingestion.Application.Interfaces.Repositories;
using Ingestion.Application.Interfaces.Services;
using Ingestion.Application.Services;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Ingestion.UnitTests;

public class SensorCollectionServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime CollectedAt = new(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IEventDeduplicator> _deduplicator = new();
    private readonly Mock<IDetectedEventReadRepository> _readModel = new();
    private readonly Mock<IOutboxRepository> _outbox = new();
    private readonly Mock<IObjectStorageProvider> _storage = new();
    private readonly Mock<IDataCollectionRepository> _collections = new();
    private readonly Mock<ISampleSensorRepository> _samples = new();
    private readonly InlineUnitOfWork _unitOfWork = new();
    private readonly DataSource _dataSource;

    public SensorCollectionServiceTests()
    {
        _dataSource = new DataSource(Guid.NewGuid(), "rain", "https://sensors.example.com", "Sensor", "Rainfall",
            "Hourly", TenantId)
        {
            EventPermissions = [new EventTypePermission { EventDomain = "Climatic", EventType = "Rainfall" }]
        };
        _deduplicator.Setup(d => d.TryBeginAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).ReturnsAsync(true);
        _storage.Setup(s => s.UploadJsonAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UploadResultDTO());
    }

    private SensorCollectionService Service()
    {
        var dataSources = new Mock<IDataSourceRepository>();
        dataSources.Setup(r => r.GetByIdAndTenantId(_dataSource.Id, TenantId)).Returns(_dataSource);
        var geospatial = new Mock<IGeospatialValidationService>();
        geospatial.Setup(g => g.ValidateCoordinatesAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return new SensorCollectionService(dataSources.Object, _collections.Object, _samples.Object, _unitOfWork,
            _deduplicator.Object, _storage.Object, _outbox.Object, _readModel.Object, geospatial.Object,
            NullLogger<SensorCollectionService>.Instance);
    }

    private Task<Guid> ProcessAsync() => Service().ProcessSensorCollection(_dataSource.Id, TenantId, CollectedAt,
        "{\"raw\":true}",
        [
            new SampleSensorDTO { SensorValue = 60, Unit = "mm", Latitude = -23.5, Longitude = -46.6 },
            new SampleSensorDTO { SensorValue = 70, Unit = "mm", Latitude = -22.9, Longitude = -43.2 }
        ], "Climatic", CancellationToken.None);

    [Fact]
    public async Task EventIds_AreDeterministicAndSharedByProjectionAndOutbox()
    {
        var projected = new List<Guid>();
        var published = new List<Guid>();
        _readModel.Setup(r => r.AddAsync(It.IsAny<DetectedEventDTO>(), It.IsAny<CancellationToken>()))
            .Callback<DetectedEventDTO, CancellationToken>((e, _) => projected.Add(e.EventId));
        _outbox.Setup(o => o.RegisterAsync(It.Is<OutboxMessage>(m => m.OutboxType != TenantUsageEvent.Topic)))
            .Callback<OutboxMessage>(m => published.Add(JsonDocument.Parse(m.Payload).RootElement
                .GetProperty("EventId").GetGuid()))
            .ReturnsAsync(Guid.NewGuid());

        await ProcessAsync();
        await ProcessAsync();

        Assert.Equal(4, projected.Count);
        Assert.Equal(projected, published);
        Assert.Equal(projected[..2], projected[2..]);
        Assert.NotEqual(projected[0], projected[1]);
    }

    [Fact]
    public async Task Collection_WritesCollectionSamplesAndOutboxInOneUnitOfWork()
    {
        DataCollection? collection = null;
        List<SampleSensor> samples = [];
        var outboxIds = new List<Guid>();
        var eventIds = new List<Guid>();
        _collections.Setup(r => r.RegisterAsync(It.IsAny<DataCollection>()))
            .Callback<DataCollection>(c => collection = c).ReturnsAsync(Guid.NewGuid());
        _samples.Setup(r => r.RegisterManyAsync(It.IsAny<IEnumerable<SampleSensor>>()))
            .Callback<IEnumerable<SampleSensor>>(s => samples = s.ToList()).Returns(Task.CompletedTask);
        _outbox.Setup(o => o.RegisterAsync(It.IsAny<OutboxMessage>()))
            .Callback<OutboxMessage>(m =>
            {
                outboxIds.Add(m.Id);
                eventIds.Add(JsonDocument.Parse(m.Payload).RootElement.GetProperty("EventId").GetGuid());
            })
            .ReturnsAsync(Guid.NewGuid());

        await ProcessAsync();

        Assert.Equal(1, _unitOfWork.Executions);
        Assert.NotNull(collection);
        Assert.Equal(2, samples.Count);
        Assert.All(samples, sample => Assert.Equal(collection.Id, sample.DataCollectionId));
        Assert.Equal(eventIds, outboxIds);
        Assert.Equal(3, outboxIds.Count);
    }

    [Fact]
    public async Task Collection_CountsItsEventsAsTenantUsageOnceAcrossRetries()
    {
        var usages = new List<OutboxMessage>();
        _outbox.Setup(o => o.RegisterAsync(It.Is<OutboxMessage>(m => m.OutboxType == TenantUsageEvent.Topic)))
            .Callback<OutboxMessage>(usages.Add)
            .ReturnsAsync(Guid.NewGuid());

        await ProcessAsync();
        await ProcessAsync();

        Assert.Equal(2, usages.Count);
        Assert.Equal(usages[0].Id, usages[1].Id);
        var usage = JsonSerializer.Deserialize<TenantUsageEvent>(usages[0].Payload)!;
        Assert.Equal(new TenantUsageEvent(TenantId, 2, 0, 0, 0, usages[0].Id), usage);
    }

    [Fact]
    public async Task CollectionWithoutEvents_RecordsNoUsage()
    {
        var geospatial = new Mock<IGeospatialValidationService>();
        geospatial.Setup(g => g.ValidateCoordinatesAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var dataSources = new Mock<IDataSourceRepository>();
        dataSources.Setup(r => r.GetByIdAndTenantId(_dataSource.Id, TenantId)).Returns(_dataSource);
        var service = new SensorCollectionService(dataSources.Object, _collections.Object, _samples.Object,
            _unitOfWork, _deduplicator.Object, _storage.Object, _outbox.Object, _readModel.Object, geospatial.Object,
            NullLogger<SensorCollectionService>.Instance);

        await service.ProcessSensorCollection(_dataSource.Id, TenantId, CollectedAt, "{\"raw\":true}",
            [new SampleSensorDTO { SensorValue = 60, Unit = "mm", Latitude = 0, Longitude = 0 }], "Climatic",
            CancellationToken.None);

        _outbox.Verify(o => o.RegisterAsync(It.IsAny<OutboxMessage>()), Times.Never);
    }

    [Fact]
    public async Task RetriedCollection_ReusesTheCollectionAndSampleIds()
    {
        var collectionIds = new List<Guid>();
        var sampleIds = new List<Guid>();
        _collections.Setup(r => r.RegisterAsync(It.IsAny<DataCollection>()))
            .Callback<DataCollection>(c => collectionIds.Add(c.Id)).ReturnsAsync(Guid.NewGuid());
        _samples.Setup(r => r.RegisterManyAsync(It.IsAny<IEnumerable<SampleSensor>>()))
            .Callback<IEnumerable<SampleSensor>>(s => sampleIds.AddRange(s.Select(sample => sample.Id)))
            .Returns(Task.CompletedTask);

        await ProcessAsync();
        await ProcessAsync();

        Assert.Equal(collectionIds[0], collectionIds[1]);
        Assert.Equal(sampleIds[..2], sampleIds[2..]);
        Assert.NotEqual(sampleIds[0], sampleIds[1]);
    }

    private SensorCollectionDTO Polled(string? sourceEventId) => new()
    {
        DataSourceId = _dataSource.Id,
        TenantId = TenantId,
        CollectedAt = CollectedAt,
        SourceEventId = sourceEventId,
        Payload = "{\"raw\":true}",
        Samples = [new SampleSensorDTO { SensorValue = 60, Unit = "mm", Latitude = -23.5, Longitude = -46.6 }]
    };

    [Fact]
    public async Task PolledReading_IgnoresTheCollectionFrequency_AndIsKeyedByTheProviderId()
    {
        _dataSource.DataCollections =
            [new DataCollection(Guid.NewGuid(), _dataSource.Id, DateTime.UtcNow.AddMinutes(-5), "{}", TenantId)];
        string? key = null;
        _deduplicator.Setup(d => d.TryBeginAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Callback<string, TimeSpan>((k, _) => key = k).ReturnsAsync(true);

        await Assert.ThrowsAsync<ArgumentException>(ProcessAsync);
        await Service().ProcessPolledCollectionAsync(Polled("us7000a"), CancellationToken.None);

        Assert.Equal($"ing:{TenantId}:{_dataSource.Id}:src:us7000a", key);
        Assert.Equal(1, _unitOfWork.Executions);
    }

    [Fact]
    public async Task PolledReading_WithoutProviderId_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service().ProcessPolledCollectionAsync(Polled(null), CancellationToken.None));
        _storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DuplicateCollection_IsSkippedBeforeStoringAnything()
    {
        _deduplicator.Setup(d => d.TryBeginAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).ReturnsAsync(false);

        await ProcessAsync();

        _storage.VerifyNoOtherCalls();
        _outbox.VerifyNoOtherCalls();
        Assert.Equal(0, _unitOfWork.Executions);
    }

    [Fact]
    public async Task FailedCollection_ReleasesTheClaimAndIsNotMarkedProcessed()
    {
        _outbox.Setup(o => o.RegisterAsync(It.IsAny<OutboxMessage>())).ThrowsAsync(new TimeoutException("db"));

        await Assert.ThrowsAsync<TimeoutException>(ProcessAsync);

        _deduplicator.Verify(d => d.AbandonAsync(It.IsAny<string>()), Times.Once);
        _deduplicator.Verify(d => d.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public void DeterministicEventId_IsAVersion8RfcGuid()
    {
        var id = DeterministicEventId.For("ing:tenant:source:20261026100000", 0);

        Assert.Equal(id, DeterministicEventId.For("ing:tenant:source:20261026100000", 0));
        Assert.Equal('8', id.ToString()[14]);
        Assert.Contains(id.ToString()[19], "89ab");
    }
}

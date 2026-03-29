using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.Deduplicators;
using Ingestion.Application.Interfaces.Providers;
using Ingestion.Application.Interfaces.Services;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Enums;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Outbox;
using Ingestion.Domain.ValueObjects;
using Ingestion.Infrastructure.Read.Persistence.DbContext;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ingestion.Application.Services;

public class SensorCollectionService : ISensorCollectionService
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly IDataCollectionRepository _dataCollectionRepository;
    private readonly IEventDeduplicator _eventDeduplicator;
    private readonly IObjectStorageProvider _minioProvider;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ReadDbContext _readDbContext;
    private readonly IConfiguration _configuration;
    private readonly IGeospatialValidationService _geospatialValidationService;
    private readonly ILogger<SensorCollectionService> _logger;

    public SensorCollectionService(
        IDataSourceRepository dataSourceRepository,
        IDataCollectionRepository dataCollectionRepository,
        IEventDeduplicator eventDeduplicator,
        IObjectStorageProvider minioProvider,
        IOutboxRepository outboxRepository,
        ReadDbContext readDbContext,
        IConfiguration configuration,
        IGeospatialValidationService geospatialValidationService,
        ILogger<SensorCollectionService> logger)
    {
        _dataSourceRepository = dataSourceRepository;
        _dataCollectionRepository = dataCollectionRepository;
        _eventDeduplicator = eventDeduplicator;
        _minioProvider = minioProvider;
        _outboxRepository = outboxRepository;
        _readDbContext = readDbContext;
        _configuration = configuration;
        _geospatialValidationService = geospatialValidationService;
        _logger = logger;
    }

    public async Task<Guid> ProcessSensorCollection<TEvent>(Guid dataSourceId, Guid tenantId, DateTime collectedAt,
        string payload,
        List<SampleSensorDTO> samples, string domain, CancellationToken cancellationToken) where TEvent : class
    {
        if (string.IsNullOrEmpty(payload))
            throw new ArgumentException("Payload can't be blank.");
        if (samples.Any(s => string.IsNullOrEmpty(s.Unit)))
            throw new ArgumentException("Samples unit can't be blank.");

        var dataSource = _dataSourceRepository.GetByIdAndTenantId(dataSourceId, tenantId)
                         ?? throw new KeyNotFoundException(
                             $"DataSource or tenant not found. DataSource Id: {dataSourceId}, TenantId: {tenantId}");

        _logger.LogInformation("Datasource info. Id: {id}; TenantId: {tenantId}", dataSource.Id, dataSource.TenantId);

        var isUnitValid = samples.Select(s => MeasurementType.From(dataSource.MeasurementType).IsValidUnit(s.Unit))
            .Any(valid => valid);
        if (!isUnitValid)
            throw new ArgumentException($"Invalid unit type for measurement type: {dataSource.MeasurementType}");

        var lastDataCollected = dataSource.DataCollections.OrderByDescending(x => x.CollectedAt).FirstOrDefault();

        _logger.LogInformation(
            "Last data collected from DataSource with id {id} and name: {name}. Json {lastDataCollected}",
            dataSource.Id,
            dataSource.Name,
            JsonSerializer.Serialize(lastDataCollected)
        );

        if (lastDataCollected != null)
        {
            var isFreqValid = CollectionFrequencyType.From(dataSource.CollectionFrequency)
                .IsValidFrequency(lastDataCollected.CollectedAt);
            if (!isFreqValid)
                throw new ArgumentException($"Collection frequency mismatch: {dataSource.CollectionFrequency}");
        }

        var dedupKey = $"ing:{tenantId}:{dataSourceId}:{collectedAt:yyyyMMddHHmmss}";
        
        if (await _eventDeduplicator.IsDuplicateAsync(dedupKey))
        {
            _logger.LogInformation("Event duplicated. Key: {dedupKey}", dedupKey);
            return dataSourceId;
        }

        var collectionGuid = Guid.NewGuid();
        var objectName = $"raw/{collectionGuid}.json";
        var putResponse =
            await _minioProvider.UploadJsonAsync(_configuration["MinIO:BucketName"]!, objectName, payload);

        var dataCollection = new DataCollection(collectionGuid, dataSourceId, collectedAt,
            JsonSerializer.Serialize(putResponse), tenantId);
        await _dataCollectionRepository.RegisterAsync(dataCollection);

        var collectionName = domain == "Climatic" ? "climaticEvents" : "disasterEvents";
        var mongoCollection = _readDbContext.GetCollection<TEvent>(collectionName);

        foreach (var sample in samples)
        {
            var isValidCoords =
                await _geospatialValidationService.ValidateCoordinatesAsync(sample.Latitude, sample.Longitude,
                    cancellationToken);
            if (!isValidCoords)
            {
                _logger.LogWarning("Invalid coordinates. Skipping sample. Lat: {Lat}, Lng: {Lng}", sample.Latitude,
                    sample.Longitude);
                continue;
            }

            var eventType = domain == "Climatic"
                ? dataSource.MapValueToEventType<ClimaticEventEnum>(sample.SensorValue).ToString()
                : dataSource.MapValueToEventType<DisasterEventEnum>(sample.SensorValue).ToString();

            if (!dataSource.CanSendEvent(domain, eventType))
            {
                _logger.LogWarning("DataSource {DataSourceId} not authorized for event. Ignoring.", dataSource.Id);
                continue;
            }

            var intensity = MeasurementType.From(dataSource.MeasurementType).CalculateIntensity(sample.SensorValue);
            var evt = Activator.CreateInstance(typeof(TEvent),
                collectionGuid,
                eventType,
                intensity,
                sample.Latitude,
                sample.Longitude,
                collectedAt) as TEvent;

            if (evt == null) continue;

            _logger.LogInformation(
                "Inserting climatic event to mongodb. Json event: {evt}",
                evt
            );
            await mongoCollection.InsertOneAsync(evt, cancellationToken);

            var outbox = new OutboxMessage(Guid.NewGuid(), collectionGuid,
                $"{domain.ToLower()}-event-detected", JsonSerializer.Serialize(evt));
            await _outboxRepository.RegisterAsync(outbox);
        }

        await _eventDeduplicator.MarkAsProcessedAsync(dedupKey, TimeSpan.FromMinutes(5));
        return dataSourceId;
    }
}
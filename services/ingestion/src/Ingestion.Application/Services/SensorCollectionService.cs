using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using Ingestion.Application.Errors;
using Ingestion.Application.Interfaces.Deduplicators;
using Ingestion.Application.Interfaces.Providers;
using Ingestion.Application.Interfaces.Repositories;
using Ingestion.Application.Interfaces.Services;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Enums;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Outbox;
using Ingestion.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Ingestion.Application.Services;

public class SensorCollectionService : ISensorCollectionService
{
    /// <summary>How long a claim on a collection survives a crashed process before another attempt may take it.</summary>
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(2);

    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly IDataCollectionRepository _dataCollectionRepository;
    private readonly ISampleSensorRepository _sampleSensorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventDeduplicator _eventDeduplicator;
    private readonly IObjectStorageProvider _minioProvider;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IDetectedEventReadRepository _detectedEventReadRepository;
    private readonly IGeospatialValidationService _geospatialValidationService;
    private readonly ILogger<SensorCollectionService> _logger;

    public SensorCollectionService(
        IDataSourceRepository dataSourceRepository,
        IDataCollectionRepository dataCollectionRepository,
        ISampleSensorRepository sampleSensorRepository,
        IUnitOfWork unitOfWork,
        IEventDeduplicator eventDeduplicator,
        IObjectStorageProvider minioProvider,
        IOutboxRepository outboxRepository,
        IDetectedEventReadRepository detectedEventReadRepository,
        IGeospatialValidationService geospatialValidationService,
        ILogger<SensorCollectionService> logger)
    {
        _dataSourceRepository = dataSourceRepository;
        _dataCollectionRepository = dataCollectionRepository;
        _sampleSensorRepository = sampleSensorRepository;
        _unitOfWork = unitOfWork;
        _eventDeduplicator = eventDeduplicator;
        _minioProvider = minioProvider;
        _outboxRepository = outboxRepository;
        _detectedEventReadRepository = detectedEventReadRepository;
        _geospatialValidationService = geospatialValidationService;
        _logger = logger;
    }

    public async Task<Guid> ProcessSensorCollection(Guid dataSourceId, Guid tenantId, DateTime collectedAt,
        string payload,
        List<SampleSensorDTO> samples, string domain, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(payload))
            throw new ArgumentException("Payload can't be blank.");
        if (samples.Any(s => string.IsNullOrEmpty(s.Unit)))
            throw new ArgumentException("Samples unit can't be blank.");

        var dataSource = _dataSourceRepository.GetByIdAndTenantId(dataSourceId, tenantId)
                         ?? throw new IngestionReferenceNotFoundException(IngestionErrorCodes.DataSourceNotFound,
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
        
        if (!await _eventDeduplicator.TryBeginAsync(dedupKey, ProcessingLease))
        {
            _logger.LogInformation("Event duplicated. Key: {dedupKey}", dedupKey);
            return dataSourceId;
        }

        try
        {
            await RegisterCollectionAsync(dataSource, tenantId, collectedAt, payload, samples, domain, dedupKey,
                cancellationToken);
        }
        catch
        {
            // Releases the claim so the collection can be retried; event ids are deterministic, so a retry does not
            // duplicate what was already projected or published.
            await _eventDeduplicator.AbandonAsync(dedupKey);
            throw;
        }

        await _eventDeduplicator.MarkAsProcessedAsync(dedupKey, TimeSpan.FromMinutes(5));
        return dataSourceId;
    }

    private async Task RegisterCollectionAsync(DataSource dataSource, Guid tenantId, DateTime collectedAt,
        string payload, List<SampleSensorDTO> samples, string domain, string dedupKey,
        CancellationToken cancellationToken)
    {
        var dataSourceId = dataSource.Id;

        // Every id below derives from the collection key, so a retry rewrites the same object and the inserts skip
        // the rows a previous attempt already committed.
        var collectionGuid = DeterministicEventId.For($"{dedupKey}/collection", 0);
        var objectName = $"raw/{collectionGuid}.json";
        var putResponse = await _minioProvider.UploadJsonAsync(objectName, payload);

        var dataCollection = new DataCollection(collectionGuid, dataSourceId, collectedAt,
            JsonSerializer.Serialize(putResponse), tenantId);
        var sampleSensors = samples
            .Select((sample, index) => new SampleSensor(DeterministicEventId.For($"{dedupKey}/sample", index),
                collectionGuid, sample.SensorValue, sample.Unit, sample.Latitude, sample.Longitude,
                sample.RecordedAt))
            .ToList();
        var outboxMessages = new List<OutboxMessage>();

        for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
        {
            var sample = samples[sampleIndex];
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
            var evt = new SensorEventDetected(collectionGuid, eventType, intensity, sample.Latitude, sample.Longitude,
                collectedAt, DeterministicEventId.For(dedupKey, sampleIndex));

            _logger.LogInformation(
                "Inserting climatic event to mongodb. Json event: {evt}",
                evt
            );
            await _detectedEventReadRepository.AddAsync(
                new DetectedEventDTO(evt.EventId, collectionGuid, tenantId, domain, eventType, intensity,
                    sample.Latitude, sample.Longitude, collectedAt),
                cancellationToken);

            outboxMessages.Add(new OutboxMessage(evt.EventId, collectionGuid,
                $"{domain.ToLower()}-event-detected", JsonSerializer.Serialize(evt)));
        }

        // Transactional outbox: the collection, its samples and the events to publish are committed together.
        await _unitOfWork.ExecuteAsync(async _ =>
        {
            await _dataCollectionRepository.RegisterAsync(dataCollection);
            await _sampleSensorRepository.RegisterManyAsync(sampleSensors);
            foreach (var outboxMessage in outboxMessages)
                await _outboxRepository.RegisterAsync(outboxMessage);
        }, cancellationToken);
    }
}
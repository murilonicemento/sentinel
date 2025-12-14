using System.Text.Json;
using Ingestion.Application.Commands;
using Ingestion.Application.Events;
using Ingestion.Application.Interfaces.Deduplicators;
using Ingestion.Application.Interfaces.Providers;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Outbox;
using Ingestion.Domain.Repositories;
using Ingestion.Domain.ValueObjects;
using Ingestion.Infrastructure.Read.Persistence.DbContext;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ingestion.Application.Handlers;

public class RegisterSensorCollectionHandler : IRequestHandler<RegisterSensorCollectionCommand, Guid>
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly IDataCollectionRepository _dataCollectionRepository;
    private readonly ISampleSensorRepository _sampleSensorRepository;
    private readonly IEventDeduplicator _eventDeduplicator;
    private readonly IObjectStorageProvider _minioProvider;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ReadDbContext _readDbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RegisterSensorCollectionHandler> _logger;

    public RegisterSensorCollectionHandler(
        IDataSourceRepository dataSourceRepository,
        IDataCollectionRepository dataCollectionRepository,
        ISampleSensorRepository sampleSensorRepository,
        IEventDeduplicator eventDeduplicator,
        IObjectStorageProvider minioProvider,
        IOutboxRepository outboxRepository,
        ReadDbContext readDbContext,
        IConfiguration configuration,
        ILogger<RegisterSensorCollectionHandler> logger)
    {
        _dataSourceRepository = dataSourceRepository;
        _dataCollectionRepository = dataCollectionRepository;
        _sampleSensorRepository = sampleSensorRepository;
        _eventDeduplicator = eventDeduplicator;
        _minioProvider = minioProvider;
        _outboxRepository = outboxRepository;
        _readDbContext = readDbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Guid> Handle(RegisterSensorCollectionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Payload))
            throw new ArgumentException("Payload can't be blank.");
        if (request.SampleSensors.Any(sampleSensorDto => string.IsNullOrEmpty(sampleSensorDto.Unit)))
            throw new ArgumentException("Samples unit can't be blank.");

        var dataSource = _dataSourceRepository.GetByIdAndTenantId(request.DatasourceId, request.TenantId) ??
                         throw new KeyNotFoundException(
                             $"Data source or tenant not exist. Data source Id: {request.DatasourceId}; Tenant Id: {request.TenantId}");

        _logger.LogInformation("Datasource info. Id: {id}; TenantId: {tenantId}", dataSource.Id, dataSource.TenantId);

        var isSamplesUnitValid = request.SampleSensors
            .Select(sample => MeasurementType.From(dataSource.MeasurementType).IsValidUnit(sample.Unit))
            .Any(isValidUnit => isValidUnit);

        if (!isSamplesUnitValid)
            throw new ArgumentException($"Invalid unit type to measurement type: {dataSource.MeasurementType}");

        var lastDataCollected = dataSource.DataCollections.OrderByDescending(x => x.CollectedAt).FirstOrDefault();

        _logger.LogInformation(
            "Last data collected from DataSource with id {id} and name: {name}. Json {lastDataCollected}",
            dataSource.Id,
            dataSource.Name,
            JsonSerializer.Serialize(lastDataCollected)
        );

        if (lastDataCollected is not null)
        {
            var isValidFrequency = CollectionFrequencyType
                .From(dataSource.CollectionFrequency)
                .IsValidFrequency(lastDataCollected.CollectedAt);

            if (!isValidFrequency)
                throw new ArgumentException(
                    $"Unable to collect data. The collection frequency to data source is {dataSource.CollectionFrequency}");
        }

        var deduplicateKey = $"ing:{request.TenantId}:{request.DatasourceId}:{request.CollectedAt:yyyyMMddHHmmss}";
        var isDuplicate = await _eventDeduplicator.IsDuplicateAsync(deduplicateKey);

        if (isDuplicate)
        {
            _logger.LogInformation("Event duplicated. DeduplicateKey: {deduplicateKey}", deduplicateKey);
            return dataSource.Id;
        }

        var collectionId = Guid.NewGuid();
        var objectName = $"raw/{collectionId}.json";
        
        _logger.LogInformation("MinIO Object Name: {objectName}", objectName);
        
        var putObjectResponse = await _minioProvider.UploadJsonAsync(
            _configuration["MinIO:BucketName"]!,
            objectName,
            request.Payload
        );
        var dataCollection = new DataCollection(
            collectionId,
            request.DatasourceId,
            request.CollectedAt,
            JsonSerializer.Serialize(putObjectResponse),
            dataSource.TenantId
        );

        await _dataCollectionRepository.RegisterAsync(dataCollection);

        var collection = _readDbContext.GetCollection<ClimaticEventDetectedEvent>("events");

        foreach (var sampleSensorDto in request.SampleSensors)
        {
            var sampleSensor = new SampleSensor(
                Guid.NewGuid(),
                collectionId,
                sampleSensorDto.SensorValue,
                sampleSensorDto.Unit,
                sampleSensorDto.Latitude,
                sampleSensorDto.Longitude,
                sampleSensorDto.RecordedAt
            );

            await _sampleSensorRepository.RegisterAsync(sampleSensor);

            var intensity = MeasurementType
                .From(dataSource.MeasurementType)
                .CalculateIntensity(sampleSensorDto.SensorValue);
            var eventType = dataSource.MapValueToEventType(sampleSensorDto.SensorValue);
            var climaticEventDetectedEvent = new ClimaticEventDetectedEvent(
                collectionId,
                eventType,
                intensity,
                sampleSensorDto.Latitude,
                sampleSensorDto.Longitude,
                request.CollectedAt
            );
            var climaticEventDetectedEventJson = JsonSerializer.Serialize(climaticEventDetectedEvent);
            var outboxMessage = new OutboxMessage(
                Guid.NewGuid(),
                collectionId,
                "climatic-event-detected",
                climaticEventDetectedEventJson
            );
            _logger.LogInformation(
                "Inserting climatic event to mongodb. Json event: {ClimaticEventDetectedEventJson}",
                climaticEventDetectedEventJson
            );
            await collection.InsertOneAsync(climaticEventDetectedEvent, cancellationToken);
            await _outboxRepository.RegisterAsync(outboxMessage);
        }

        await _eventDeduplicator.MarkAsProcessedAsync(deduplicateKey, TimeSpan.FromMinutes(5));

        return dataSource.Id;
    }
}
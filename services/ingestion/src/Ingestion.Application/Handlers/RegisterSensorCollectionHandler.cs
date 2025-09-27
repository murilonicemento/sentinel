using System.Text.Json;
using Ingestion.Application.Commands;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.Events;
using Ingestion.Application.Interfaces.Providers;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Outbox;
using Ingestion.Domain.Repositories;
using Ingestion.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Ingestion.Application.Handlers;

public class RegisterSensorCollectionHandler : IRequestHandler<RegisterSensorCollectionCommand, Guid>
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly IDataCollectionRepository _dataCollectionRepository;
    private readonly ISampleSensorRepository _sampleSensorRepository;
    private readonly IEventDeduplicator _eventDeduplicator;
    private readonly IObjectStorageProvider _minioProvider;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IConfiguration _configuration;

    public RegisterSensorCollectionHandler(
        IDataSourceRepository dataSourceRepository,
        IDataCollectionRepository dataCollectionRepository,
        ISampleSensorRepository sampleSensorRepository,
        IEventDeduplicator eventDeduplicator,
        IObjectStorageProvider minioProvider,
        IOutboxRepository outboxRepository,
        IConfiguration configuration
    )
    {
        _dataSourceRepository = dataSourceRepository;
        _dataCollectionRepository = dataCollectionRepository;
        _sampleSensorRepository = sampleSensorRepository;
        _eventDeduplicator = eventDeduplicator;
        _minioProvider = minioProvider;
        _outboxRepository = outboxRepository;
        _configuration = configuration;
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
        var isSamplesUnitValid = request.SampleSensors
            .Select(sample => MeasurementType.From(dataSource.MeasurementType).IsValidUnit(sample.Unit))
            .Any(isValidUnit => isValidUnit);

        if (!isSamplesUnitValid)
            throw new ArgumentException($"Invalid unit type to measurement type: {dataSource.MeasurementType}");

        var lastDataCollected = dataSource.DataCollections.OrderByDescending(x => x.CollectedAt).First();
        var isValidFrequency = CollectionFrequencyType
            .From(dataSource.CollectionFrequency)
            .IsValidFrequency(lastDataCollected.CollectedAt);

        if (!isValidFrequency)
            throw new ArgumentException(
                $"Unable to collect data. The collection frequency to data source is {dataSource.CollectionFrequency}");

        var deduplicateKey = $"ing:{request.TenantId}:{request.DatasourceId}:{request.CollectedAt:yyyyMMddHHmmss}";
        var isDuplicate = await _eventDeduplicator.IsDuplicateAsync(deduplicateKey);

        if (isDuplicate)
            throw new InvalidOperationException("Duplicate collection detected.");

        var collectionId = Guid.NewGuid();
        var objectName = $"raw/{collectionId}.json";
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
            Guid.NewGuid()
        );

        await _dataCollectionRepository.RegisterAsync(dataCollection);

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
            var climaticEventDto = new ClimaticEventDTO(
                collectionId,
                dataSource.DataSourceType,
                intensity,
                sampleSensorDto.Latitude,
                sampleSensorDto.Longitude,
                request.CollectedAt
            );
            var climaticEventDtoJson = JsonSerializer.Serialize(climaticEventDto);
            var outboxMessage = new OutboxMessage(
                Guid.NewGuid(),
                collectionId,
                "ClimaticEventDTO",
                climaticEventDtoJson
            );

            await _outboxRepository.RegisterAsync(outboxMessage);
        }

        await _eventDeduplicator.MarkAsProcessedAsync(deduplicateKey, TimeSpan.FromMinutes(5));

        return collectionId;
    }
}
using System.Text.Json;
using Ingestion.Application.Commands;
using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using Ingestion.Application.Providers;
using Ingestion.Domain.Aggregates;
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
    private readonly IObjectStorageProvider _objectStorageProvider;
    private readonly IConfiguration _configuration;

    public RegisterSensorCollectionHandler(
        IDataSourceRepository dataSourceRepository,
        IDataCollectionRepository dataCollectionRepository,
        ISampleSensorRepository sampleSensorRepository,
        IEventDeduplicator eventDeduplicator,
        IObjectStorageProvider objectStorageProvider,
        IConfiguration configuration
    )
    {
        _dataSourceRepository = dataSourceRepository;
        _dataCollectionRepository = dataCollectionRepository;
        _sampleSensorRepository = sampleSensorRepository;
        _eventDeduplicator = eventDeduplicator;
        _objectStorageProvider = objectStorageProvider;
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

        var putObjectResponse = await _objectStorageProvider.UploadJsonAsync(
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
                sampleSensorDto.RecordedAt
            );

            await _sampleSensorRepository.RegisterAsync(sampleSensor);
        }

        var climaticEventDto = new ClimaticEventDTO
        {
            EventId = collectionId,
            EventType = dataSource.DataSourceType,
            CollectedAt = request.CollectedAt
        };
        var climaticEventJson = JsonSerializer.Serialize(climaticEventDto);

        return collectionId;
    }
}
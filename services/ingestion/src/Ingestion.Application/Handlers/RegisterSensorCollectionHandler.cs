using Ingestion.Application.Commands;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Repositories;
using Ingestion.Domain.ValueObjects;
using MediatR;

namespace Ingestion.Application.Handlers;

public class RegisterSensorCollectionHandler : IRequestHandler<RegisterSensorCollectionCommand, Guid>
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly IDataCollectionRepository _dataCollectionRepository;
    private readonly ISampleSensorRepository _sampleSensorRepository;

    public RegisterSensorCollectionHandler(
        IDataSourceRepository dataSourceRepository,
        IDataCollectionRepository dataCollectionRepository,
        ISampleSensorRepository sampleSensorRepository
    )
    {
        _dataSourceRepository = dataSourceRepository;
        _dataCollectionRepository = dataCollectionRepository;
        _sampleSensorRepository = sampleSensorRepository;
    }

    public async Task<Guid> Handle(RegisterSensorCollectionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Payload))
            throw new ArgumentException("Payload can't be blank.");
        if (request.SampleSensors.Any(sampleSensorDto => string.IsNullOrEmpty(sampleSensorDto.Unit)))
            throw new ArgumentException("Samples unit can't be blank.");

        var dataSource = _dataSourceRepository.GetById(request.DatasourceId) ??
                         throw new KeyNotFoundException($"Data source with id {request.DatasourceId} not exist.");
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

        var dataCollection = new DataCollection(
            Guid.NewGuid(),
            request.DatasourceId,
            request.CollectedAt,
            request.Payload
        );

        await _dataCollectionRepository.RegisterAsync(dataCollection);

        foreach (var sampleSensorDto in request.SampleSensors)
        {
            var sampleSensor = new SampleSensor(
                Guid.NewGuid(),
                dataCollection.Id,
                sampleSensorDto.SensorValue,
                sampleSensorDto.Unit,
                sampleSensorDto.RecordedAt
            );

            await _sampleSensorRepository.RegisterAsync(sampleSensor);
        }

        return dataCollection.Id;
    }
}
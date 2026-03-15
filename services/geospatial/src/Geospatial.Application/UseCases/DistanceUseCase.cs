using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Domain.Events;
using Geospatial.Domain.Repositories;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Geospatial.Application.UseCases;

public class DistanceUseCase : IDistanceUseCase
{
    private readonly IGeospatialCalculator _calculator;
    private readonly IGeospatialEventRepository _eventRepository;
    private readonly ILogger<DistanceUseCase> _logger;

    public DistanceUseCase(
        IGeospatialCalculator calculator,
        IGeospatialEventRepository eventRepository,
        ILogger<DistanceUseCase> logger)
    {
        _calculator = calculator;
        _eventRepository = eventRepository;
        _logger = logger;
    }

    public async Task<double> ExecuteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var payload = new { From = new { from.Latitude, from.Longitude }, To = new { to.Latitude, to.Longitude } };

        _logger.LogInformation(
            "[{OperationType}] Input: {@Payload} | Timestamp: {Timestamp}",
            "distance", payload, timestamp);

        var result = _calculator.Distance(from, to);

        _logger.LogInformation(
            "[{OperationType}] Result: {Result}m | Timestamp: {Timestamp}",
            "distance", result, timestamp);

        var evt = new GeospatialOperationEvent
        {
            CollectionId = Guid.NewGuid(),
            OperationType = "distance",
            Payload = payload,
            Result = result,
            Timestamp = timestamp,
            MainPoint = new GeoLocation { Lat = from.Latitude, Lon = from.Longitude }
        };

        await _eventRepository.IndexAsync(evt, cancellationToken);

        return result;
    }
}

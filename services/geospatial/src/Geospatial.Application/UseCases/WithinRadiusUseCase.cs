using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Domain.Events;
using Geospatial.Domain.Repositories;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Geospatial.Application.UseCases;

public class WithinRadiusUseCase : IWithinRadiusUseCase
{
    private readonly IGeospatialCalculator _calculator;
    private readonly IGeospatialEventRepository _eventRepository;
    private readonly ILogger <WithinRadiusUseCase> _logger;

    public WithinRadiusUseCase(
        IGeospatialCalculator calculator,
        IGeospatialEventRepository eventRepository,
        ILogger<WithinRadiusUseCase> logger)
    {
        _calculator = calculator;
        _eventRepository = eventRepository;
        _logger = logger;
    }

    public async Task<(bool WithinRadius, double Distance)> ExecuteAsync(GeoPoint center, GeoPoint point, GeoRadius radius, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var payload = new
        {
            Center = new { center.Latitude, center.Longitude },
            Point = new { point.Latitude, point.Longitude },
            Radius = new { radius.Value, radius.Unit }
        };

        _logger.LogInformation(
            "[{OperationType}] Input: {@Payload} | Timestamp: {Timestamp}",
            "within-radius", payload, timestamp);

        var distance = _calculator.Distance(center, point);
        var isWithin = distance <= radius.ToMeters();
        var result = (WithinRadius: isWithin, Distance: distance);

        _logger.LogInformation(
            "[{OperationType}] Result: WithinRadius={WithinRadius}, Distance={Distance}m | Timestamp: {Timestamp}",
            "within-radius", result.WithinRadius, result.Distance, timestamp);

        var evt = new GeospatialOperationEvent
        {
            CollectionId = Guid.NewGuid(),
            OperationType = "within-radius",
            Payload = payload,
            Result = result,
            Timestamp = timestamp,
            MainPoint = new GeoLocation { Lat = center.Latitude, Lon = center.Longitude }
        };

        await _eventRepository.IndexAsync(evt, cancellationToken);

        return result;
    }
}

using Geospatial.Application.Interfaces.Repositories;
using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Application.Models;
using Geospatial.Domain.Geometry;
using Geospatial.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Geospatial.Application.UseCases;

public class IntersectsUseCase : IIntersectsUseCase
{
    private readonly IGeospatialCalculator _calculator;
    private readonly IGeospatialEventRepository _eventRepository;
    private readonly ILogger<IntersectsUseCase> _logger;

    public IntersectsUseCase(
        IGeospatialCalculator calculator,
        IGeospatialEventRepository eventRepository,
        ILogger<IntersectsUseCase> logger)
    {
        _calculator = calculator;
        _eventRepository = eventRepository;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(GeoPolygon a, GeoPolygon b, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var mainPoint = a.Points.FirstOrDefault();
        var payload = new
        {
            PolygonA = a.Points.Select(p => new { p.Latitude, p.Longitude }),
            PolygonB = b.Points.Select(p => new { p.Latitude, p.Longitude })
        };

        _logger.LogInformation(
            "[{OperationType}] Input: {@Payload} | Timestamp: {Timestamp}",
            "intersects", payload, timestamp);

        var result = _calculator.Intersects(a, b);

        _logger.LogInformation(
            "[{OperationType}] Result: {Result} | Timestamp: {Timestamp}",
            "intersects", result, timestamp);

        var evt = new GeospatialOperationEvent
        {
            EventId = Guid.NewGuid(),
            OperationType = "intersects",
            Payload = payload,
            Result = result,
            Timestamp = timestamp,
            MainPoint = mainPoint != null ? new GeoLocation { Lat = mainPoint.Latitude, Lon = mainPoint.Longitude } : null
        };

        await _eventRepository.IndexAsync(evt, cancellationToken);

        return result;
    }
}

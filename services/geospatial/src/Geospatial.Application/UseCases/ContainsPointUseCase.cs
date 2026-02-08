using Geospatial.Application.Interfaces.Repositories;
using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Application.Models;
using Geospatial.Domain.Geometry;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Geospatial.Application.UseCases;

public class ContainsPointUseCase : IContainsPointUseCase
{
    private readonly IGeospatialCalculator _calculator;
    private readonly IGeospatialEventRepository _eventRepository;
    private readonly ILogger<ContainsPointUseCase> _logger;

    public ContainsPointUseCase(
        IGeospatialCalculator calculator,
        IGeospatialEventRepository eventRepository,
        ILogger<ContainsPointUseCase> logger)
    {
        _calculator = calculator;
        _eventRepository = eventRepository;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(GeoPolygon area, GeoPoint point, CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var payload = new { Area = area.Points.Select(p => new { p.Latitude, p.Longitude }), Point = new { point.Latitude, point.Longitude } };

        _logger.LogInformation(
            "[{OperationType}] Input: {@Payload} | Timestamp: {Timestamp}",
            "contains-point", payload, timestamp);

        var result = _calculator.ContainsPoint(area, point);

        _logger.LogInformation(
            "[{OperationType}] Result: {Result} | Timestamp: {Timestamp}",
            "contains-point", result, timestamp);

        var evt = new GeospatialOperationEvent
        {
            EventId = Guid.NewGuid(),
            OperationType = "contains-point",
            Payload = payload,
            Result = result,
            Timestamp = timestamp,
            MainPoint = new GeoLocation { Lat = point.Latitude, Lon = point.Longitude }
        };

        await _eventRepository.IndexAsync(evt, cancellationToken);

        return result;
    }
}

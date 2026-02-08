using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.UseCases;

public class WithinRadiusUseCase : IWithinRadiusUseCase
{
    private readonly IGeospatialCalculator _calculator;

    public WithinRadiusUseCase(IGeospatialCalculator calculator)
    {
        _calculator = calculator;
    }

    public (bool WithinRadius, double Distance) Execute(GeoPoint center, GeoPoint point, GeoRadius radius)
    {
        var distance = _calculator.Distance(center, point);
        var isWithin = distance <= radius.ToMeters();

        return (isWithin, distance);
    }
}
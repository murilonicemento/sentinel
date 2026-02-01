using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.UseCases;

public class DistanceUseCase
{
    private readonly IGeospatialCalculator _calculator;

    public DistanceUseCase(IGeospatialCalculator calculator)
    {
        _calculator = calculator;
    }

    public double Execute(GeoPoint from, GeoPoint to) => _calculator.Distance(from, to);
}
using Geospatial.Domain.Geometry;
using Geospatial.Domain.Services;

namespace Geospatial.Application.UseCases;

public class IntersectsUseCase
{
    private readonly IGeospatialCalculator _calculator;

    public IntersectsUseCase(IGeospatialCalculator calculator)
    {
        _calculator = calculator;
    }

    public bool Execute(GeoPolygon a, GeoPolygon b) => _calculator.Intersects(a, b);
}
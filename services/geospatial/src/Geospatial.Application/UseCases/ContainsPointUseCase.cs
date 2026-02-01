using Geospatial.Domain.Geometry;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.UseCases;

public class ContainsPointUseCase
{
    private readonly IGeospatialCalculator _calculator;

    public ContainsPointUseCase(IGeospatialCalculator calculator)
    {
        _calculator = calculator;
    }

    public bool Execute(GeoPolygon area, GeoPoint point) => _calculator.ContainsPoint(area, point);
}
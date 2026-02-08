using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.Interfaces.UseCases;

public interface IWithinRadiusUseCase
{
    public (bool WithinRadius, double Distance) Execute(GeoPoint center, GeoPoint point, GeoRadius radius);
}
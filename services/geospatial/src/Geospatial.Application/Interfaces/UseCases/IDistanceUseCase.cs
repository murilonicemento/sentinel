using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.Interfaces.UseCases;

public interface IDistanceUseCase
{
    public double Execute(GeoPoint from, GeoPoint to);
}
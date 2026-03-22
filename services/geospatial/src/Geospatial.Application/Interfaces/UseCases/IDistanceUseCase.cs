using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.Interfaces.UseCases;

public interface IDistanceUseCase
{
    Task<double> ExecuteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken = default);
}
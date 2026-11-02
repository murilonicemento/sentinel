using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.Interfaces.UseCases;

public interface IWithinRadiusUseCase
{
    Task<(bool WithinRadius, double Distance)> ExecuteAsync(GeoPoint center, GeoPoint point, GeoRadius radius, CancellationToken cancellationToken = default);
}
namespace Ingestion.Application.Services;

public interface IGeospatialValidationService
{
    Task<bool> ValidateCoordinatesAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

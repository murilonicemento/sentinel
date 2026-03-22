namespace Ingestion.Application.Interfaces.Services;

public interface IGeospatialValidationService
{
    public Task<bool> ValidateCoordinatesAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}
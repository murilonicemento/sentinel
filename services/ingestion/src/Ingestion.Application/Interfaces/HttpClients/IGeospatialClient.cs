using Ingestion.Application.DTO;

namespace Ingestion.Application.Interfaces.HttpClients;

public interface IGeospatialClient
{
    Task<bool> ContainsPointAsync(PointDTO point, PolygonDTO area, CancellationToken cancellationToken = default);

    Task<(bool WithinRadius, double Distance)> WithinRadiusAsync(
        PointDTO center,
        PointDTO point,
        RadiusDTO radius,
        CancellationToken cancellationToken = default);

    Task<bool> IntersectsAsync(PolygonDTO polygonA, PolygonDTO polygonB, CancellationToken cancellationToken = default);
    Task<double> DistanceAsync(PointDTO from, PointDTO to, CancellationToken cancellationToken = default);
    Task<BatchResponseDTO> EvaluateBatchAsync(BatchRequestDTO request, CancellationToken cancellationToken = default);
}
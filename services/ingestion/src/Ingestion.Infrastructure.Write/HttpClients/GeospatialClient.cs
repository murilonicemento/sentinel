using System.Net.Http.Json;
using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Microsoft.Extensions.Logging;

namespace Ingestion.Infrastructure.Write.HttpClients;

public class GeospatialClient : IGeospatialClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeospatialClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GeospatialClient(HttpClient httpClient, ILogger<GeospatialClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<bool> ContainsPointAsync(PointDTO point, PolygonDTO area,
        CancellationToken cancellationToken = default)
    {
        var request = new ContainsPointRequestDTO
        {
            Point = point,
            Area = area
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/geospatial/contains-point",
                request,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ContainsPointResponse>(
                _jsonOptions,
                cancellationToken);

            return result?.Contains ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Geospatial Service ContainsPoint endpoint");
            throw;
        }
    }

    public async Task<(bool WithinRadius, double Distance)> WithinRadiusAsync(
        PointDTO center,
        PointDTO point,
        RadiusDTO radius,
        CancellationToken cancellationToken = default)
    {
        var request = new WithinRadiusRequestDTO
        {
            Center = center,
            Point = point,
            Radius = radius
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/geospatial/within-radius",
                request,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<WithinRadiusResponse>(
                _jsonOptions,
                cancellationToken);

            return (result?.WithinRadius ?? false, result?.Distance ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Geospatial Service WithinRadius endpoint");
            throw;
        }
    }

    public async Task<bool> IntersectsAsync(PolygonDTO polygonA, PolygonDTO polygonB,
        CancellationToken cancellationToken = default)
    {
        var request = new IntersectsRequestDTO
        {
            PolygonA = polygonA,
            PolygonB = polygonB
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/geospatial/intersects",
                request,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<IntersectsResponse>(
                _jsonOptions,
                cancellationToken);

            return result?.Intersects ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Geospatial Service Intersects endpoint");
            throw;
        }
    }

    public async Task<double> DistanceAsync(PointDTO from, PointDTO to, CancellationToken cancellationToken = default)
    {
        var request = new DistanceRequestDTO
        {
            From = from,
            To = to
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/geospatial/distance",
                request,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DistanceResponse>(
                _jsonOptions,
                cancellationToken);

            return result?.Distance ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Geospatial Service Distance endpoint");
            throw;
        }
    }

    public async Task<BatchResponseDTO> EvaluateBatchAsync(BatchRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/geospatial/evaluate-batch",
                request,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BatchResponseDTO>(
                _jsonOptions,
                cancellationToken);

            return result ?? new BatchResponseDTO();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Geospatial Service EvaluateBatch endpoint");
            throw;
        }
    }

    private record ContainsPointResponse(bool Contains);

    private record WithinRadiusResponse(bool WithinRadius, double Distance);

    private record IntersectsResponse(bool Intersects);

    private record DistanceResponse(double Distance);
}
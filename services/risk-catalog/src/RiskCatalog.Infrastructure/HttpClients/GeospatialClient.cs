using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Interfaces;

namespace RiskCatalog.Infrastructure.HttpClients;

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
                "api/v1/geospatial/operations/contains-point",
                request,
                _jsonOptions,
                cancellationToken);

            var result = await ReadResultAsync<ContainsPointResponse>(response, cancellationToken);

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
                "api/v1/geospatial/operations/within-radius",
                request,
                _jsonOptions,
                cancellationToken);

            var result = await ReadResultAsync<WithinRadiusResponse>(response, cancellationToken);

            return (result?.WithinRadius ?? false, result?.DistanceMeters ?? result?.Distance ?? 0);
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
                "api/v1/geospatial/operations/intersects",
                request,
                _jsonOptions,
                cancellationToken);

            var result = await ReadResultAsync<IntersectsResponse>(response, cancellationToken);

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
                "api/v1/geospatial/operations/distance",
                request,
                _jsonOptions,
                cancellationToken);

            var result = await ReadResultAsync<DistanceResponse>(response, cancellationToken);

            return result?.DistanceMeters ?? result?.Distance ?? 0;
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
                "api/v1/geospatial/operations/evaluate-batch",
                request,
                _jsonOptions,
                cancellationToken);

            var result = await ReadResultAsync<BatchResponseDTO>(response, cancellationToken);

            return result ?? new BatchResponseDTO();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Geospatial Service EvaluateBatch endpoint");
            throw;
        }
    }

    /// <summary>
    /// Reads a geospatial result. The calls go to <c>/api/v1/geospatial/operations</c>, which wraps the result in the
    /// <c>ResponseBaseDTO</c> envelope; a bare body (the deprecated <c>/api/geospatial</c> routes) is read too, so a
    /// geospatial rollback does not break this caller (ADR-0007 §4, X-25).
    /// </summary>
    private async Task<T?> ReadResultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var body = document.RootElement;
        var result = body.ValueKind == JsonValueKind.Object
            ? body.EnumerateObject().FirstOrDefault(p => p.NameEquals("data") || p.NameEquals("Data")).Value
            : default;

        return (result.ValueKind == JsonValueKind.Undefined ? body : result).Deserialize<T>(_jsonOptions);
    }

    private record ContainsPointResponse(bool Contains);

    // v1 names the distance DistanceMeters; the frozen route named it Distance.
    private record WithinRadiusResponse(bool WithinRadius, double? DistanceMeters, double? Distance);

    private record IntersectsResponse(bool Intersects);

    private record DistanceResponse(double? DistanceMeters, double? Distance);
}
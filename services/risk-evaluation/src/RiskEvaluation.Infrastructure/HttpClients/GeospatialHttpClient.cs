using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Application.Interfaces.HttpClients;

namespace RiskEvaluation.Infrastructure.HttpClients;

public class GeospatialHttpClient : IGeospatialClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeospatialHttpClient> _logger;

    public GeospatialHttpClient(HttpClient httpClient, ILogger<GeospatialHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> IsPointInRiskAreaAsync(int latitude, int longitude, string areaType,
        CancellationToken cancellationToken = default)
    {
        var request = new { Point = new { Latitude = latitude, Longitude = longitude }, AreaType = areaType };
        var response = await _httpClient.PostAsJsonAsync("/api/geospatial/contains-point", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return false;

        var result = await response.Content.ReadFromJsonAsync<ContainsPointResponse>(cancellationToken);
        return result?.Contains ?? false;
    }

    public async Task<GeospatialContextDTO?> GetSpatialContextAsync(int latitude, int longitude,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/geospatial/context?lat={latitude}&lon={longitude}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GeospatialContextDTO>(cancellationToken);
    }

    public async Task<IReadOnlyList<NearbyRiskDTO>> GetNearbyRisksAsync(int latitude, int longitude,
        double radiusMeters, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            Center = new { Latitude = latitude, Longitude = longitude },
            RadiusMeters = radiusMeters
        };

        var response =
            await _httpClient.PostAsJsonAsync("/api/geospatial/query/search-by-radius", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new List<NearbyRiskDTO>();

        var data = await response.Content.ReadFromJsonAsync<List<NearbyRiskDTO>>(cancellationToken);
        return data ?? [];
    }

    public async Task<double> CalculateDistanceAsync(int lat1, int lon1, int lat2, int lon2,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            From = new { Latitude = lat1, Longitude = lon1 },
            To = new { Latitude = lat2, Longitude = lon2 }
        };

        var response = await _httpClient.PostAsJsonAsync("/api/geospatial/distance", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return double.MaxValue;

        var result = await response.Content.ReadFromJsonAsync<DistanceResponse>(cancellationToken);
        return result?.Distance ?? double.MaxValue;
    }

    private class ContainsPointResponse
    {
        public bool Contains { get; set; }
    }

    private class DistanceResponse
    {
        public double Distance { get; set; }
    }
}
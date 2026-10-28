using System.Net.Http.Json;
using System.Text.Json;
using RiskEvaluation.Application.Interfaces.HttpClients;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Enums;

namespace RiskEvaluation.Infrastructure.HttpClients;

/// <summary>Reads a catalog version from the paged listings of <c>/api/v1/risk-catalog</c>.</summary>
public sealed class RiskCatalogHttpClient : IRiskCatalogClient
{
    private const int PageSize = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    public RiskCatalogHttpClient(HttpClient httpClient, TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RiskCatalogSnapshot> FetchSnapshotAsync(int version, CancellationToken cancellationToken = default)
    {
        var regions = await ReadAllAsync<RegionDto>("/api/v1/risk-catalog/regional-risk-parameters", cancellationToken);
        var criteria = await ReadAllAsync<SeverityCriterionDto>(
            $"/api/v1/risk-catalog/severity-criteria?version={version}", cancellationToken);
        var matrix = await ReadAllAsync<MatrixEntryDto>(
            $"/api/v1/risk-catalog/risk-matrix-entries?version={version}", cancellationToken);

        return new RiskCatalogSnapshot(
            version,
            _timeProvider.GetUtcNow().UtcDateTime,
            regions.Select(r => new RegionalAdjustment(r.RegionId, r.AdjustmentFactor, r.CenterLatitude,
                r.CenterLongitude, r.CoverageRadiusKm,
                r.RegionBounds?.Coordinates.Select(p => new GeoCoordinate(p.Latitude, p.Longitude)).ToList() ?? []))
                .ToList(),
            criteria.Select(c => new SeverityRule(c.EventTypeCode, c.SeverityLevel, c.MinValue, c.MaxValue)).ToList(),
            // Entries with a level this service does not know are ignored rather than failing the whole copy.
            matrix.Where(m => Enum.TryParse<RiskLevel>(m.RiskLevel, true, out _))
                .Select(m => new RiskMatrixRule(m.EventTypeCode, m.SeverityLevel, Enum.Parse<RiskLevel>(m.RiskLevel, true)))
                .ToList());
    }

    private async Task<List<T>> ReadAllAsync<T>(string path, CancellationToken cancellationToken)
    {
        var items = new List<T>();
        var separator = path.Contains('?') ? '&' : '?';
        for (var page = 1; ; page++)
        {
            using var response = await _httpClient.GetAsync($"{path}{separator}page={page}&pageSize={PageSize}",
                cancellationToken);
            // 5xx/408/429 are transient for the consumer's retry; 4xx (e.g. a missing trust) goes to the dead-letter.
            response.EnsureSuccessStatusCode();

            var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions, cancellationToken)
                           ?? throw new InvalidOperationException($"risk-catalog returned an empty body for {path}.");
            items.AddRange(envelope.Data.Items);
            if (page >= envelope.Data.TotalPages) return items;
        }
    }

    private sealed record Envelope<T>(PagedData<T> Data);

    private sealed record PagedData<T>(List<T> Items, int TotalPages);

    private sealed record RegionDto(Guid RegionId, double AdjustmentFactor, double? CenterLatitude,
        double? CenterLongitude, double? CoverageRadiusKm, PolygonDto? RegionBounds);

    private sealed record PolygonDto(List<PointDto> Coordinates);

    private sealed record PointDto(double Latitude, double Longitude);

    private sealed record SeverityCriterionDto(string EventTypeCode, string SeverityLevel, double MinValue,
        double MaxValue);

    private sealed record MatrixEntryDto(string EventTypeCode, string SeverityLevel, string RiskLevel);
}

using RiskEvaluation.Domain.Enums;

namespace RiskEvaluation.Domain.Entities;

/// <summary>
/// Local copy of one published risk-catalog version: regional adjustment factors and the rules that turn an event's
/// intensity into a severity and the severity into a risk level. Evaluations read only this copy.
/// </summary>
public sealed class RiskCatalogSnapshot
{
    /// <summary>Same bounds as the geographic multiplier of the score calculation.</summary>
    public const double MinRegionalFactor = 0.5;
    public const double MaxRegionalFactor = 2.0;

    public RiskCatalogSnapshot(int version, DateTime capturedAt, IReadOnlyList<RegionalAdjustment> regions,
        IReadOnlyList<SeverityRule> severityRules, IReadOnlyList<RiskMatrixRule> matrixRules)
    {
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version), "The catalog version must be positive.");

        Version = version;
        CapturedAt = capturedAt;
        Regions = regions;
        SeverityRules = severityRules;
        MatrixRules = matrixRules;
    }

    public int Version { get; }
    public DateTime CapturedAt { get; }
    public IReadOnlyList<RegionalAdjustment> Regions { get; }
    public IReadOnlyList<SeverityRule> SeverityRules { get; }
    public IReadOnlyList<RiskMatrixRule> MatrixRules { get; }

    /// <summary>
    /// Highest adjustment factor among the regions that contain the point, bounded to
    /// [<see cref="MinRegionalFactor"/>, <see cref="MaxRegionalFactor"/>]; 1 when no region contains it.
    /// </summary>
    public double RegionalFactorAt(double latitude, double longitude)
    {
        var region = RegionAt(latitude, longitude);
        return region is null ? 1.0 : Math.Clamp(region.Factor, MinRegionalFactor, MaxRegionalFactor);
    }

    /// <summary>
    /// The region whose factor applies to the point: the highest factor among the regions that contain it, ties broken
    /// by region code so the choice is stable. Null when no region contains the point.
    /// </summary>
    public RegionalAdjustment? RegionAt(double latitude, double longitude) =>
        Regions.Where(r => r.Contains(latitude, longitude))
            .OrderByDescending(r => r.Factor)
            .ThenBy(r => r.Code, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// Risk level the catalog assigns to an event of this type and intensity: intensity → severity by the criteria
    /// ranges, severity → risk level by the matrix. Null when the catalog has no rule for it.
    /// </summary>
    public RiskLevel? MatrixLevelFor(string? eventType, double? intensity)
    {
        if (string.IsNullOrWhiteSpace(eventType) || intensity is null) return null;

        var levels = SeverityRules
            .Where(rule => rule.Matches(eventType, intensity.Value))
            .SelectMany(rule => MatrixRules.Where(m =>
                string.Equals(m.EventTypeCode, eventType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.SeverityLevel, rule.SeverityLevel, StringComparison.OrdinalIgnoreCase)))
            .Select(m => m.RiskLevel)
            .ToList();

        return levels.Count == 0 ? null : levels.Max();
    }
}

/// <summary>
/// A catalog region: a polygon when it has at least three vertices, otherwise a circle around its center.
/// <paramref name="RegionCode"/> is null in copies taken before risk-catalog exposed it (RC-14).
/// </summary>
public sealed record RegionalAdjustment(
    Guid RegionId,
    double Factor,
    double? CenterLatitude,
    double? CenterLongitude,
    double? CoverageRadiusKm,
    IReadOnlyList<GeoCoordinate> Polygon,
    string? RegionCode = null)
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>The region's identity; a copy without the code falls back to the code risk-catalog gave legacy rows.</summary>
    public string Code => string.IsNullOrWhiteSpace(RegionCode) ? $"legacy-{RegionId:N}" : RegionCode;

    public bool Contains(double latitude, double longitude)
    {
        if (Polygon.Count >= 3) return PolygonContains(latitude, longitude);

        if (CenterLatitude is not { } centerLat || CenterLongitude is not { } centerLon ||
            CoverageRadiusKm is not { } radius) return false;

        return DistanceKm(latitude, longitude, centerLat, centerLon) <= radius;
    }

    // Ray casting on latitude/longitude; adequate for the regional extents the catalog stores.
    private bool PolygonContains(double latitude, double longitude)
    {
        var inside = false;
        for (int i = 0, j = Polygon.Count - 1; i < Polygon.Count; j = i++)
        {
            var (yi, xi) = (Polygon[i].Latitude, Polygon[i].Longitude);
            var (yj, xj) = (Polygon[j].Latitude, Polygon[j].Longitude);
            if (yi > latitude != yj > latitude &&
                longitude < (xj - xi) * (latitude - yi) / (yj - yi) + xi)
                inside = !inside;
        }

        return inside;
    }

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;
        var dLat = Radians(lat2 - lat1);
        var dLon = Radians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Radians(lat1)) * Math.Cos(Radians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}

public sealed record GeoCoordinate(double Latitude, double Longitude);

/// <summary>Intensity range, inclusive, that the catalog classifies as <see cref="SeverityLevel"/>.</summary>
public sealed record SeverityRule(string EventTypeCode, string SeverityLevel, double MinValue, double MaxValue)
{
    public bool Matches(string eventType, double intensity) =>
        string.Equals(EventTypeCode, eventType, StringComparison.OrdinalIgnoreCase) &&
        intensity >= MinValue && intensity <= MaxValue;
}

public sealed record RiskMatrixRule(string EventTypeCode, string SeverityLevel, RiskLevel RiskLevel);

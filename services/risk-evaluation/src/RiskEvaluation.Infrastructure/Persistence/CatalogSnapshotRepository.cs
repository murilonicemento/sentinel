using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Enums;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Infrastructure.Persistence;

/// <summary>Stores snapshots in <c>CatalogSnapshots</c>, one document per catalog version.</summary>
public class CatalogSnapshotRepository : ICatalogSnapshotRepository
{
    private readonly IMongoCollection<SnapshotDocument> _collection;

    public CatalogSnapshotRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SnapshotDocument>("CatalogSnapshots");
    }

    public Task SaveAsync(RiskCatalogSnapshot snapshot, CancellationToken cancellationToken = default) =>
        _collection.ReplaceOneAsync(d => d.Version == snapshot.Version, SnapshotDocument.From(snapshot),
            new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public async Task<RiskCatalogSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var document = await _collection.Find(_ => true).SortByDescending(d => d.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return document?.ToSnapshot();
    }

    [BsonIgnoreExtraElements]
    internal sealed class SnapshotDocument
    {
        public int Version { get; set; }
        public DateTime CapturedAt { get; set; }
        public List<RegionDocument> Regions { get; set; } = [];
        public List<SeverityRuleDocument> SeverityRules { get; set; } = [];
        public List<MatrixRuleDocument> MatrixRules { get; set; } = [];

        public static SnapshotDocument From(RiskCatalogSnapshot snapshot) => new()
        {
            Version = snapshot.Version,
            CapturedAt = snapshot.CapturedAt,
            Regions = snapshot.Regions.Select(r => new RegionDocument
            {
                RegionId = r.RegionId,
                Factor = r.Factor,
                CenterLatitude = r.CenterLatitude,
                CenterLongitude = r.CenterLongitude,
                CoverageRadiusKm = r.CoverageRadiusKm,
                Polygon = r.Polygon.Select(p => new[] { p.Latitude, p.Longitude }).ToList()
            }).ToList(),
            SeverityRules = snapshot.SeverityRules.Select(s => new SeverityRuleDocument
            {
                EventTypeCode = s.EventTypeCode,
                SeverityLevel = s.SeverityLevel,
                MinValue = s.MinValue,
                MaxValue = s.MaxValue
            }).ToList(),
            MatrixRules = snapshot.MatrixRules.Select(m => new MatrixRuleDocument
            {
                EventTypeCode = m.EventTypeCode,
                SeverityLevel = m.SeverityLevel,
                RiskLevel = m.RiskLevel.ToString()
            }).ToList()
        };

        public RiskCatalogSnapshot ToSnapshot() => new(
            Version,
            DateTime.SpecifyKind(CapturedAt, DateTimeKind.Utc),
            Regions.Select(r => new RegionalAdjustment(r.RegionId, r.Factor, r.CenterLatitude, r.CenterLongitude,
                r.CoverageRadiusKm, r.Polygon.Select(p => new GeoCoordinate(p[0], p[1])).ToList())).ToList(),
            SeverityRules.Select(s => new SeverityRule(s.EventTypeCode, s.SeverityLevel, s.MinValue, s.MaxValue))
                .ToList(),
            MatrixRules.Select(m => new RiskMatrixRule(m.EventTypeCode, m.SeverityLevel,
                Enum.Parse<RiskLevel>(m.RiskLevel))).ToList());
    }

    internal sealed class RegionDocument
    {
        [BsonGuidRepresentation(MongoDB.Bson.GuidRepresentation.Standard)]
        public Guid RegionId { get; set; }

        public double Factor { get; set; }
        public double? CenterLatitude { get; set; }
        public double? CenterLongitude { get; set; }
        public double? CoverageRadiusKm { get; set; }

        /// <summary>Vertices as [latitude, longitude].</summary>
        public List<double[]> Polygon { get; set; } = [];
    }

    internal sealed class SeverityRuleDocument
    {
        public string EventTypeCode { get; set; } = string.Empty;
        public string SeverityLevel { get; set; } = string.Empty;
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
    }

    internal sealed class MatrixRuleDocument
    {
        public string EventTypeCode { get; set; } = string.Empty;
        public string SeverityLevel { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
    }
}

namespace RiskEvaluation.Domain.Entities;

/// <summary>A risk-catalog version published by risk-catalog and seen by this service.</summary>
public class CatalogVersion
{
    public CatalogVersion(int version, string? notes, DateTime publishedAt, Guid publishedBy, DateTime receivedAt)
    {
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version), "The catalog version must be positive.");

        Version = version;
        Notes = notes;
        PublishedAt = publishedAt;
        PublishedBy = publishedBy;
        ReceivedAt = receivedAt;
    }

    public int Version { get; private set; }
    public string? Notes { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public Guid PublishedBy { get; private set; }
    public DateTime ReceivedAt { get; private set; }
}

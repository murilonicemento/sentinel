using RiskCatalog.Domain.Enums;

namespace RiskCatalog.Domain.RiskModels;

public class RiskMatrix
{
    public Guid Id { get; set; }
    public Guid EventTypeId { get; set; }
    public SeverityLevelEnum SeverityLevel { get; set; }
    public RiskLevelEnum RiskLevel { get; set; }
    public int Version { get; set; }

    public RiskMatrix()
    {
    }

    public RiskMatrix(Guid id, Guid eventTypeId, SeverityLevelEnum severityLevel, RiskLevelEnum riskLevel, int version)
    {
        Id = id;
        EventTypeId = eventTypeId;
        SeverityLevel = severityLevel;
        RiskLevel = riskLevel;
        Version = version;
    }
}
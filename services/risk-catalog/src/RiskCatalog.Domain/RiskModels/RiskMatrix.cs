using System.ComponentModel.DataAnnotations;
using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.EventTypes;

namespace RiskCatalog.Domain.RiskModels;

public class RiskMatrix
{
    [Key] public Guid Id { get; set; }
    public Guid EventTypeId { get; set; }
    public SeverityLevelEnum SeverityLevel { get; set; }
    public RiskLevelEnum RiskLevel { get; set; }
    public int Version { get; set; }
    public virtual EventType EventType { get; set; }

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
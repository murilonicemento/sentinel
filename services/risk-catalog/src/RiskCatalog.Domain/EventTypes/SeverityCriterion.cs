using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Domain.EventTypes;

public class SeverityCriterion
{
    [Key] public Guid Id { get; set; }
    public Guid EventTypeId { get; set; }
    public Guid SeverityId { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public string Unit { get; set; }
    public int Version { get; set; }
    public virtual EventType EventType { get; set; }
    public virtual Severity Severity { get; set; }

    public SeverityCriterion()
    {
    }

    public SeverityCriterion(
        Guid id,
        Guid eventTypeId,
        Guid severityId,
        double minValue,
        int maxValue,
        string unit,
        int version
    )
    {
        Id = id;
        EventTypeId = eventTypeId;
        SeverityId = severityId;
        MinValue = minValue;
        MaxValue = maxValue;
        Unit = unit;
        Version = version;
    }
}
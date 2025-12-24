using RiskCatalog.Domain.Enums;

namespace RiskCatalog.Domain.EventTypes;

public class Severity
{
    public Guid Id { get; set; }
    public SeverityLevelEnum Level { get; set; }
    public string Description { get; set; }

    public Severity()
    {
    }

    public Severity(Guid id, SeverityLevelEnum level, string description)
    {
        Id = id;
        Level = level;
        Description = description;
    }
}
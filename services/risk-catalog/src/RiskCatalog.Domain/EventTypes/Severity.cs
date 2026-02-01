using System.ComponentModel.DataAnnotations;
using RiskCatalog.Domain.Enums;

namespace RiskCatalog.Domain.EventTypes;

public class Severity
{
    [Key] public Guid Id { get; set; }
    public SeverityLevelEnum Level { get; set; }
    public string Description { get; set; }
    public virtual ICollection<SeverityCriterion> SeverityCriteria { get; set; }

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
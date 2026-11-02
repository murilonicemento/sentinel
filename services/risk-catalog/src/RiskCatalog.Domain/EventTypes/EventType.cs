using System.ComponentModel.DataAnnotations;
using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Domain.EventTypes;

public class EventType
{
    [Key]
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public virtual ICollection<SeverityCriterion> SeverityCriteria { get; set; } = [];
    public virtual ICollection<IDFCurve> IDFCurves { get; set; } = [];
    public virtual ICollection<RiskMatrix> RiskMatrix { get; set; } = [];

    public EventType()
    {
    }

    public EventType(Guid id, string code, string name, string description, bool isActive)
    {
        Id = id;
        Code = code;
        Name = name;
        Description = description;
        IsActive = isActive;
    }
}
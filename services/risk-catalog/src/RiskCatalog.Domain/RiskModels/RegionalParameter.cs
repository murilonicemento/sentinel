using System.ComponentModel.DataAnnotations;

namespace RiskCatalog.Domain.RiskModels;

public class RegionalParameter
{
    [Key]
    public Guid Id { get; set; }
    public Guid RegionId { get; set; }
    public double AdjustmentFactor { get; set; }
    public string Description { get; set; }

    public RegionalParameter()
    {
    }

    public RegionalParameter(Guid id, Guid regionId, double adjustmentFactor, string description)
    {
        Id = id;
        RegionId = regionId;
        AdjustmentFactor = adjustmentFactor;
        Description = description;
    }
}
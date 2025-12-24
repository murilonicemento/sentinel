namespace RiskCatalog.Domain.RiskModels;

public class RegionalParameter
{
    public Guid Id { get; set; }
    public Guid RegionId { get; set; }
    public double AdjustementFactor { get; set; }
    public string Description { get; set; }

    public RegionalParameter()
    {
    }

    public RegionalParameter(Guid id, Guid regionId, double adjustementFactor, string description)
    {
        Id = id;
        RegionId = regionId;
        AdjustementFactor = adjustementFactor;
        Description = description;
    }
}
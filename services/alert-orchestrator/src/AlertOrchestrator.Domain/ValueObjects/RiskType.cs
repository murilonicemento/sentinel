namespace AlertOrchestrator.Domain.ValueObjects;

public sealed record RiskType
{
    private RiskType(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static RiskType Flood => new("flood");
    public static RiskType Wildfire => new("wildfire");
    public static RiskType Earthquake => new("earthquake");
    public static RiskType Hurricane => new("hurricane");
    public static RiskType Tornado => new("tornado");
    public static RiskType Drought => new("drought");
    public static RiskType Landslide => new("landslide");
    public static RiskType Tsunami => new("tsunami");
    public static RiskType VolcanicEruption => new("volcanic_eruption");
    public static RiskType ExtremeHeat => new("extreme_heat");
    public static RiskType ExtremeCold => new("extreme_cold");
    public static RiskType SevereStorm => new("severe_storm");
    public static RiskType AirQuality => new("air_quality");

    public static RiskType FromString(string value)
    {
        var normalized = value.ToLowerInvariant().Replace(" ", "_");
        return new RiskType(normalized);
    }

    public override string ToString()
    {
        return Value;
    }
}
using Ingestion.Domain.ValueObjects;

namespace Ingestion.UnitTests;

public class MeasurementTypeTests
{
    /// <summary>Regression: Fire was stored as "WildFire", which the unit table never matched.</summary>
    [Theory]
    [InlineData("Fire", "kW/m²", true)]
    [InlineData("Fire", "MW", true)]
    [InlineData("WildFire", "MW", true)]
    [InlineData("Fire", "K", false)]
    public void Fire_AcceptsItsUnits_UnderTheCurrentAndLegacyName(string type, string unit, bool expected)
    {
        Assert.Equal(expected, MeasurementType.From(type).IsValidUnit(unit));
        Assert.Equal("Fire", MeasurementType.From(type).Value);
    }
}

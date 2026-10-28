using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Enums;
using Xunit;

namespace RiskEvaluation.UnitTests;

public class RiskCatalogSnapshotTests
{
    private static RegionalAdjustment Circle(double factor, double lat, double lon, double radiusKm) =>
        new(Guid.NewGuid(), factor, lat, lon, radiusKm, []);

    [Fact]
    public void RegionalFactor_UsesTheHighestFactorAmongContainingRegions()
    {
        var snapshot = new RiskCatalogSnapshot(1, DateTime.UtcNow,
            [Circle(1.3, -23.5, -46.6, 100), Circle(1.6, -23.5, -46.6, 10), Circle(1.9, 0, 0, 10)], [], []);

        Assert.Equal(1.6, snapshot.RegionalFactorAt(-23.5, -46.6));
        Assert.Equal(1.3, snapshot.RegionalFactorAt(-23.0, -46.6));
        Assert.Equal(1.0, snapshot.RegionalFactorAt(10, 10));
    }

    [Theory]
    [InlineData(5.0, 2.0)]
    [InlineData(0.1, 0.5)]
    public void RegionalFactor_IsBoundedLikeTheGeographicMultiplier(double factor, double expected)
    {
        var snapshot = new RiskCatalogSnapshot(1, DateTime.UtcNow, [Circle(factor, 0, 0, 10)], [], []);

        Assert.Equal(expected, snapshot.RegionalFactorAt(0, 0));
    }

    [Fact]
    public void CircleContainment_UsesGreatCircleDistance()
    {
        // Rio de Janeiro to São Paulo is about 360 km.
        var region = Circle(1.5, -22.9068, -43.1729, 350);

        Assert.False(region.Contains(-23.5505, -46.6333));
        Assert.True(Circle(1.5, -22.9068, -43.1729, 370).Contains(-23.5505, -46.6333));
    }

    [Fact]
    public void PolygonContainment_TakesPrecedenceOverTheCircle()
    {
        var region = new RegionalAdjustment(Guid.NewGuid(), 1.5, 0, 0, 10_000,
            [new GeoCoordinate(-23, -47), new GeoCoordinate(-23, -46), new GeoCoordinate(-24, -46)]);

        Assert.True(region.Contains(-23.2, -46.2));
        Assert.False(region.Contains(-23.8, -46.8));
    }

    [Fact]
    public void MatrixLevel_IsNullWithoutARuleForTheEvent()
    {
        var snapshot = new RiskCatalogSnapshot(1, DateTime.UtcNow, [],
            [new SeverityRule("Flood", "High", 5, 10)], [new RiskMatrixRule("Flood", "High", RiskLevel.Critical)]);

        Assert.Equal(RiskLevel.Critical, snapshot.MatrixLevelFor("FLOOD", 10));
        Assert.Null(snapshot.MatrixLevelFor("Flood", 4.9));
        Assert.Null(snapshot.MatrixLevelFor("Wildfire", 8));
        Assert.Null(snapshot.MatrixLevelFor(null, 8));
    }
}

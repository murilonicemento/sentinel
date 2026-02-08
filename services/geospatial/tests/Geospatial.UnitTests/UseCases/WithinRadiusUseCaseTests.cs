using Geospatial.Application.UseCases;
using Geospatial.Domain.Services;
using Geospatial.Domain.ValueObjects;
using Moq;

namespace Geospatial.UnitTests.UseCases;

public class WithinRadiusUseCaseTests
{
    private readonly Mock<IGeospatialCalculator> _mockCalculator;
    private readonly WithinRadiusUseCase _sut;

    public WithinRadiusUseCaseTests()
    {
        _mockCalculator = new Mock<IGeospatialCalculator>();
        _sut = new WithinRadiusUseCase(_mockCalculator.Object);
    }

    [Fact]
    public void Execute_WhenPointWithinRadius_ReturnsTrueAndDistance()
    {
        var center = new GeoPoint(-23.5505, -46.6333);
        var point = new GeoPoint(-23.5506, -46.6334);
        var radius = new GeoRadius(500, DistanceUnit.Meters);

        _mockCalculator.Setup(x => x.Distance(center, point)).Returns(150.0);

        var (withinRadius, distance) = _sut.Execute(center, point, radius);

        Assert.True(withinRadius);
        Assert.Equal(150.0, distance);
    }

    [Fact]
    public void Execute_WhenPointOutsideRadius_ReturnsFalseAndDistance()
    {
        var center = new GeoPoint(-23.5505, -46.6333);
        var point = new GeoPoint(-23.5600, -46.6400);
        var radius = new GeoRadius(100, DistanceUnit.Meters);

        _mockCalculator.Setup(x => x.Distance(center, point)).Returns(1500.0);

        var (withinRadius, distance) = _sut.Execute(center, point, radius);

        Assert.False(withinRadius);
        Assert.Equal(1500.0, distance);
    }

    [Fact]
    public void Execute_WhenPointExactlyAtRadiusBoundary_ReturnsTrue()
    {
        var center = new GeoPoint(0, 0);
        var point = new GeoPoint(0, 0);
        var radius = new GeoRadius(100, DistanceUnit.Meters);

        _mockCalculator.Setup(x => x.Distance(center, point)).Returns(100.0);

        var (withinRadius, distance) = _sut.Execute(center, point, radius);

        Assert.True(withinRadius);
        Assert.Equal(100.0, distance);
    }

    [Fact]
    public void Execute_WhenRadiusInKilometers_ConvertsCorrectly()
    {
        var center = new GeoPoint(0, 0);
        var point = new GeoPoint(0, 0);
        var radius = new GeoRadius(1, DistanceUnit.Kilometers);

        _mockCalculator.Setup(x => x.Distance(center, point)).Returns(999.0);

        var (withinRadius, _) = _sut.Execute(center, point, radius);

        Assert.True(withinRadius);
    }
}

using Geospatial.Application.UseCases;
using Geospatial.Domain.Events;
using Geospatial.Domain.Repositories;
using Moq;

namespace Geospatial.UnitTests.UseCases;

public class SearchGeospatialEventsUseCaseTests
{
    private readonly Mock<IGeospatialEventRepository> _mockRepository;
    private readonly SearchGeospatialEventsUseCase _sut;

    public SearchGeospatialEventsUseCaseTests()
    {
        _mockRepository = new Mock<IGeospatialEventRepository>();
        _sut = new SearchGeospatialEventsUseCase(_mockRepository.Object);
    }

    [Fact]
    public async Task SearchByRadiusAsync_ReturnsEventsFromRepository()
    {
        IReadOnlyList<GeospatialOperationEvent> expected = [new() { OperationType = "distance" }];
        _mockRepository
            .Setup(r => r.SearchByPointAsync(-23.55, -46.63, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.SearchByRadiusAsync(-23.55, -46.63, 1000);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task SearchByPolygonAsync_ForwardsPolygonPointsInOrder()
    {
        IReadOnlyList<(double Latitude, double Longitude)> polygon = [(-24, -47), (-24, -46), (-23, -46)];
        IReadOnlyList<(double Lat, double Lon)>? forwarded = null;
        _mockRepository
            .Setup(r => r.SearchByPolygonAsync(It.IsAny<IReadOnlyList<(double Lat, double Lon)>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<(double Lat, double Lon)>, CancellationToken>((p, _) => forwarded = p)
            .ReturnsAsync([]);

        var result = await _sut.SearchByPolygonAsync(polygon);

        Assert.Empty(result);
        Assert.NotNull(forwarded);
        Assert.Equal(polygon.Select(p => (p.Latitude, p.Longitude)), forwarded!.Select(p => (p.Lat, p.Lon)));
    }
}

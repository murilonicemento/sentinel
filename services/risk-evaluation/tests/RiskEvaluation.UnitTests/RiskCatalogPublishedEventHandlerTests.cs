using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RiskEvaluation.Application.Contracts;
using RiskEvaluation.Application.Handlers;
using RiskEvaluation.Application.Interfaces.HttpClients;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Domain.Repositories;
using Xunit;

namespace RiskEvaluation.UnitTests;

public class RiskCatalogPublishedEventHandlerTests
{
    [Fact]
    public async Task Publication_RecordsTheVersionAndInvalidatesCachedScores()
    {
        var versions = new Mock<ICatalogVersionRepository>();
        var cache = new Mock<IRecentScoresCache>();
        var snapshots = new Mock<ICatalogSnapshotRepository>();
        var client = new Mock<IRiskCatalogClient>();
        var snapshot = new RiskCatalogSnapshot(4, DateTime.UtcNow, [], [], []);
        client.Setup(c => c.FetchSnapshotAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        var publishedBy = Guid.NewGuid();

        await new RiskCatalogPublishedEventHandler(versions.Object, snapshots.Object, client.Object, cache.Object,
                NullLogger<RiskCatalogPublishedEventHandler>.Instance)
            .Handle(new RiskCatalogPublishedEvent
            {
                Version = 4,
                Notes = "Flood thresholds",
                PublishedAt = DateTime.UtcNow,
                PublishedBy = publishedBy
            }, CancellationToken.None);

        versions.Verify(v => v.SaveAsync(It.Is<CatalogVersion>(c => c.Version == 4 && c.PublishedBy == publishedBy),
            It.IsAny<CancellationToken>()), Times.Once);
        snapshots.Verify(s => s.SaveAsync(snapshot, It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.InvalidateAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnreachableCatalog_PropagatesSoThePublicationIsRetried()
    {
        var cache = new Mock<IRecentScoresCache>();
        var snapshots = new Mock<ICatalogSnapshotRepository>();
        var client = new Mock<IRiskCatalogClient>();
        client.Setup(c => c.FetchSnapshotAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("risk-catalog unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() => new RiskCatalogPublishedEventHandler(
                Mock.Of<ICatalogVersionRepository>(), snapshots.Object, client.Object, cache.Object,
                NullLogger<RiskCatalogPublishedEventHandler>.Instance)
            .Handle(new RiskCatalogPublishedEvent { Version = 5 }, CancellationToken.None));

        snapshots.VerifyNoOtherCalls();
        cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublicationWithoutVersion_IsRejectedByTheDomain()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new RiskCatalogPublishedEventHandler(
                Mock.Of<ICatalogVersionRepository>(), Mock.Of<ICatalogSnapshotRepository>(),
                Mock.Of<IRiskCatalogClient>(), Mock.Of<IRecentScoresCache>(),
                NullLogger<RiskCatalogPublishedEventHandler>.Instance)
            .Handle(new RiskCatalogPublishedEvent { Version = 0 }, CancellationToken.None));
    }
}

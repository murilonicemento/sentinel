using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RiskEvaluation.Application.Contracts;
using RiskEvaluation.Application.Handlers;
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
        var publishedBy = Guid.NewGuid();

        await new RiskCatalogPublishedEventHandler(versions.Object, cache.Object,
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
        cache.Verify(c => c.InvalidateAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublicationWithoutVersion_IsRejectedByTheDomain()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new RiskCatalogPublishedEventHandler(
                Mock.Of<ICatalogVersionRepository>(), Mock.Of<IRecentScoresCache>(),
                NullLogger<RiskCatalogPublishedEventHandler>.Instance)
            .Handle(new RiskCatalogPublishedEvent { Version = 0 }, CancellationToken.None));
    }
}

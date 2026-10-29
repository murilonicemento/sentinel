using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Infrastructure.Caching;
using RiskEvaluation.Infrastructure.Messaging;
using RiskEvaluation.Infrastructure.Persistence;
using StackExchange.Redis;
using Testcontainers.MongoDb;
using Testcontainers.Redis;

namespace RiskEvaluation.IntegrationTests.Persistence;

public sealed class StorageFixture : IAsyncLifetime
{
    // No credentials: the mongo image then skips the temporary init server, whose port was sometimes still bound
    // when the real mongod started under the load of the whole solution run (exit 48, "Address already in use").
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7").WithUsername(string.Empty).WithPassword(string.Empty)
        .Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();

    public IMongoDatabase Database { get; private set; } = null!;
    public IConnectionMultiplexer Redis { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mongo.StartAsync(), _redis.StartAsync());
        Database = new MongoClient(_mongo.GetConnectionString()).GetDatabase("RiskEvaluationDatabase");
        Redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString() + ",allowAdmin=true");
    }

    public async Task DisposeAsync()
    {
        await Redis.DisposeAsync();
        await Task.WhenAll(_mongo.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}

public class RiskEvaluationStorageTests : IClassFixture<StorageFixture>
{
    private readonly StorageFixture _fixture;

    public RiskEvaluationStorageTests(StorageFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CatalogVersions_AreUpsertedAndReadBack()
    {
        var repository = new CatalogVersionRepository(_fixture.Database);
        var publishedBy = Guid.NewGuid();

        await repository.SaveAsync(new CatalogVersion(7, "first", DateTime.UtcNow, publishedBy, DateTime.UtcNow));
        await repository.SaveAsync(new CatalogVersion(7, "redelivered", DateTime.UtcNow, publishedBy, DateTime.UtcNow));
        await repository.SaveAsync(new CatalogVersion(3, "older", DateTime.UtcNow, publishedBy, DateTime.UtcNow));

        var latest = await repository.GetLatestAsync();
        Assert.Equal(7, latest!.Version);
        Assert.Equal("redelivered", latest.Notes);
        Assert.Equal(1, await _fixture.Database.GetCollection<CatalogVersion>("CatalogVersions")
            .CountDocumentsAsync(Builders<CatalogVersion>.Filter.Eq(c => c.Version, 7)));
    }

    [Fact]
    public async Task InvalidateAll_RemovesOnlyCachedScores()
    {
        var cache = new RedisRecentScoresCache(_fixture.Redis, NullLogger<RedisRecentScoresCache>.Instance);
        var database = _fixture.Redis.GetDatabase();
        await cache.SetAsync(-23, -46, new RiskScoreCacheEntry { Score = 0.4, Level = "Low" });
        await cache.SetAsync(-22, -43, new RiskScoreCacheEntry { Score = 0.9, Level = "High" });
        await database.StringSetAsync("risk-evaluation:consumed:v1:other", "done");

        var removed = await cache.InvalidateAllAsync();

        Assert.True(removed >= 2);
        Assert.Null(await cache.GetAsync(-23, -46));
        Assert.True(await database.KeyExistsAsync("risk-evaluation:consumed:v1:other"));
    }

    [Fact]
    public async Task ConsumedMessageGuard_LetsOnlyOneConsumerClaimAMessage()
    {
        var guard = new RedisConsumedMessageGuard(_fixture.Redis);
        var key = $"climatic-event-detected:0:{Random.Shared.Next()}";

        var claims = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => guard.TryBeginAsync(key)));
        await guard.CompleteAsync(key);
        await guard.AbandonAsync(key);

        Assert.Equal(1, claims.Count(c => c == ConsumedMessageClaim.Acquired));
        Assert.Equal(ConsumedMessageClaim.AlreadyProcessed, await guard.TryBeginAsync(key));
    }

    [Fact]
    public async Task CatalogSnapshots_RoundTripAndTheLatestVersionWins()
    {
        var repository = new CatalogSnapshotRepository(_fixture.Database);
        await repository.SaveAsync(new RiskCatalogSnapshot(41, DateTime.UtcNow,
            [new RegionalAdjustment(Guid.NewGuid(), 1.5, null, null, null,
                [new GeoCoordinate(-23, -47), new GeoCoordinate(-23, -46), new GeoCoordinate(-24, -46)])],
            [new SeverityRule("Flood", "High", 5, 10)], [new RiskMatrixRule("Flood", "High", Domain.Enums.RiskLevel.Critical)]));
        await repository.SaveAsync(new RiskCatalogSnapshot(40, DateTime.UtcNow, [], [], []));

        var latest = await repository.GetLatestAsync();

        Assert.Equal(41, latest!.Version);
        Assert.Equal(1.5, latest.RegionalFactorAt(-23.2, -46.2));
        Assert.Equal(Domain.Enums.RiskLevel.Critical, latest.MatrixLevelFor("Flood", 6));
    }
}

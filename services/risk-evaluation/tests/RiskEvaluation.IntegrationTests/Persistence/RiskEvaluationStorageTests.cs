using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
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
        // Program.cs registers it at startup; the repositories here are built without the host.
        MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(
            new MongoDB.Bson.Serialization.Serializers.GuidSerializer(GuidRepresentation.Standard));
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

    /// <summary>RE-18: two points in the same whole degree no longer share a cached score.</summary>
    [Fact]
    public async Task RecentScores_AreCachedByExactPosition()
    {
        var cache = new RedisRecentScoresCache(_fixture.Redis, NullLogger<RedisRecentScoresCache>.Instance);
        await cache.SetAsync(-12.95, -38.45, new RiskScoreCacheEntry { Score = 0.8, Level = "High" });

        Assert.Equal(0.8, (await cache.GetAsync(-12.95, -38.45))!.Score);
        Assert.Null(await cache.GetAsync(-12.10, -38.90));
    }

    private RiskEvaluationRepository Evaluations(out IMongoCollection<MongoDB.Bson.BsonDocument> raw)
    {
        // Own database: the cell queries below count every evaluation they find.
        var database = _fixture.Database.Client.GetDatabase($"evaluations-{Guid.NewGuid():N}");
        raw = database.GetCollection<MongoDB.Bson.BsonDocument>("RiskEvaluations");
        return new RiskEvaluationRepository(database, NullLogger<RiskEvaluationRepository>.Instance);
    }

    [Fact]
    public async Task Evaluations_KeepTheExactPosition()
    {
        var repository = Evaluations(out _);
        var evaluation = new RiskEvaluationEntity(-22.95, -43.25, 0.7, Domain.Enums.RiskLevel.High);
        await repository.AddAsync(evaluation);

        var stored = await repository.GetByIdAsync(evaluation.Id);

        Assert.Equal((-22.95, -43.25), (stored!.Latitude, stored.Longitude));
    }

    /// <summary>
    /// The history groups by whole-degree cell (truncated toward zero) and still finds the evaluations stored before
    /// RE-18, whose coordinates are integers.
    /// </summary>
    [Fact]
    public async Task History_ReturnsTheWholeDegreeCellIncludingOldIntegerDocuments()
    {
        var repository = Evaluations(out var raw);
        var old = new RiskEvaluationEntity(0, 0, 0.3, Domain.Enums.RiskLevel.Low).ToBsonDocument();
        old["Latitude"] = -22;
        old["Longitude"] = -43;
        await raw.InsertOneAsync(old);
        await repository.AddAsync(new RiskEvaluationEntity(-22.95, -43.25, 0.7, Domain.Enums.RiskLevel.High));
        await repository.AddAsync(new RiskEvaluationEntity(-23.01, -43.25, 0.7, Domain.Enums.RiskLevel.High));
        await repository.AddAsync(new RiskEvaluationEntity(-21.99, -43.25, 0.7, Domain.Enums.RiskLevel.High));

        var cell = await repository.GetByLocationAsync(-22, -43);

        Assert.Equal([-22.95, -22.0], cell.Select(e => e.Latitude).OrderBy(l => l));
    }

    [Theory]
    [InlineData(0.5, 0, 0)]
    [InlineData(-0.5, 0, 0)]
    [InlineData(5.99, 5, 0)]
    [InlineData(-5.01, -5, 0)]
    public async Task History_CellMatchesTheTruncatedDegree(double latitude, int cellLatitude, int cellLongitude)
    {
        var repository = Evaluations(out _);
        await repository.AddAsync(new RiskEvaluationEntity(latitude, 0.25, 0.5, Domain.Enums.RiskLevel.Medium));

        Assert.Single(await repository.GetByLocationAsync(cellLatitude, cellLongitude));
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

    [Fact]
    public async Task CatalogSnapshots_KeepTheRegionCode()
    {
        // Own database: the latest version is global, and the other snapshot test relies on it.
        var repository = new CatalogSnapshotRepository(
            _fixture.Database.Client.GetDatabase($"snapshots-{Guid.NewGuid():N}"));
        await repository.SaveAsync(new RiskCatalogSnapshot(90, DateTime.UtcNow,
            [new RegionalAdjustment(Guid.NewGuid(), 1.2, -22.9, -43.2, 50, [], "rj-capital")], [], []));

        var latest = await repository.GetLatestAsync();

        Assert.Equal("rj-capital", latest!.RegionAt(-22.9, -43.2)!.Code);
    }
}

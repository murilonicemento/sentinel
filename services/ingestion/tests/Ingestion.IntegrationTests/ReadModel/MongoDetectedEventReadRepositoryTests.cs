using Ingestion.Application.DTO;
using Ingestion.Infrastructure.Read.Persistence.DbContext;
using Ingestion.Infrastructure.Read.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace Ingestion.IntegrationTests.ReadModel;

public sealed class MongoReadModelFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:7").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

public class MongoDetectedEventReadRepositoryTests : IClassFixture<MongoReadModelFixture>, IAsyncLifetime
{
    private static readonly DateTime Start = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly MongoReadModelFixture _fixture;
    private readonly IMongoDatabase _database;
    private readonly MongoDetectedEventReadRepository _repository;

    public MongoDetectedEventReadRepositoryTests(MongoReadModelFixture fixture)
    {
        _fixture = fixture;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IngestionReadDatabase"] = fixture.ConnectionString
            })
            .Build();
        _repository = new MongoDetectedEventReadRepository(new ReadDbContext(configuration));
        _database = new MongoClient(fixture.ConnectionString).GetDatabase("IngestionReadDatabase");
    }

    // Mirrors platform/init-db/mongodb/2-schema.js, whose unique EventId index rejected projections without an id.
    public async Task InitializeAsync()
    {
        await _database.DropCollectionAsync("climaticEvents");
        await _database.DropCollectionAsync("disasterEvents");
        foreach (var name in new[] { "climaticEvents", "disasterEvents" })
            await _database.GetCollection<BsonDocument>(name).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("EventId"),
                    new CreateIndexOptions { Unique = true }));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static DetectedEventDTO Event(Guid tenantId, string domain, string eventType, double intensity,
        int secondsAfterStart) =>
        new(Guid.NewGuid(), Guid.NewGuid(), tenantId, domain, eventType, intensity, -23.55, -46.63,
            Start.AddSeconds(secondsAfterStart));

    [Fact]
    public async Task AddAsync_ProjectsSeveralEventsIntoTheSameCollection()
    {
        var tenant = Guid.NewGuid();

        await _repository.AddAsync(Event(tenant, "Climatic", "Rainfall", 10, 0), CancellationToken.None);
        await _repository.AddAsync(Event(tenant, "Climatic", "WindGust", 20, 1), CancellationToken.None);

        Assert.Equal(2, await _database.GetCollection<BsonDocument>("climaticEvents")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task AddAsync_WithAnAlreadyProjectedEventId_KeepsOneDocument()
    {
        var detected = Event(Guid.NewGuid(), "Climatic", "Rainfall", 10, 0);

        await _repository.AddAsync(detected, CancellationToken.None);
        await _repository.AddAsync(detected, CancellationToken.None);

        Assert.Equal(1, await _database.GetCollection<BsonDocument>("climaticEvents")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task GetPageAsync_MergesBothDomainsNewestFirstAndIsolatesTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await _repository.AddAsync(Event(tenantA, "Climatic", "Rainfall", 10, 0), CancellationToken.None);
        await _repository.AddAsync(Event(tenantA, "Disaster", "Flood", 30, 2), CancellationToken.None);
        await _repository.AddAsync(Event(tenantA, "Climatic", "WindGust", 20, 1), CancellationToken.None);
        await _repository.AddAsync(Event(tenantB, "Disaster", "Earthquake", 90, 3), CancellationToken.None);

        var firstPage = await _repository.GetPageAsync(TenantAccessScope.ForTenants([tenantA]), 1, 2,
            CancellationToken.None);
        var secondPage = await _repository.GetPageAsync(TenantAccessScope.ForTenants([tenantA]), 2, 2,
            CancellationToken.None);

        Assert.Equal(3, firstPage.TotalItems);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(["Flood", "WindGust"], firstPage.Items.Select(e => e.EventType));
        Assert.Equal("Disaster", firstPage.Items[0].Domain);
        Assert.Equal("Rainfall", Assert.Single(secondPage.Items).EventType);
        Assert.All(firstPage.Items.Concat(secondPage.Items), e => Assert.Equal(tenantA, e.TenantId));
    }

    [Fact]
    public async Task GetPageAsync_WithPlatformScope_IncludesDocumentsWithoutTenant()
    {
        await _database.GetCollection<BsonDocument>("disasterEvents").InsertOneAsync(new BsonDocument
        {
            ["EventId"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
            ["EventType"] = "Wildfire",
            ["Intensity"] = 12.5,
            ["Latitude"] = -23.55,
            ["Longitude"] = -46.63,
            ["CollectedAt"] = Start
        });

        var platform = await _repository.GetPageAsync(TenantAccessScope.Platform, 1, 10, CancellationToken.None);
        var tenant = await _repository.GetPageAsync(TenantAccessScope.ForTenants([Guid.NewGuid()]), 1, 10,
            CancellationToken.None);

        Assert.Equal("Wildfire", Assert.Single(platform.Items).EventType);
        Assert.Equal(0, tenant.TotalItems);
    }

    [Fact]
    public async Task GetStatisticsAsync_AggregatesBothDomainsWithinTheDateRange()
    {
        var tenant = Guid.NewGuid();
        await _repository.AddAsync(Event(tenant, "Climatic", "Rainfall", 10, 0), CancellationToken.None);
        await _repository.AddAsync(Event(tenant, "Climatic", "Rainfall", 20, 10), CancellationToken.None);
        await _repository.AddAsync(Event(tenant, "Disaster", "Flood", 60, 20), CancellationToken.None);
        await _repository.AddAsync(Event(tenant, "Disaster", "Flood", 99, 3600), CancellationToken.None);
        await _repository.AddAsync(Event(Guid.NewGuid(), "Disaster", "Flood", 5, 5), CancellationToken.None);

        var statistics = await _repository.GetStatisticsAsync(TenantAccessScope.ForTenants([tenant]), Start,
            Start.AddMinutes(1), CancellationToken.None);

        Assert.Equal(3, statistics.TotalEvents);
        Assert.Equal(2, statistics.TotalByType["Rainfall"]);
        Assert.Equal(1, statistics.TotalByType["Flood"]);
        Assert.Equal(10, statistics.MinIntensity);
        Assert.Equal(60, statistics.MaxIntensity);
        Assert.Equal(30, statistics.AverageIntensity, precision: 6);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithoutEvents_ReturnsZeroes()
    {
        var statistics = await _repository.GetStatisticsAsync(TenantAccessScope.ForTenants([Guid.NewGuid()]), null,
            null, CancellationToken.None);

        Assert.Equal(0, statistics.TotalEvents);
        Assert.Empty(statistics.TotalByType);
    }
}

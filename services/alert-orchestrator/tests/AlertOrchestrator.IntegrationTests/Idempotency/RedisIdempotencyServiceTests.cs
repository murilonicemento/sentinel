using AlertOrchestrator.Application.Interfaces.Messaging;
using AlertOrchestrator.Infrastructure.Idempotency;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace AlertOrchestrator.IntegrationTests.Idempotency;

public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7").Build();

    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Connection = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await Connection.DisposeAsync();
        await _container.DisposeAsync();
    }
}

public class RedisIdempotencyServiceTests : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _fixture;

    public RedisIdempotencyServiceTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    private RedisIdempotencyService Service(TimeSpan? lease = null) => new(_fixture.Connection, lease: lease);

    [Fact]
    public async Task ConcurrentClaims_OnlyOneConsumerAcquiresTheEvent()
    {
        var eventId = Guid.NewGuid();
        var service = Service();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => service.TryBeginAsync(eventId)));

        Assert.Equal(1, results.Count(r => r == IdempotencyClaimResult.Acquired));
        Assert.All(results.Where(r => r != IdempotencyClaimResult.Acquired),
            r => Assert.Equal(IdempotencyClaimResult.InProgress, r));
    }

    [Fact]
    public async Task CompletedEvent_IsReportedAsAlreadyProcessedAndCannotBeAbandoned()
    {
        var eventId = Guid.NewGuid();
        var service = Service();
        await service.TryBeginAsync(eventId);
        await service.CompleteAsync(eventId);

        await service.AbandonAsync(eventId);

        Assert.Equal(IdempotencyClaimResult.AlreadyProcessed, await service.TryBeginAsync(eventId));
    }

    [Fact]
    public async Task AbandonedClaim_CanBeAcquiredAgain()
    {
        var eventId = Guid.NewGuid();
        var service = Service();
        await service.TryBeginAsync(eventId);

        await service.AbandonAsync(eventId);

        Assert.Equal(IdempotencyClaimResult.Acquired, await service.TryBeginAsync(eventId));
    }

    [Fact]
    public async Task ExpiredLease_AllowsAnotherConsumerToTakeOver()
    {
        var eventId = Guid.NewGuid();
        var service = Service(lease: TimeSpan.FromMilliseconds(200));
        await service.TryBeginAsync(eventId);

        await Task.Delay(TimeSpan.FromMilliseconds(400));

        Assert.Equal(IdempotencyClaimResult.Acquired, await service.TryBeginAsync(eventId));
    }

    [Fact]
    public async Task EventMarkedByThePreviousVersion_IsStillAlreadyProcessed()
    {
        var eventId = Guid.NewGuid();
        // IDistributedCache (RedisCache) stores entries as hashes under the bare key.
        await _fixture.Connection.GetDatabase().HashSetAsync($"idempotency:{eventId}", "data", "processed");

        Assert.Equal(IdempotencyClaimResult.AlreadyProcessed, await Service().TryBeginAsync(eventId));
    }
}

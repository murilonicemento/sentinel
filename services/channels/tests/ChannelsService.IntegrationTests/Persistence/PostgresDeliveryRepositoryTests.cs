using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.Infrastructure.Persistence;
using ChannelsService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ChannelsService.IntegrationTests.Persistence;

/// <summary>Postgres 15.5 with the channels schema from platform/init-db/postgres/2-schema.sql.</summary>
public sealed class ChannelsPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:15.5").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sentinel.sln")))
            directory = directory.Parent;
        var script = await File.ReadAllTextAsync(Path.Combine(directory!.FullName, "platform", "init-db", "postgres",
            "2-schema.sql"));
        var section = script[(script.IndexOf("\\c channels;", StringComparison.Ordinal) + "\\c channels;".Length)..];
        var next = section.IndexOf("\\c ", StringComparison.Ordinal);
        if (next >= 0) section = section[..next];

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(section, connection);
        await command.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public ChannelsServiceDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ChannelsServiceDbContext>().UseNpgsql(ConnectionString).Options);
}

public class PostgresDeliveryRepositoryTests : IClassFixture<ChannelsPostgresFixture>
{
    private readonly ChannelsPostgresFixture _fixture;

    public PostgresDeliveryRepositoryTests(ChannelsPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeliveryAttempts_RoundTripWithTheirStatus()
    {
        var eventId = $"evt-{Guid.NewGuid():N}";
        await using (var context = _fixture.CreateContext())
            await new PostgresDeliveryRepository(context).AddAsync(new DeliveryAttempt
            {
                AttemptId = Guid.NewGuid().ToString("N"),
                EventId = eventId,
                TenantId = "tenant-a",
                Channel = ChannelTypeEnum.Sms,
                Provider = "twilio",
                StatusEnum = DeliveryStatusEnum.Retried,
                AttemptCount = 2,
                Timestamp = DateTime.UtcNow
            });

        await using (var context = _fixture.CreateContext())
        {
            var attempt = Assert.Single(await new PostgresDeliveryRepository(context).GetByEventAsync(eventId));
            Assert.Equal(DeliveryStatusEnum.Retried, attempt.StatusEnum);
            Assert.False(context.Database.HasPendingModelChanges());
        }
    }
}

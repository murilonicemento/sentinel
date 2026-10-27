using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Domain.Enums;
using AlertOrchestrator.Domain.ValueObjects;
using AlertOrchestrator.Infrastructure.Persistence;
using AlertOrchestrator.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AlertOrchestrator.IntegrationTests.Persistence;

/// <summary>Postgres 15.5 with the alert_orchestrator schema from platform/init-db/postgres/2-schema.sql.</summary>
public sealed class AlertOrchestratorPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:15.5").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(ScriptSection("alert_orchestrator"), connection);
        await command.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public AlertOrchestratorDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AlertOrchestratorDbContext>().UseNpgsql(ConnectionString).Options);

    private static string ScriptSection(string database)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sentinel.sln")))
            directory = directory.Parent;
        var script = File.ReadAllText(Path.Combine(directory!.FullName, "platform", "init-db", "postgres",
            "2-schema.sql"));

        var start = script.IndexOf($"\\c {database};", StringComparison.Ordinal);
        var section = script[(start + database.Length + 4)..];
        var next = section.IndexOf("\\c ", StringComparison.Ordinal);
        return next < 0 ? section : section[..next];
    }
}

public class PostgreSqlAlertWindowRepositoryTests : IClassFixture<AlertOrchestratorPostgresFixture>
{
    private readonly AlertOrchestratorPostgresFixture _fixture;

    public PostgreSqlAlertWindowRepositoryTests(AlertOrchestratorPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static Signal SignalFrom(SignalSource source) => new(source, DateTime.UtcNow, Guid.NewGuid(), 0.9,
        RiskType.Flood, new Dictionary<string, string> { ["source"] = source.ToString() });

    private PostgreSqlAlertWindowRepository Repository(AlertOrchestratorDbContext context) =>
        new(context, NullLogger<PostgreSqlAlertWindowRepository>.Instance);

    [Fact]
    public async Task AddingSignalsAcrossMessages_PersistsEverySignalOnce()
    {
        var region = $"region-{Guid.NewGuid():N}";
        await using (var context = _fixture.CreateContext())
            await Repository(context).AddAsync(
                AlertWindow.Open(region, RiskType.Flood, "tenant-a", 0.5, TimeSpan.FromHours(1)));

        // Each message is handled in its own scope, as the consumer does.
        foreach (var source in new[] { SignalSource.Sensor, SignalSource.Satellite })
        {
            await using var context = _fixture.CreateContext();
            var repository = Repository(context);
            var window = await repository.GetOpenWindowAsync(region, RiskType.Flood, "tenant-a");
            window!.AddSignal(SignalFrom(source));
            await repository.UpdateAsync(window);
        }

        await using (var context = _fixture.CreateContext())
        {
            var window = await Repository(context).GetOpenWindowAsync(region, RiskType.Flood, "tenant-a");
            Assert.Equal(2, window!.Signals.Count);
            Assert.Equal("Sensor", window.Signals.Single(s => s.Source == SignalSource.Sensor).Metadata["source"]);
        }
    }

    [Fact]
    public async Task ConcurrentUpdatesOfTheSameWindow_SecondWriterGetsConcurrencyConflict()
    {
        var region = $"region-{Guid.NewGuid():N}";
        await using (var context = _fixture.CreateContext())
            await Repository(context).AddAsync(
                AlertWindow.Open(region, RiskType.Flood, "tenant-a", 0.5, TimeSpan.FromHours(1)));

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var firstWindow = await Repository(first).GetOpenWindowAsync(region, RiskType.Flood, "tenant-a");
        var secondWindow = await Repository(second).GetOpenWindowAsync(region, RiskType.Flood, "tenant-a");

        firstWindow!.AddSignal(SignalFrom(SignalSource.Sensor));
        await Repository(first).UpdateAsync(firstWindow);

        secondWindow!.Close("operator", "duplicate");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Repository(second).UpdateAsync(secondWindow));
    }

    [Fact]
    public async Task SearchAsync_FiltersByTenantInTheDatabaseAndPagesNewestFirst()
    {
        var region = $"region-{Guid.NewGuid():N}";
        var created = new List<AlertWindow>();
        await using (var context = _fixture.CreateContext())
        {
            var repository = Repository(context);
            foreach (var tenant in new[] { "tenant-a", "TENANT-A", "tenant-b", "tenant-a" })
            {
                var window = AlertWindow.Open(region, RiskType.Flood, tenant, 0.5, TimeSpan.FromHours(1));
                await repository.AddAsync(window);
                created.Add(window);
                await Task.Delay(5);
            }
        }

        await using var readContext = _fixture.CreateContext();
        var firstPage = await Repository(readContext).SearchAsync(region, AlertStatus.Open, ["tenant-a"], 1, 2);
        var secondPage = await Repository(readContext).SearchAsync(region, null, ["Tenant-A"], 2, 2);
        var platform = await Repository(readContext).SearchAsync(region, null, null, 1, 10);

        Assert.Equal(3, firstPage.TotalItems);
        Assert.Equal([created[3].Id, created[1].Id], firstPage.Items.Select(w => w.Id));
        Assert.Equal(created[0].Id, Assert.Single(secondPage.Items).Id);
        Assert.Equal(4, platform.TotalItems);
    }
}

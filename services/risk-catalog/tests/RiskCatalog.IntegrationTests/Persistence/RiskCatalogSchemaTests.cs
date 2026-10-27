using Microsoft.EntityFrameworkCore;
using Npgsql;
using RiskCatalog.Domain.EventTypes;
using RiskCatalog.Domain.RiskModels;
using RiskCatalog.Infrastructure.Persistence.DatabaseContext;
using RiskCatalog.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace RiskCatalog.IntegrationTests.Persistence;

/// <summary>Postgres 15.5 with the risk_catalog schema from platform/init-db/postgres/2-schema.sql.</summary>
public sealed class RiskCatalogPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:15.5").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(ScriptSection("risk_catalog"), connection);
        await command.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public RiskCatalogDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<RiskCatalogDbContext>().UseNpgsql(ConnectionString).Options);

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

public class RiskCatalogSchemaTests : IClassFixture<RiskCatalogPostgresFixture>
{
    private readonly RiskCatalogPostgresFixture _fixture;

    public RiskCatalogSchemaTests(RiskCatalogPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Schema_MatchesTheModelWithNoPendingChanges()
    {
        await using var context = _fixture.CreateContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task EventTypes_RoundTripThroughTheRepository()
    {
        var code = $"EVT{Guid.NewGuid():N}"[..20];
        await using (var context = _fixture.CreateContext())
            Assert.True(await new EventTypeRepository(context).CreateEventType(
                new EventType(Guid.NewGuid(), code, "Flood", "River flood", true)));

        await using (var context = _fixture.CreateContext())
        {
            var stored = await new EventTypeRepository(context).GetEventTypeByCode(code);
            Assert.Equal("Flood", stored!.Name);
        }
    }

    [Fact]
    public async Task RegionalParameters_PersistTheGeographicColumns()
    {
        var id = Guid.NewGuid();
        await using (var context = _fixture.CreateContext())
            Assert.True(await new RegionalParameterRepository(context).AddRegionalParameterAsync(
                new RegionalParameter(id, 1.2, "Coast", -23.5, -46.6, null, 15)));

        await using (var context = _fixture.CreateContext())
        {
            var stored = await new RegionalParameterRepository(context).GetRegionByIdAsync(id);
            Assert.Equal(15, stored!.CoverageRadiusKm);
            Assert.Equal(-23.5, stored.CenterLatitude);
        }
    }
}

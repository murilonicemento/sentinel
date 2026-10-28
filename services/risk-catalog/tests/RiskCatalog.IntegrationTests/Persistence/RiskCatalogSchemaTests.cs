using Microsoft.EntityFrameworkCore;
using Npgsql;
using RiskCatalog.Application.Services;
using RiskCatalog.Domain.Enums;
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

    [Fact]
    public async Task Listings_ReturnTheCatalogVersionInADeterministicOrder()
    {
        var code = $"LST{Guid.NewGuid():N}"[..20];
        var eventTypeId = Guid.NewGuid();
        var severityId = Guid.NewGuid();
        await using (var context = _fixture.CreateContext())
        {
            await new EventTypeRepository(context).CreateEventType(new EventType(eventTypeId, code, "Flood", "d", true));
            await new SeverityRepository(context).CreateSeverity(
                new Severity(severityId, SeverityLevelEnum.High, $"high-{code}"));
            var events = new EventTypeRepository(context);
            await events.AddSeverityCriterionToEventType(
                new SeverityCriterion(Guid.NewGuid(), eventTypeId, severityId, 50, 80, "mm", 9));
            await events.AddSeverityCriterionToEventType(
                new SeverityCriterion(Guid.NewGuid(), eventTypeId, severityId, 10, 50, "mm", 9));
            var matrices = new RiskMatrixRepository(context);
            await matrices.AddRiskMatrixAsync(new RiskMatrix(Guid.NewGuid(), eventTypeId, SeverityLevelEnum.High,
                RiskLevelEnum.Critical, 9));
            await matrices.AddRiskMatrixAsync(new RiskMatrix(Guid.NewGuid(), eventTypeId, SeverityLevelEnum.Low,
                RiskLevelEnum.Low, 8));
        }

        await using var read = _fixture.CreateContext();
        var listing = new CatalogListingService(new RegionalParameterRepository(read), new RiskMatrixRepository(read),
            new EventTypeRepository(read));
        var criteria = await listing.GetSeverityCriteriaAsync(9, 1, 50);
        var matrix = await listing.GetRiskMatrixEntriesAsync(9, 1, 50);

        Assert.Equal([10d, 50d], criteria.Items.Where(c => c.EventTypeCode == code).Select(c => c.MinValue));
        Assert.All(criteria.Items, c => Assert.Equal(9, c.Version));
        var entry = Assert.Single(matrix.Items, m => m.EventTypeCode == code);
        Assert.Equal(("High", "Critical"), (entry.SeverityLevel, entry.RiskLevel));
    }

    [Fact]
    public async Task RegionalListing_IsPagedWithTheBoundsParsed()
    {
        await using (var context = _fixture.CreateContext())
            await new RegionalParameterRepository(context).AddRegionalParameterAsync(new RegionalParameter(
                Guid.NewGuid(), 1.5, "Polygon", null, null,
                "{\"Coordinates\":[{\"Latitude\":-23,\"Longitude\":-47},{\"Latitude\":-23,\"Longitude\":-46},{\"Latitude\":-24,\"Longitude\":-46}]}",
                null));

        await using var read = _fixture.CreateContext();
        var page = await new CatalogListingService(new RegionalParameterRepository(read),
            new RiskMatrixRepository(read), new EventTypeRepository(read)).GetRegionalParametersAsync(1, 50);

        var polygon = Assert.Single(page.Items, r => r.Description == "Polygon");
        Assert.Equal(3, polygon.RegionBounds!.Coordinates.Count);
        Assert.Equal(page.Items.OrderBy(r => r.RegionId).Select(r => r.RegionId), page.Items.Select(r => r.RegionId));
    }
}

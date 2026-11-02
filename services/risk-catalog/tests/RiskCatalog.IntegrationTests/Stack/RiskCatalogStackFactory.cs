using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using RiskCatalog.Api;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Interfaces;
using RiskCatalog.IntegrationTests.Support;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace RiskCatalog.IntegrationTests.Stack;

/// <summary>
/// The API on real stores: Postgres 15.5 with the risk_catalog section of
/// <c>platform/init-db/postgres/2-schema.sql</c>, Redis and Kafka. Only the geospatial service is replaced.
/// </summary>
public sealed class RiskCatalogStackFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly Guid PublisherTenant = Guid.Parse("4b1e6a1c-8a7d-4f43-9e55-0c3f2a9d7b10");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:15.5")
        .WithDatabase("risk_catalog").Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();
    private readonly KafkaContainer _kafka = new KafkaBuilder("confluentinc/cp-kafka:7.6.0").Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();
    public string KafkaBootstrapServers => _kafka.GetBootstrapAddress();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _kafka.StartAsync());
        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();
        await using var schema = new NpgsqlCommand(SchemaSection(), connection);
        await schema.ExecuteNonQueryAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(),
            _kafka.DisposeAsync().AsTask());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:RiskCatalogDatabase", PostgresConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Kafka", KafkaBootstrapServers);
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("ServiceToken:SigningKey", "integration-tests-risk-catalog-service-token-key-0123456789");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGeospatialClient>();
            services.AddSingleton<IGeospatialClient, AcceptingGeospatialClient>();
        });
    }

    public HttpClient ClientFor(params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.User(roles, [PublisherTenant]));
        return client;
    }

    public async Task<long> CountAsync(string sql, object parameter)
    {
        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("p", parameter);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static string SchemaSection()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sentinel.sln")))
            directory = directory.Parent;
        var lines = File.ReadAllLines(Path.Combine(directory!.FullName, "platform", "init-db", "postgres",
            "2-schema.sql"));
        return string.Join('\n', lines
            .SkipWhile(line => !line.StartsWith(@"\c risk_catalog"))
            .Skip(1)
            .TakeWhile(line => !line.StartsWith(@"\c ")));
    }

    /// <summary>Every coordinate and region is valid; geospatial behavior is covered by its own service.</summary>
    private sealed class AcceptingGeospatialClient : IGeospatialClient
    {
        public Task<bool> ContainsPointAsync(PointDTO point, PolygonDTO area,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<(bool WithinRadius, double Distance)> WithinRadiusAsync(PointDTO center, PointDTO point,
            RadiusDTO radius, CancellationToken cancellationToken = default) => Task.FromResult((true, 0d));

        public Task<bool> IntersectsAsync(PolygonDTO polygonA, PolygonDTO polygonB,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<double> DistanceAsync(PointDTO from, PointDTO to, CancellationToken cancellationToken = default) =>
            Task.FromResult(0d);

        public Task<BatchResponseDTO> EvaluateBatchAsync(BatchRequestDTO request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BatchResponseDTO
            {
                Results = request.Evaluations
                .Select(e => new BatchResultDTO { Type = e.Type, Contains = true, WithinRadius = true, Distance = 0 })
                .ToList()
            });
    }
}

[CollectionDefinition(Name)]
public sealed class RiskCatalogStackCollection : ICollectionFixture<RiskCatalogStackFactory>
{
    public const string Name = "risk-catalog-stack";
}

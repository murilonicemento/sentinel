using System.Net.Http.Headers;
using Ingestion.Api;
using Ingestion.Application.Interfaces.Services;
using Ingestion.IntegrationTests.V1;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using Npgsql;
using Testcontainers.Kafka;
using Testcontainers.Minio;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Ingestion.IntegrationTests.Stack;

/// <summary>
/// The whole ingestion write path on real stores: Postgres with the ingestion section of
/// <c>platform/init-db/postgres/2-schema.sql</c>, MongoDB with the unique <c>EventId</c> indexes of
/// <c>2-schema.js</c>, Redis, MinIO and Kafka. Only the calls to other services (tenants-billing, geospatial) are
/// replaced, because they are outside this service.
/// </summary>
public sealed class IngestionStackFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly Guid TenantA = Guid.Parse("6d3f1c8e-1a52-4b0e-9a59-2f0d6f1b9a01");
    public static readonly Guid TenantB = Guid.Parse("6d3f1c8e-1a52-4b0e-9a59-2f0d6f1b9a02");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:15.5")
        .WithDatabase("ingestion").Build();

    // No credentials: the mongo image then skips the temporary init server, whose port was sometimes still bound
    // when the real mongod started under the load of the whole solution run (exit 48, "Address already in use").
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7").WithUsername(string.Empty).WithPassword(string.Empty)
        .Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();
    private readonly KafkaContainer _kafka = new KafkaBuilder("confluentinc/cp-kafka:7.6.0").Build();

    // Same image as platform/docker-compose.yml (the public MinIO images are no longer published).
    private readonly MinioContainer _minio = new MinioBuilder(
        "cgr.dev/chainguard/minio@sha256:e70b2b1e74d79457f808710483bd726ff364529ca30736eaa15a6ab547d3a5a7").Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();
    public string KafkaBootstrapServers => _kafka.GetBootstrapAddress();
    public IMongoDatabase ReadDatabase => new MongoClient(_mongo.GetConnectionString()).GetDatabase("IngestionReadDatabase");

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _mongo.StartAsync(), _redis.StartAsync(), _kafka.StartAsync(),
            _minio.StartAsync());
        await ApplyIngestionSchemaAsync();
        await CreateReadModelIndexesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _mongo.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(), _kafka.DisposeAsync().AsTask(), _minio.DisposeAsync().AsTask());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:IngestionWriteDatabase", PostgresConnectionString);
        builder.UseSetting("ConnectionStrings:IngestionReadDatabase", _mongo.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Kafka", KafkaBootstrapServers);
        builder.UseSetting("MinIO:Host", _minio.Hostname);
        builder.UseSetting("MinIO:Port", _minio.GetMappedPublicPort(9000).ToString());
        builder.UseSetting("MinIO:AccessKey", _minio.GetAccessKey());
        builder.UseSetting("MinIO:SecretKey", _minio.GetSecretKey());
        builder.UseSetting("MinIO:BucketName", "ingestion-tests");
        builder.UseSetting("Outbox:IntervalSeconds", "1");
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("ServiceToken:SigningKey", "integration-tests-ingestion-service-token-key-0123456789");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITenantBillingGateway>();
            services.AddSingleton<ITenantBillingGateway, ActiveTenants>();
            services.RemoveAll<IGeospatialValidationService>();
            services.AddSingleton<IGeospatialValidationService, ValidCoordinates>();
        });
    }

    public HttpClient ClientFor(string role, params Guid[] tenants)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.User([role], tenants));
        return client;
    }

    public async Task<T?> ScalarAsync<T>(string sql, object? parameter = null)
    {
        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        if (parameter is not null) command.Parameters.AddWithValue("p", parameter);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    /// <summary>Runs the ingestion section of the real bootstrap script, so a broken script fails here first.</summary>
    private async Task ApplyIngestionSchemaAsync()
    {
        var script = await File.ReadAllLinesAsync(Path.Combine(RepositoryRoot(), "platform", "init-db", "postgres",
            "2-schema.sql"));
        var section = script
            .SkipWhile(line => !line.StartsWith(@"\c ingestion"))
            .Skip(1)
            .TakeWhile(line => !line.StartsWith(@"\c "));

        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();
        await using (var schema = new NpgsqlCommand(string.Join('\n', section), connection))
            await schema.ExecuteNonQueryAsync();
        await using var tenants = new NpgsqlCommand(
            "INSERT INTO tenant (id, name) VALUES (@a, 'Tenant A'), (@b, 'Tenant B')", connection);
        tenants.Parameters.AddWithValue("a", TenantA);
        tenants.Parameters.AddWithValue("b", TenantB);
        await tenants.ExecuteNonQueryAsync();
    }

    private async Task CreateReadModelIndexesAsync()
    {
        foreach (var name in new[] { "climaticEvents", "disasterEvents" })
            await ReadDatabase.GetCollection<BsonDocument>(name).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("EventId"),
                    new CreateIndexOptions { Unique = true }));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sentinel.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Sentinel.sln not found.");
    }

    private sealed class ActiveTenants : ITenantBillingGateway
    {
        public Task<bool> ValidateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class ValidCoordinates : IGeospatialValidationService
    {
        public Task<bool> ValidateCoordinatesAsync(double latitude, double longitude,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}

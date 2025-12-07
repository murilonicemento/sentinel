using Ingestion.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Testcontainers.Minio;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Ingestion.IntegrationTests;

public class IngestionWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder()
        .WithImage("postgres")
        .WithDatabase("ingestion")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly MongoDbContainer _mongoDbContainer = new MongoDbBuilder()
        .WithImage("mongo")
        .WithUsername("mongodb")
        .WithPassword("mongodb")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis")
        .Build();

    private readonly MinioContainer _minioContainer = new MinioBuilder()
        .WithImage("minio/minio")
        .WithUsername("minioadmin")
        .WithPassword("minioadmin")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services => { });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:IngestionWriteDatabase", _postgreSqlContainer.GetConnectionString() },
                { "ConnectionStrings:IngestionReadDatabase", _mongoDbContainer.GetConnectionString() },
                {
                    "ConnectionStrings:Redis", $"{_redisContainer.Hostname}:{_redisContainer.GetMappedPublicPort(6379)}"
                },
                { "MinIO:Host", _minioContainer.Hostname },
                { "MinIO:Port", _minioContainer.GetMappedPublicPort(9000).ToString() },
                { "MinIO:AccessKey", "minioadmin" },
                { "MinIO:SecretKey", "minioadmin" },
                { "MinIO:BucketName", "ingestion-test" }
            });
        });

        builder.ConfigureServices(services => { });
    }

    public async Task InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync();
        await _mongoDbContainer.StartAsync();
        await _redisContainer.StartAsync();
        await _minioContainer.StartAsync();
    }

    public async new Task DisposeAsync()
    {
        await _postgreSqlContainer.StopAsync();
        await _mongoDbContainer.StopAsync();
        await _redisContainer.StopAsync();
        await _minioContainer.StopAsync();
    }
}
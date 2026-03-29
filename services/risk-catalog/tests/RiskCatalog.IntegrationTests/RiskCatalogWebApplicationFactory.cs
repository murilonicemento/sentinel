using Confluent.Kafka;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using RiskCatalog.Infrastructure.Persistence.DatabaseContext;
using StackExchange.Redis;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace RiskCatalog.IntegrationTests;

public class RiskCatalogWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder()
        .WithImage("postgres:15.5")
        .WithCleanUp(true)
        .WithDatabase("risk_catalog")
        .WithUsername("postgres")
        .WithPassword("postgrespw")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7")
        .WithCleanUp(true)
        .Build();

    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.6.0")
        .WithCleanUp(true)
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var testData = new Dictionary<string, string>
            {
                {"Jwt:Issuer", "TestIssuer"},
                {"Jwt:Audience", "TestAudience"},
                {"Jwt:Key", "ThisIsASecretKeyForJWTTokenGeneration123456789"},
                {"ElasticSearch:URI", "http://localhost:9200"}
            };
            
            config.AddInMemoryCollection(testData);
        });

        builder.ConfigureTestServices(services =>
        {
            var descriptor =
                services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RiskCatalogDbContext>));

            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<RiskCatalogDbContext>(options =>
            {
                options.UseNpgsql(_postgreSqlContainer.GetConnectionString());
            });
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configurationOptions = ConfigurationOptions.Parse(_redisContainer.GetConnectionString());

                configurationOptions.AbortOnConnectFail = false;
                configurationOptions.ConnectRetry = 3;
                configurationOptions.ConnectTimeout = 15000;
                configurationOptions.SyncTimeout = 15000;
                configurationOptions.AsyncTimeout = 15000;

                return ConnectionMultiplexer.Connect(configurationOptions);
            });
            services.AddSingleton<IProducer<Null, string>>(sp =>
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = _kafkaContainer.GetBootstrapAddress(),
                    Acks = Acks.All,
                    EnableIdempotence = true,
                    MessageTimeoutMs = 5000
                };

                return new ProducerBuilder<Null, string>(config).Build();
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync();
        await _redisContainer.StartAsync();
        await _kafkaContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgreSqlContainer.StopAsync();
        await _redisContainer.StopAsync();
        await _kafkaContainer.StopAsync();
    }
}
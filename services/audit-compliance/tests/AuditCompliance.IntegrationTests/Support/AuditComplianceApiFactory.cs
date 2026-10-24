using AuditCompliance.Application.Interfaces;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MongoDb;

namespace AuditCompliance.IntegrationTests.Support;

/// <summary>
/// API com MongoDB real (Testcontainers). Consumidor Kafka e worker de exportação ficam desabilitados;
/// os testes acionam o processamento de exportações explicitamente para manter o fluxo determinístico.
/// </summary>
public class AuditComplianceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7").Build();

    public string DatabaseName { get; } = $"audit_tests_{Guid.NewGuid():N}";

    async Task IAsyncLifetime.InitializeAsync()
    {
        await StartContainersAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await StopContainersAsync();
    }

    protected virtual Task StartContainersAsync()
    {
        return _mongo.StartAsync();
    }

    protected virtual async Task StopContainersAsync()
    {
        await _mongo.DisposeAsync();
    }

    public Task PauseStorageAsync()
    {
        return _mongo.PauseAsync();
    }

    public Task ResumeStorageAsync()
    {
        return _mongo.UnpauseAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting garante que os valores estejam disponíveis durante o registro dos serviços.
        foreach (var (key, value) in Settings()) builder.UseSetting(key, value);
    }

    protected virtual IEnumerable<KeyValuePair<string, string>> Settings()
    {
        return new Dictionary<string, string>
        {
            ["MongoDb:ConnectionString"] = _mongo.GetConnectionString(),
            ["MongoDb:DatabaseName"] = DatabaseName,
            ["MongoDb:EnsureIndexesOnStartup"] = "true",
            ["Kafka:ConsumerEnabled"] = "false",
            ["Kafka:BootstrapServers"] = "localhost:1",
            ["Exports:WorkerEnabled"] = "false",
            ["Exports:MaxRecords"] = "50",
            ["Jwt:Issuer"] = TestTokens.Issuer,
            ["Jwt:Audience"] = TestTokens.Audience,
            ["Jwt:SigningKey"] = TestTokens.SigningKey,
            ["RateLimiting:QueriesPerMinute"] = "10000",
            ["RateLimiting:ExportsPerMinute"] = "10000"
        };
    }

    public async Task<AuditRecord> SeedAsync(AuditRecord record)
    {
        using var scope = Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IIntegrityHasher>();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditRecordRepository>();

        record.SealIntegrity(hasher.Compute(record));
        Assert.Equal(AppendResultEnum.Appended, await repository.AppendAsync(record, CancellationToken.None));
        return record;
    }
}

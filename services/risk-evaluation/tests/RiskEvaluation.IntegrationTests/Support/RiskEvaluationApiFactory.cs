using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using RiskEvaluation.Application.Interfaces.Messaging;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.IntegrationTests.Support;

public sealed class RiskEvaluationApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("ServiceToken:Issuer", "sentinel-service:risk-evaluation");
        builder.UseSetting("ServiceToken:Audience", TestTokens.Audience);
        builder.UseSetting("ServiceToken:SigningKey", "integration-tests-risk-evaluation-service-key-0123456789");
        builder.UseSetting("ServiceToken:Roles:0", "RiskCatalog.Read");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

            // Mongo, Redis and Kafka are replaced so the HTTP contract can run in memory.
            services.RemoveAll<IRiskEvaluationRepository>();
            services.AddSingleton<IRiskEvaluationRepository>(Evaluations);
            services.RemoveAll<IRiskFactorRepository>();
            services.AddSingleton(Mock.Of<IRiskFactorRepository>(r =>
                r.GetAllAsync(It.IsAny<CancellationToken>()) == Task.FromResult<IReadOnlyList<RiskFactor>>(new List<RiskFactor>())));
            services.RemoveAll<IRiskModelRepository>();
            services.AddSingleton(Mock.Of<IRiskModelRepository>());
            services.RemoveAll<ICatalogSnapshotRepository>();
            services.AddSingleton(Mock.Of<ICatalogSnapshotRepository>());
            services.RemoveAll<IRecentScoresCache>();
            services.AddSingleton(Mock.Of<IRecentScoresCache>());
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton(Mock.Of<IEventPublisher>());
        });
    }

    public InMemoryRiskEvaluationRepository Evaluations { get; } = new();

    public HttpClient CreateClientWithToken(string? token)
    {
        var client = CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class InMemoryRiskEvaluationRepository : IRiskEvaluationRepository
{
    private readonly List<RiskEvaluationEntity> _items = [];
    private readonly Lock _lock = new();

    public Task AddAsync(RiskEvaluationEntity evaluation)
    {
        lock (_lock) _items.Add(evaluation);
        return Task.CompletedTask;
    }

    public Task<RiskEvaluationEntity?> GetByIdAsync(Guid id)
    {
        lock (_lock) return Task.FromResult(_items.FirstOrDefault(e => e.Id == id));
    }

    public Task<List<RiskEvaluationEntity>> GetByLocationAsync(int latitude, int longitude)
    {
        lock (_lock) return Task.FromResult(_items.Where(e => e.Latitude == latitude && e.Longitude == longitude).ToList());
    }

    public Task UpdateAsync(RiskEvaluationEntity evaluation) => Task.CompletedTask;
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Elastic.Clients.Elasticsearch;
using Reporting.Domain.Interfaces;
using Reporting.Infrastructure.HostedServices;
using Reporting.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Reporting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton(CreateElasticClient);
        services.AddSingleton(CreateRedisDatabase);
        services.AddSingleton<IReportingRepository, ElasticsearchReportingRepository>();
        services.AddSingleton<IReportingAuditRepository, ReportingAuditRepository>();
        services.AddHostedService<ReportingKafkaConsumerHostedService>();
        return services;
    }

    private static ElasticsearchClient CreateElasticClient(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var uri = configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
        var settings = new ElasticsearchClientSettings(new Uri(uri))
            .DefaultIndex(configuration["Elasticsearch:IndexName"] ?? "reporting-events");

        return new ElasticsearchClient(settings);
    }

    private static IDatabase CreateRedisDatabase(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var redisConfig = configuration["Redis:Configuration"];
        if (string.IsNullOrWhiteSpace(redisConfig))
        {
            throw new InvalidOperationException("Redis configuration is required for reporting infrastructure.");
        }

        var options = ConfigurationOptions.Parse(redisConfig);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.KeepAlive = 180;

        var multiplexer = ConnectionMultiplexer.Connect(options);
        return multiplexer.GetDatabase();
    }
}

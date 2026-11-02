using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Elastic.Clients.Elasticsearch;
using Reporting.Application.Interfaces;
using Reporting.Domain.Interfaces;
using Reporting.Infrastructure.HostedServices;
using Reporting.Infrastructure.Messaging;
using Reporting.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Reporting.Infrastructure;

public static class DependencyInjection
{
    private static readonly string[] RequiredSettings =
        ["Elasticsearch:Uri", "Redis:Configuration", "Kafka:BootstrapServers"];

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        foreach (var key in RequiredSettings)
            if (string.IsNullOrWhiteSpace(configuration[key]))
                throw new InvalidOperationException($"Configuration '{key}' is required.");

        services.AddSingleton(CreateElasticClient);
        services.AddSingleton(CreateRedisDatabase);
        services.AddSingleton<IReportingRepository, ElasticsearchReportingRepository>();
        services.AddSingleton<IReportingAuditRepository, ReportingAuditRepository>();
        services.AddSingleton<IReportingReplayPublisher, KafkaReportingReplayPublisher>();
        services.AddHostedService<ReportingKafkaConsumerHostedService>();
        services.AddHostedService<ReportingDeadLetterConsumerHostedService>();
        return services;
    }

    private static ElasticsearchClient CreateElasticClient(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var uri = configuration["Elasticsearch:Uri"]!;
        var settings = new ElasticsearchClientSettings(new Uri(uri))
            .DefaultIndex(configuration["Elasticsearch:IndexName"] ?? "reporting-events");

        return new ElasticsearchClient(settings);
    }

    private static IDatabase CreateRedisDatabase(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var options = ConfigurationOptions.Parse(configuration["Redis:Configuration"]!);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.KeepAlive = 180;

        var multiplexer = ConnectionMultiplexer.Connect(options);
        return multiplexer.GetDatabase();
    }
}

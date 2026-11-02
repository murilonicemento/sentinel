using AuditCompliance.Application.Interfaces;
using AuditCompliance.Application.Policies;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.Infrastructure.HostedServices;
using AuditCompliance.Infrastructure.Integrity;
using AuditCompliance.Infrastructure.Messaging;
using AuditCompliance.Infrastructure.Observability;
using AuditCompliance.Infrastructure.Options;
using AuditCompliance.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AuditCompliance.Infrastructure;

public static class ServiceCollectionExtensions
{
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddAuditComplianceInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AuditQueryOptions>()
            .Bind(configuration.GetSection(AuditQueryOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.MaxQueryRangeDays <= o.MaxExportRangeDays,
                "AuditQuery:MaxQueryRangeDays must not exceed AuditQuery:MaxExportRangeDays.")
            .ValidateOnStart();

        services.AddOptions<ExportOptions>()
            .Bind(configuration.GetSection(ExportOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AuditQueryOptions>>().Value;
            return new AuditQueryPolicy
            {
                MaxQueryRangeDays = options.MaxQueryRangeDays,
                MaxExportRangeDays = options.MaxExportRangeDays
            };
        });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ExportOptions>>().Value;
            return new AuditExportPolicy
            {
                MaxRecords = options.MaxRecords,
                FileExpiration = TimeSpan.FromHours(options.FileExpirationHours),
                ProcessingLease = TimeSpan.FromMinutes(options.ProcessingLeaseMinutes),
                MaxProcessingAttempts = options.MaxProcessingAttempts
            };
        });

        // Persistence
        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
            settings.ApplicationName = "audit-compliance";
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(options.ServerSelectionTimeoutSeconds);
            settings.ConnectTimeout = TimeSpan.FromSeconds(options.ServerSelectionTimeoutSeconds);
            settings.SocketTimeout = TimeSpan.FromSeconds(options.SocketTimeoutSeconds);
            return new MongoClient(settings);
        });

        services.AddSingleton(sp => new AuditComplianceMongoContext(
            sp.GetRequiredService<IMongoClient>(),
            sp.GetRequiredService<IOptions<MongoDbOptions>>().Value.DatabaseName));

        services.AddSingleton<MongoAuditRecordRepository>();
        services.AddSingleton<IAuditRecordRepository>(sp => sp.GetRequiredService<MongoAuditRecordRepository>());
        services.AddSingleton<IAuditRecordQueryRepository>(sp => sp.GetRequiredService<MongoAuditRecordRepository>());
        services.AddSingleton<IAuditExportRepository, MongoAuditExportRepository>();
        services.AddSingleton<IExportFileStorage, GridFsExportFileStorage>();

        if (configuration.GetValue<bool>($"{MongoDbOptions.SectionName}:{nameof(MongoDbOptions.EnsureIndexesOnStartup)}"))
            services.AddHostedService<MongoIndexInitializer>();

        // Integrity and observability
        services.AddSingleton<IIntegrityHasher, Sha256IntegrityHasher>();
        services.AddSingleton<IAuditMetrics, AuditMetrics>();

        // Messaging
        services.AddSingleton<KafkaDeadLetterPublisher>();

        if (configuration.GetValue($"{KafkaOptions.SectionName}:{nameof(KafkaOptions.ConsumerEnabled)}", true))
            services.AddHostedService<AuditEventConsumerHostedService>();

        // Exports
        if (configuration.GetValue($"{ExportOptions.SectionName}:{nameof(ExportOptions.WorkerEnabled)}", true))
            services.AddHostedService<ExportProcessingWorker>();

        services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>("mongodb", HealthStatus.Unhealthy, [ReadinessTag]);

        return services;
    }
}

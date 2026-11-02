using AuditCompliance.Application.Exports;
using AuditCompliance.Application.Exports.Formatters;
using AuditCompliance.Application.Ingestion;
using AuditCompliance.Application.Records;
using Microsoft.Extensions.DependencyInjection;

namespace AuditCompliance.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuditComplianceApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IAuditExportFormatter, CsvAuditExportFormatter>();
        services.AddSingleton<IAuditExportFormatter, JsonAuditExportFormatter>();

        services.AddScoped<IngestAuditEventHandler>();
        services.AddScoped<SearchAuditRecordsHandler>();
        services.AddScoped<GetAuditRecordByIdHandler>();
        services.AddScoped<SelfAuditRecorder>();
        services.AddScoped<RequestAuditExportHandler>();
        services.AddScoped<GetAuditExportHandler>();
        services.AddScoped<DownloadAuditExportHandler>();
        services.AddScoped<ProcessAuditExportsHandler>();

        return services;
    }
}

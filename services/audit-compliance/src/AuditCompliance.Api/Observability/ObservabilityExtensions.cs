using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;

namespace AuditCompliance.Api.Observability;

/// <summary>
/// Platform observability (ADR-0004): Serilog to console and Loki, OpenTelemetry traces to Tempo over OTLP, metrics
/// for Prometheus, and liveness/readiness health checks. Each service keeps its own copy (no shared library yet).
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>Health checks with this tag decide readiness; liveness never checks dependencies.</summary>
    public const string ReadinessTag = "ready";

    /// <summary>Activity source of the Kafka producers and consumers.</summary>
    public const string MessagingActivitySource = "Sentinel.Messaging";

    /// <summary>
    /// Configuration: <c>Loki:Url</c> (logs) and <c>OpenTelemetry:OtlpEndpoint</c> (traces); either may be absent, in
    /// which case that signal stays local. Log levels come from the <c>Serilog</c> section.
    /// </summary>
    public static WebApplicationBuilder AddSentinelObservability(this WebApplicationBuilder builder, string serviceName,
        string[]? activitySources = null, string[]? meters = null)
    {
        builder.Host.UseSerilog((context, configuration) =>
        {
            configuration.ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.With<TraceContextEnricher>()
                .Enrich.WithProperty("service", serviceName)
                .Enrich.WithProperty("environment", context.HostingEnvironment.EnvironmentName)
                .WriteTo.Console();

            var lokiUrl = context.Configuration["Loki:Url"];
            if (!string.IsNullOrWhiteSpace(lokiUrl))
                configuration.WriteTo.GrafanaLoki(lokiUrl,
                    labels: [new LokiLabel { Key = "service", Value = serviceName }],
                    propertiesAsLabels: ["level"]);
        });

        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !IsOperationalPath(context.Request.Path))
                    .AddHttpClientInstrumentation()
                    .AddSource(MessagingActivitySource)
                    .AddSource(activitySources ?? []);
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
            })
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(meters ?? [])
                .AddPrometheusExporter());

        builder.Services.AddHealthChecks();
        return builder;
    }

    /// <summary>Request logs, quiet for health checks and metric scrapes.</summary>
    public static WebApplication UseSentinelRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
            exception is not null || context.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : IsOperationalPath(context.Request.Path)
                    ? LogEventLevel.Verbose
                    : LogEventLevel.Information);
        return app;
    }

    /// <summary>
    /// <c>/health/live</c> (process up, no dependency checked), <c>/health/ready</c> (checks tagged
    /// <see cref="ReadinessTag"/>), <c>/health</c> (every check, kept for existing probes) and <c>/metrics</c>.
    /// Responses carry only the status, never dependency details. <c>/metrics</c> is for Prometheus inside the
    /// platform network and is not routed by the api-gateway.
    /// </summary>
    public static WebApplication MapSentinelOperationalEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready",
            new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadinessTag) }).AllowAnonymous();
        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapPrometheusScrapingEndpoint().AllowAnonymous();
        return app;
    }

    private static bool IsOperationalPath(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/metrics");

    /// <summary>Adds TraceId/SpanId so a log line in Loki links to its trace in Tempo.</summary>
    private sealed class TraceContextEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (Activity.Current is not { } activity) return;
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToHexString()));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToHexString()));
        }
    }
}

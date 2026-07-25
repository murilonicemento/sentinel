using OpenTelemetry.Metrics;
using Reporting.Application;
using Reporting.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console();
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices();

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter("ReportingService"))
    .WithTracing(tracing => tracing.AddSource("ReportingService"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

namespace Reporting.Api
{
    public partial class Program
    {
    }
}

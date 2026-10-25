using OpenTelemetry.Metrics;
using Reporting.Api.Security;
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
builder.Services.AddReportingAuthentication(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices();

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("ReportingService")
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing.AddSource("ReportingService"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapPrometheusScrapingEndpoint().AllowAnonymous();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

namespace Reporting.Api
{
    public partial class Program
    {
    }
}

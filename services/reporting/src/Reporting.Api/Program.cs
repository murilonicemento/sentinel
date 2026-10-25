using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Metrics;
using Reporting.Api.Contracts;
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
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    var defaultFactory = options.InvalidModelStateResponseFactory;
    options.InvalidModelStateResponseFactory = context =>
        ApiProblems.IsVersionedApi(context.HttpContext) ? ModelValidationProblems.For(context) : defaultFactory(context);
});
builder.Services.AddOpenApi(options => options.MarkDeprecatedOperations());
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var status = context.ProblemDetails.Status ?? StatusCodes.Status500InternalServerError;
        ApiProblems.Enrich(context.ProblemDetails, context.HttpContext, ApiProblems.DefaultCodeFor(status));
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
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

app.UseExceptionHandler();
app.UseWhen(ApiProblems.IsVersionedApi, versioned => versioned.UseStatusCodePages());
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

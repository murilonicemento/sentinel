using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Contracts;
using Reporting.Api.Observability;
using Reporting.Api.Security;
using Reporting.Application;
using Reporting.Infrastructure;
using Reporting.Infrastructure.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddSentinelObservability("reporting", activitySources: ["ReportingService"], meters: ["ReportingService"]);

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
builder.Services.AddHealthChecks().AddReportingReadinessChecks(ObservabilityExtensions.ReadinessTag);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

app.UseSentinelRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options => options.WithTitle("Sentinel - Reporting API")).AllowAnonymous();
}

app.UseExceptionHandler();
app.UseWhen(ApiProblems.IsVersionedApi, versioned => versioned.UseStatusCodePages());
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapSentinelOperationalEndpoints();

app.Run();

namespace Reporting.Api
{
    public partial class Program
    {
    }
}

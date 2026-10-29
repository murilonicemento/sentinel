using Geospatial.Api.Contracts;
using Geospatial.Api.Middlewares;
using Geospatial.Api.Options;
using Geospatial.Api.Observability;
using Geospatial.Api.Security;
using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Application.UseCases;
using Geospatial.Infrastructure;
using Geospatial.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.AddSentinelObservability("geospatial");
builder.Services.AddHealthChecks().AddGeospatialHealthChecks();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddGeospatialAuthentication(builder.Configuration);

builder.Services
    .AddOpenApi(options => options.MarkDeprecatedOperations())
    .AddInfrastructureWriteServiceCollection(builder.Configuration);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var status = context.ProblemDetails.Status ?? StatusCodes.Status500InternalServerError;
        ApiProblems.Enrich(context.ProblemDetails, context.HttpContext, ApiProblems.DefaultCodeFor(status));
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    var defaultFactory = options.InvalidModelStateResponseFactory;
    options.InvalidModelStateResponseFactory = context =>
        ApiProblems.IsVersionedApi(context.HttpContext) ? ModelValidationProblems.For(context) : defaultFactory(context);
});

builder.Services.AddGeospatialOptions(builder.Configuration);

builder.Services
    .AddScoped<IBatchEvaluateUseCase, BatchEvaluateUseCase>()
    .AddScoped<IWithinRadiusUseCase, WithinRadiusUseCase>()
    .AddScoped<IIntersectsUseCase, IntersectsUseCase>()
    .AddScoped<IDistanceUseCase, DistanceUseCase>()
    .AddScoped<IContainsPointUseCase, ContainsPointUseCase>()
    .AddScoped<ISearchGeospatialEventsUseCase, SearchGeospatialEventsUseCase>();

var app = builder.Build();

app.UseSentinelRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Sentinel - Geospatial API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    }).AllowAnonymous();
}

app.UseExceptionHandler();
app.UseWhen(ApiProblems.IsVersionedApi, versioned => versioned.UseStatusCodePages());
app.UseHttpsRedirection();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapSentinelOperationalEndpoints();

try
{
    Log.Information("Geospatial API starting");
    app.Run();
    Log.Information("Geospatial API started successfully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
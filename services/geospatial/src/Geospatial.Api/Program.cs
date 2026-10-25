using Geospatial.Api.Contracts;
using Geospatial.Api.Middlewares;
using Geospatial.Api.Security;
using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Application.UseCases;
using Geospatial.Infrastructure;
using Geospatial.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

var builder = WebApplication.CreateBuilder(args);

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithMachineName()
        .WriteTo.Console();

    var lokiUrl = context.Configuration["Loki:Url"];
    if (!string.IsNullOrEmpty(lokiUrl))
    {
        config.WriteTo.GrafanaLoki(lokiUrl);
    }
});

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

builder.Services
    .Configure<ElasticsearchOptions>(builder.Configuration.GetSection("Elasticsearch"))
    .Configure<KafkaConsumerOptions>(builder.Configuration.GetSection("KafkaConsumerOptions"))
    .Configure<Geospatial.Infrastructure.Options.KafkaProducerOptions>(builder.Configuration.GetSection("KafkaProducerOptions"));

builder.Services
    .AddScoped<IBatchEvaluateUseCase, BatchEvaluateUseCase>()
    .AddScoped<IWithinRadiusUseCase, WithinRadiusUseCase>()
    .AddScoped<IIntersectsUseCase, IntersectsUseCase>()
    .AddScoped<IDistanceUseCase, DistanceUseCase>()
    .AddScoped<IContainsPointUseCase, ContainsPointUseCase>()
    .AddScoped<ISearchGeospatialEventsUseCase, SearchGeospatialEventsUseCase>();

var app = builder.Build();

app.UseSerilogRequestLogging();

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
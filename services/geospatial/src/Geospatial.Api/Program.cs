using Geospatial.Api.Middlewares;
using Geospatial.Application.Interfaces.Repositories;
using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Application.UseCases;
using Geospatial.Domain.Services;
using Geospatial.Infrastructure.Elasticsearch;
using Geospatial.Infrastructure.GeometryEngine;
using Geospatial.Infrastructure.Repositories;
using Nest;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddOpenApi();

builder.Services.AddElasticsearch(builder.Configuration);
builder.Services.AddScoped<IGeospatialEventRepository, ElasticsearchGeospatialEventRepository>();

builder.Services.AddScoped<IBatchEvaluateUseCase, BatchEvaluateUseCase>();
builder.Services.AddScoped<IWithinRadiusUseCase, WithinRadiusUseCase>();
builder.Services.AddScoped<IIntersectsUseCase, IntersectsUseCase>();
builder.Services.AddScoped<IDistanceUseCase, DistanceUseCase>();
builder.Services.AddScoped<IContainsPointUseCase, ContainsPointUseCase>();
builder.Services.AddScoped<IGeospatialCalculator, NetTopologyGeospatialCalculator>();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Sentinel - Geospatial API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });

    var client = app.Services.GetRequiredService<IElasticClient>();
    await ElasticsearchExtensions.EnsureIndexExistsAsync(client);
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();

try
{
    Log.Information("Geospatial API starting");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

using Geospatial.Api.Middlewares;
using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Application.UseCases;
using Geospatial.Domain.Services;
using Geospatial.Infrastructure.GeometryEngine;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddOpenApi();

builder.Services.AddScoped<IBatchEvaluateUseCase, BatchEvaluateUseCase>();
builder.Services.AddScoped<IWithinRadiusUseCase, WithinRadiusUseCase>();
builder.Services.AddScoped<IIntersectsUseCase, IntersectsUseCase>();
builder.Services.AddScoped<IDistanceUseCase, DistanceUseCase>();
builder.Services.AddScoped<IContainsPointUseCase, ContainsPointUseCase>();
builder.Services.AddScoped<IGeospatialCalculator, NetTopologyGeospatialCalculator>();

var app = builder.Build();

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
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();
app.Run();
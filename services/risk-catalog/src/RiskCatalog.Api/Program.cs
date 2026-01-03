using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using RiskCatalog.Api.Filters;
using RiskCatalog.Api.Middlewares;
using RiskCatalog.Application;
using RiskCatalog.Infrastructure;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(builder.Configuration["ElasticSearch:URI"]!))
    {
        AutoRegisterTemplate = true,
        IndexFormat = $"risk-catalog-logs-{environment.ToLower()}-{DateTime.UtcNow:yyyy-MM}",
        NumberOfShards = 1,
        NumberOfReplicas = 1,
        MinimumLogEventLevel = Serilog.Events.LogEventLevel.Information,
        FailureCallback = e =>
            Console.WriteLine("An error occurred while sending logs to Elasticsearch: " + e.MessageTemplate)
    })
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers(options => { options.Filters.Add<ResponseWrapperFilter>(); });
builder.Services
    .AddOpenApi()
    .AddApplicationServiceCollection()
    .AddInfrastructureServiceCollection(builder.Configuration)
    .Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            var responseObj = new
            {
                title = "One or more validation errors occurred.",
                type = "RequestFormat",
                statusCode = HttpStatusCode.BadRequest,
                success = false,
                errors = new
                {
                    messages = errors
                }
            };

            return new BadRequestObjectResult(responseObj);
        };
    });
builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy("RiskCatalogWrite", policy => policy.RequireRole("RiskCatalog.Admin", "RiskCatalog.Write"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<TenantValidationMiddleware>();
app.MapControllers();

app.Run();
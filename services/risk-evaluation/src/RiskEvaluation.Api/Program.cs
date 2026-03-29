using System.Net;
using RiskEvaluation.Application;
using RiskEvaluation.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using RiskEvaluation.Api.Filters;
using RiskEvaluation.Api.Middlewares;
using RiskEvaluation.Infrastructure.HostedServices;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options => { options.Filters.Add<ResponseWrapperFilter>(); });
builder.Services.AddOpenApi();
builder.Services.Configure<ApiBehaviorOptions>(options =>
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
// Logging
builder.Host.UseSerilog((context, config) => { config.ReadFrom.Configuration(context.Configuration); });

// MongoDB Serialization
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Layered Service Registration
builder.Services
    .AddApplicationServiceCollection()
    .AddInfrastructureServiceCollection(builder.Configuration);

// Hosted Service for Consumer
builder.Services.AddHostedService<MessageConsumerHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Sentinel - Risk Evaluation API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();

app.Run();
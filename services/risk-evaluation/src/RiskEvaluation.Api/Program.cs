using System.Net;
using RiskEvaluation.Application;
using RiskEvaluation.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using RiskEvaluation.Api.Contracts;
using RiskEvaluation.Api.Filters;
using RiskEvaluation.Api.Middlewares;
using RiskEvaluation.Api.Observability;
using RiskEvaluation.Api.Security;
using RiskEvaluation.Infrastructure.HealthChecks;
using RiskEvaluation.Infrastructure.HostedServices;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options => { options.Filters.Add<ResponseWrapperFilter>(); });
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
builder.Services.AddRiskEvaluationAuthentication(builder.Configuration);
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        if (ApiProblems.IsVersionedApi(context.HttpContext)) return ModelValidationProblems.For(context);

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
builder.AddSentinelObservability("risk-evaluation");
builder.Services.AddHealthChecks().AddRiskEvaluationReadinessChecks(ObservabilityExtensions.ReadinessTag);

// MongoDB Serialization
BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Layered Service Registration
builder.Services
    .AddApplicationServiceCollection()
    .AddInfrastructureServiceCollection(builder.Configuration);

var app = builder.Build();

app.UseSentinelRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Sentinel - Risk Evaluation API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    }).AllowAnonymous();
}

app.UseExceptionHandler();
app.UseWhen(ApiProblems.IsVersionedApi, versioned => versioned.UseStatusCodePages());
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();
app.MapSentinelOperationalEndpoints();

app.Run();

public partial class Program;
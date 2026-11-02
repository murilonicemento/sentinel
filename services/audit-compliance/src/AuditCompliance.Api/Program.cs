using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AuditCompliance.Api.Errors;
using AuditCompliance.Api.Filters;
using AuditCompliance.Api.Observability;
using AuditCompliance.Api.Security;
using AuditCompliance.Application;
using AuditCompliance.Infrastructure;
using AuditCompliance.Infrastructure.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddSentinelObservability("audit-compliance", activitySources: [AuditTelemetry.ActivitySourceName],
    meters: [AuditMetrics.MeterName, "Microsoft.AspNetCore.Server.Kestrel"]);

builder.Services.AddControllers(options => { options.Filters.Add<ResponseWrapperFilter>(); })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(e => "The value is invalid or malformed.").Distinct().ToArray());

        return ApiProblems.Create(context.HttpContext, StatusCodes.Status400BadRequest,
            ApiProblems.DefaultCodeFor(StatusCodes.Status400BadRequest), "One or more validation errors occurred.",
            errors);
    };
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var status = context.ProblemDetails.Status ?? StatusCodes.Status500InternalServerError;
        var code = ApiProblems.DefaultCodeFor(status);

        context.ProblemDetails.Extensions.TryAdd(ApiProblems.CodeExtension, code);
        context.ProblemDetails.Extensions.TryAdd(ApiProblems.TraceIdExtension, ApiProblems.TraceId(context.HttpContext));
        context.ProblemDetails.Type ??= ApiProblems.TypeFor(code);
    };
});
builder.Services.AddExceptionHandler<RequestCancelledExceptionHandler>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Sentinel - Audit & Compliance API";
        document.Info.Version = "v1";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["BearerAuth"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (metadata.Any(m => m is IAuthorizeData) && !metadata.Any(m => m is IAllowAnonymous))
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "BearerAuth" }
                        },
                        Array.Empty<string>()
                    }
                }
            ];

        if (metadata.Any(m => m is TenantContextAttribute))
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = TenantContextAttribute.HeaderName,
                In = ParameterLocation.Header,
                Required = true,
                Description = "Tenant da operação. Deve constar nas claims tenant_id do token.",
                Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
            });
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwt) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SigningKey)),
            NameClaimType = AuditClaims.Subject,
            RoleClaimType = AuditClaims.Role,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(AuditPolicies.Read, policy => policy
        .RequireClaim(AuditClaims.Subject)
        .RequireRole(AuditRoles.Admin, AuditRoles.Read))
    .AddPolicy(AuditPolicies.ReadStates, policy => policy
        .RequireClaim(AuditClaims.Subject)
        .RequireRole(AuditRoles.Admin, AuditRoles.ReadSensitive))
    .AddPolicy(AuditPolicies.Export, policy => policy
        .RequireClaim(AuditClaims.Subject)
        .RequireRole(AuditRoles.Admin, AuditRoles.Export));

var rateLimits = builder.Configuration.GetSection("RateLimiting");
var queriesPerMinute = rateLimits.GetValue("QueriesPerMinute", 120);
var exportsPerMinute = rateLimits.GetValue("ExportsPerMinute", 10);

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(RateLimitPolicies.Queries, context => FixedWindowPerIdentity(context, queriesPerMinute));
    options.AddPolicy(RateLimitPolicies.Exports, context => FixedWindowPerIdentity(context, exportsPerMinute));
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        var problem = ApiProblems.Create(context.HttpContext, StatusCodes.Status429TooManyRequests,
            ApiProblems.DefaultCodeFor(StatusCodes.Status429TooManyRequests), "Too many requests. Retry later.");

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(problem.Value, options: null,
            contentType: "application/problem+json", cancellationToken);
    };
});

builder.Services.AddAuditComplianceApplication();
builder.Services.AddAuditComplianceInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseSentinelRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Sentinel - Audit & Compliance API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .AddPreferredSecuritySchemes("BearerAuth");
    }).AllowAnonymous();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// /metrics é destinado à coleta interna pelo Prometheus; não deve ser roteado pelo api-gateway.
app.MapSentinelOperationalEndpoints();

app.Run();

static RateLimitPartition<string> FixedWindowPerIdentity(HttpContext context, int permitsPerMinute)
{
    var partitionKey = context.User.FindFirst(AuditClaims.Subject)?.Value ??
                       context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

    return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitsPerMinute,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    });
}

public partial class Program;

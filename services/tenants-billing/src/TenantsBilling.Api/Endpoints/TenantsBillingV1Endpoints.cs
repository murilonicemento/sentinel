using System.Security.Claims;
using TenantsBilling.Api.Contracts;
using TenantsBilling.Api.Security;
using TenantsBilling.Application.DTOs;
using TenantsBilling.Application.Services;

namespace TenantsBilling.Api.Endpoints;

public sealed record CreateTenantRequest(string? Name, Guid PlanId, string? Region, string? TimeZone);

public sealed record CreatePlanRequest(
    string? Name,
    int MaxEventsPerMonth,
    int MaxAlertsPerMonth,
    int MaxApiRequestsPerMonth,
    int MaxChannelsPerMonth,
    int? SoftLimitPercentage,
    int? HardLimitPercentage);

public sealed record RegisterUsageRequest(int EventsConsumed, int AlertsTriggered, int ApiRequests, int ChannelUsage);

public static class TenantsBillingV1Endpoints
{
    public const string BasePath = "/api/v1/tenants-billing";

    public static IEndpointRouteBuilder MapTenantsBillingV1Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(BasePath).WithTags("Tenants & Billing v1");

        group.MapPost("/tenants", CreateTenant)
            .RequireAuthorization(TenantsBillingPolicies.Write)
            .Produces<ResponseBaseDTO<TenantDTO>>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/tenants/{tenantId:guid}", GetTenant)
            .RequireAuthorization(TenantsBillingPolicies.Read)
            .Produces<ResponseBaseDTO<TenantDTO>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/tenants/{tenantId:guid}/usage", RegisterUsage)
            .RequireAuthorization(TenantsBillingPolicies.Write)
            .Produces<ResponseBaseDTO<UsageResultDTO>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/plans", CreatePlan)
            .RequireAuthorization(TenantsBillingPolicies.Write)
            .Produces<ResponseBaseDTO<PlanDTO>>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> CreateTenant(CreateTenantRequest request, HttpContext httpContext,
        TenantManagementService service, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors["name"] = ["The name is required."];
        if (request.PlanId == Guid.Empty) errors["planId"] = ["The planId is required."];
        if (errors.Count > 0) return ApiProblems.Validation(httpContext, errors);

        var tenant = await service.CreateTenantAsync(
            new CreateTenantCommand(request.Name!, request.PlanId, request.Region, request.TimeZone), cancellationToken);

        return TypedResults.Created($"{BasePath}/tenants/{tenant.Id}", Envelope(StatusCodes.Status201Created,
            TenantDTO.From(tenant)));
    }

    private static async Task<IResult> GetTenant(Guid tenantId, ClaimsPrincipal user, HttpContext httpContext,
        TenantAccessEvaluator tenantAccess, TenantManagementService service, CancellationToken cancellationToken)
    {
        if (!tenantAccess.CanAccessTenant(user, tenantId))
            return ApiProblems.Result(httpContext, StatusCodes.Status403Forbidden, "TENANT_ACCESS_DENIED",
                "The authenticated identity is not authorized for the requested tenant.");

        var tenant = await service.GetTenantAsync(tenantId, cancellationToken);
        return tenant is null
            ? ApiProblems.Result(httpContext, StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant not found.")
            : TypedResults.Ok(Envelope(StatusCodes.Status200OK, TenantDTO.From(tenant)));
    }

    private static async Task<IResult> RegisterUsage(Guid tenantId, RegisterUsageRequest request,
        ClaimsPrincipal user, HttpContext httpContext, TenantAccessEvaluator tenantAccess,
        TenantManagementService service, CancellationToken cancellationToken)
    {
        if (!tenantAccess.CanAccessTenant(user, tenantId))
            return ApiProblems.Result(httpContext, StatusCodes.Status403Forbidden, "TENANT_ACCESS_DENIED",
                "The authenticated identity is not authorized for the requested tenant.");

        var errors = new Dictionary<string, string[]>();
        if (request.EventsConsumed < 0) errors["eventsConsumed"] = ["The value must not be negative."];
        if (request.AlertsTriggered < 0) errors["alertsTriggered"] = ["The value must not be negative."];
        if (request.ApiRequests < 0) errors["apiRequests"] = ["The value must not be negative."];
        if (request.ChannelUsage < 0) errors["channelUsage"] = ["The value must not be negative."];
        if (errors.Count > 0) return ApiProblems.Validation(httpContext, errors);

        var status = await service.RegisterUsageAsync(tenantId, request.EventsConsumed, request.AlertsTriggered,
            request.ApiRequests, request.ChannelUsage, cancellationToken);

        return TypedResults.Ok(Envelope(StatusCodes.Status200OK, UsageResultDTO.From(tenantId, status)));
    }

    private static async Task<IResult> CreatePlan(CreatePlanRequest request, HttpContext httpContext,
        PlanManagementService service, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiProblems.Validation(httpContext,
                new Dictionary<string, string[]> { ["name"] = ["The name is required."] });

        var plan = await service.CreatePlanAsync(new CreatePlanCommand(
            request.Name,
            request.MaxEventsPerMonth,
            request.MaxAlertsPerMonth,
            request.MaxApiRequestsPerMonth,
            request.MaxChannelsPerMonth,
            request.SoftLimitPercentage ?? 80,
            request.HardLimitPercentage ?? 100), cancellationToken);

        return TypedResults.Created($"{BasePath}/plans/{plan.Id}", Envelope(StatusCodes.Status201Created,
            PlanDTO.From(plan)));
    }

    private static ResponseBaseDTO<T> Envelope<T>(int statusCode, T data) =>
        new() { StatusCode = statusCode, Success = true, Data = data };
}

using System.Security.Claims;
using TenantsBilling.Api.Contracts;
using TenantsBilling.Api.Security;
using TenantsBilling.Application.DTOs;
using TenantsBilling.Application.Services;
using TenantsBilling.Domain.Enums;

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

/// <summary>Target status: <c>Active</c>, <c>Suspended</c> or <c>Canceled</c> (case-insensitive).</summary>
public sealed record ChangeTenantStatusRequest(string? Status);

public static class TenantsBillingV1Endpoints
{
    public const string BasePath = "/api/v1/tenants-billing";
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 50;

    // Statuses a client may name. Parsed by name only: Enum.TryParse would also accept numbers such as "2".
    private static readonly Dictionary<string, TenantStatus> StatusNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Active"] = TenantStatus.Active,
        ["Suspended"] = TenantStatus.Suspended,
        ["Canceled"] = TenantStatus.Canceled
    };

    private static readonly Dictionary<string, TenantStatus> FilterStatusNames = new(StatusNames, StringComparer.OrdinalIgnoreCase)
    {
        ["Pending"] = TenantStatus.Pending
    };

    public static IEndpointRouteBuilder MapTenantsBillingV1Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(BasePath).WithTags("Tenants & Billing v1");

        group.MapPost("/tenants", CreateTenant)
            .RequireAuthorization(TenantsBillingPolicies.Write)
            .Produces<ResponseBaseDTO<TenantDTO>>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/tenants", ListTenants)
            .RequireAuthorization(TenantsBillingPolicies.Read)
            .Produces<ResponseBaseDTO<PagedResultDTO<TenantDTO>>>()
            .ProducesValidationProblem()
            .WithDescription("Tenants the identity belongs to (every tenant for a trusted service), ordered by name " +
                "and then id. Optional status filter: Active, Suspended, Canceled or Pending.");

        group.MapPatch("/tenants/{tenantId:guid}/status", ChangeTenantStatus)
            .RequireAuthorization(TenantsBillingPolicies.Admin)
            .Produces<ResponseBaseDTO<TenantDTO>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithDescription("Suspends, activates or cancels the tenant. Canceled is terminal; asking for the " +
                "current status returns the tenant unchanged.");

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

        group.MapGet("/plans", ListPlans)
            .RequireAuthorization(TenantsBillingPolicies.Read)
            .Produces<ResponseBaseDTO<PagedResultDTO<PlanDTO>>>()
            .ProducesValidationProblem()
            .WithDescription("Plans ordered by name and then id.");

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

    private static async Task<IResult> ListTenants(int? page, int? pageSize, string? status, ClaimsPrincipal user,
        HttpContext httpContext, TenantAccessEvaluator tenantAccess, TenantManagementService service,
        CancellationToken cancellationToken)
    {
        var errors = PaginationErrors(page, pageSize, out var resolvedPage, out var resolvedPageSize);
        TenantStatus? statusFilter = null;
        if (status is not null)
        {
            if (FilterStatusNames.TryGetValue(status, out var parsed)) statusFilter = parsed;
            else errors["status"] = ["The status must be Active, Suspended, Canceled or Pending."];
        }

        if (errors.Count > 0) return ApiProblems.Validation(httpContext, errors);

        // A user lists only the tenants in its tenant_id claims; a trusted service acts at platform level.
        IReadOnlyCollection<Guid>? tenantIds = tenantAccess.IsServicePrincipal(user)
            ? null
            : tenantAccess.TenantIds(user).Select(id => Guid.TryParse(id, out var parsedId) ? parsedId : Guid.Empty)
                .Where(id => id != Guid.Empty).Distinct().ToList();

        var tenants = await service.ListTenantsAsync(tenantIds, statusFilter, resolvedPage, resolvedPageSize,
            cancellationToken);
        return TypedResults.Ok(Envelope(StatusCodes.Status200OK,
            PagedResultDTO<TenantDTO>.From(tenants, resolvedPage, resolvedPageSize, TenantDTO.From)));
    }

    private static async Task<IResult> ChangeTenantStatus(Guid tenantId, ChangeTenantStatusRequest request,
        ClaimsPrincipal user, HttpContext httpContext, TenantAccessEvaluator tenantAccess,
        TenantManagementService service, CancellationToken cancellationToken)
    {
        if (!tenantAccess.CanAccessTenant(user, tenantId))
            return ApiProblems.Result(httpContext, StatusCodes.Status403Forbidden, "TENANT_ACCESS_DENIED",
                "The authenticated identity is not authorized for the requested tenant.");

        if (string.IsNullOrWhiteSpace(request.Status) || !StatusNames.TryGetValue(request.Status, out var target))
            return ApiProblems.Validation(httpContext, new Dictionary<string, string[]>
            {
                ["status"] = ["The status must be Active, Suspended or Canceled."]
            });

        var tenant = await service.ChangeStatusAsync(tenantId, target, cancellationToken);
        return TypedResults.Ok(Envelope(StatusCodes.Status200OK, TenantDTO.From(tenant)));
    }

    private static async Task<IResult> ListPlans(int? page, int? pageSize, HttpContext httpContext,
        PlanManagementService service, CancellationToken cancellationToken)
    {
        var errors = PaginationErrors(page, pageSize, out var resolvedPage, out var resolvedPageSize);
        if (errors.Count > 0) return ApiProblems.Validation(httpContext, errors);

        var plans = await service.ListPlansAsync(resolvedPage, resolvedPageSize, cancellationToken);
        return TypedResults.Ok(Envelope(StatusCodes.Status200OK,
            PagedResultDTO<PlanDTO>.From(plans, resolvedPage, resolvedPageSize, PlanDTO.From)));
    }

    /// <summary>Page starts at 1; page size defaults to 10, maximum 50. Out-of-range values are rejected.</summary>
    private static Dictionary<string, string[]> PaginationErrors(int? page, int? pageSize, out int resolvedPage,
        out int resolvedPageSize)
    {
        var errors = new Dictionary<string, string[]>();
        resolvedPage = page ?? 1;
        resolvedPageSize = pageSize ?? DefaultPageSize;

        if (resolvedPage < 1) errors["page"] = ["The page must be greater than or equal to 1."];
        if (resolvedPageSize is < 1 or > MaxPageSize)
            errors["pageSize"] = [$"The pageSize must be between 1 and {MaxPageSize}."];

        return errors;
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

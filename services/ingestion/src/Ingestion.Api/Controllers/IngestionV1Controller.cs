using Ingestion.Api.Contracts;
using Ingestion.Api.Security;
using Ingestion.Application.Commands;
using Ingestion.Application.DTO;
using Ingestion.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ingestion.Api.Controllers;

public sealed record CollectionAcceptedDTO(Guid DataSourceId);

/// <summary>
/// Ingestion API v1. Collections are processed synchronously up to the outbox; the detected events reach Kafka
/// asynchronously, so collection endpoints answer 202. Timestamps are ISO 8601 UTC.
/// </summary>
[ApiController]
[Route(BasePath)]
public class IngestionV1Controller : ControllerBase
{
    public const string BasePath = "api/v1/ingestion";

    private readonly IMediator _mediator;
    private readonly TenantAccessEvaluator _tenantAccess;

    public IngestionV1Controller(IMediator mediator, TenantAccessEvaluator tenantAccess)
    {
        _mediator = mediator;
        _tenantAccess = tenantAccess;
    }

    /// <summary>Detected events of the caller's tenants, newest first (ties broken by event id).</summary>
    [HttpGet("detected-events")]
    [Authorize(Policy = IngestionPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<DetectedEventDTO>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> GetDetectedEvents([FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryResolve(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var errors))
            return Invalid(errors);

        var result = await _mediator.Send(new GetDetectedEventsPageQuery(Scope(), resolvedPage, resolvedPageSize),
            cancellationToken);
        return Ok(Envelope(StatusCodes.Status200OK, result));
    }

    /// <summary>Statistics of the caller's detected events; both dates are optional and inclusive.</summary>
    [HttpGet("detected-events/statistics")]
    [Authorize(Policy = IngestionPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<DetectedEventStatisticsDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> GetStatistics([FromQuery] DateTime? initialDate, [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        if (initialDate is { } start && endDate is { } end && start > end)
            return Invalid(new Dictionary<string, string[]>
            {
                ["initialDate"] = ["The initialDate must not be after endDate."]
            });

        var result = await _mediator.Send(new GetDetectedEventStatisticsQuery(Scope(), initialDate, endDate),
            cancellationToken);
        return Ok(Envelope(StatusCodes.Status200OK, result));
    }

    [HttpPost("data-sources")]
    [Authorize(Policy = IngestionPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<RegisterDatasourceResponseDTO>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> RegisterDataSource([FromBody] RegisterDataSourceCommand command,
        CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, command.TenantId)) return TenantAccessDenied();

        var (dataSourceId, tenantId) = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, Envelope(StatusCodes.Status201Created,
            new RegisterDatasourceResponseDTO { DataSourceId = dataSourceId, TenantId = tenantId }));
    }

    /// <summary>Registers a collection, routing it to the climatic or disaster domain of its data source.</summary>
    [HttpPost("sensor-collections")]
    [Authorize(Policy = IngestionPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<CollectionAcceptedDTO>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> RegisterSensorCollection([FromBody] RegisterSensorCollectionCommand command,
        CancellationToken cancellationToken) =>
        AcceptCollection(command.TenantId, command, cancellationToken);

    [HttpPost("climatic-collections")]
    [Authorize(Policy = IngestionPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<CollectionAcceptedDTO>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> RegisterClimaticCollection([FromBody] RegisterClimaticEventCommand command,
        CancellationToken cancellationToken) =>
        AcceptCollection(command.TenantId, command, cancellationToken);

    [HttpPost("disaster-collections")]
    [Authorize(Policy = IngestionPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<CollectionAcceptedDTO>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> RegisterDisasterCollection([FromBody] RegisterDisasterEventCommand command,
        CancellationToken cancellationToken) =>
        AcceptCollection(command.TenantId, command, cancellationToken);

    private async Task<IActionResult> AcceptCollection(Guid tenantId, IRequest<Guid> command,
        CancellationToken cancellationToken)
    {
        if (!_tenantAccess.CanAccessTenant(User, tenantId)) return TenantAccessDenied();

        var dataSourceId = await _mediator.Send(command, cancellationToken);
        return Accepted(Envelope(StatusCodes.Status202Accepted, new CollectionAcceptedDTO(dataSourceId)));
    }

    private TenantAccessScope Scope() =>
        _tenantAccess.IsServicePrincipal(User)
            ? TenantAccessScope.Platform
            : TenantAccessScope.ForTenants(_tenantAccess.TenantIds(User)
                .Select(id => Guid.TryParse(id, out var tenantId) ? tenantId : (Guid?)null)
                .OfType<Guid>());

    private IActionResult Invalid(Dictionary<string, string[]> errors) =>
        new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationError,
            "One or more fields are invalid.", errors));

    private IActionResult TenantAccessDenied() =>
        new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status403Forbidden, "TENANT_ACCESS_DENIED",
            "The authenticated identity is not authorized for the requested tenant."));

    private static ResponseBaseDTO<T> Envelope<T>(int statusCode, T data) =>
        new() { StatusCode = statusCode, Success = true, Data = data };
}

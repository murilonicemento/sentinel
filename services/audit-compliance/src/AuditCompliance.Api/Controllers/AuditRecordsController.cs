using AuditCompliance.Api.Errors;
using AuditCompliance.Api.Security;
using AuditCompliance.Application.DTOs;
using AuditCompliance.Application.Records;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AuditCompliance.Api.Controllers;

[ApiController]
[Route("api/v1/audit-compliance/records")]
[Authorize(Policy = AuditPolicies.Read)]
[TenantContext]
[EnableRateLimiting(RateLimitPolicies.Queries)]
public class AuditRecordsController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly GetAuditRecordByIdHandler _getByIdHandler;
    private readonly SearchAuditRecordsHandler _searchHandler;

    public AuditRecordsController(
        SearchAuditRecordsHandler searchHandler,
        GetAuditRecordByIdHandler getByIdHandler,
        IAuthorizationService authorizationService)
    {
        _searchHandler = searchHandler;
        _getByIdHandler = getByIdHandler;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Consulta paginada dos registros do tenant informado em X-Tenant-Id. O intervalo [from, to) é
    /// obrigatório; ordenação padrão: occurredAt decrescente, com Id como desempate.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<AuditRecordSummaryDTO>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Search(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? eventType,
        [FromQuery] string? actorId,
        [FromQuery] string? sourceService,
        [FromQuery] string? resourceType,
        [FromQuery] string? resourceId,
        [FromQuery] string? correlationId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        CancellationToken cancellationToken)
    {
        var result = await _searchHandler.HandleAsync(new SearchAuditRecordsQuery
        {
            TenantId = TenantContextAttribute.GetTenantId(HttpContext),
            Filter = new AuditFilterInput
            {
                From = from,
                To = to,
                EventType = eventType,
                ActorId = actorId,
                SourceService = sourceService,
                ResourceType = resourceType,
                ResourceId = resourceId,
                CorrelationId = correlationId
            },
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection
        }, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ApiProblems.FromError(HttpContext, result.Error!);
    }

    /// <summary>
    /// Detalhe de um registro, incluindo a verificação do hash de integridade. Os estados anterior e
    /// posterior só são retornados para identidades com o papel Audit.Sensitive ou Audit.Admin.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ResponseBaseDTO<AuditRecordDetailDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var canReadStates = (await _authorizationService.AuthorizeAsync(User, AuditPolicies.ReadStates)).Succeeded;

        var result = await _getByIdHandler.HandleAsync(
            new GetAuditRecordByIdQuery(TenantContextAttribute.GetTenantId(HttpContext), id, canReadStates),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ApiProblems.FromError(HttpContext, result.Error!);
    }
}

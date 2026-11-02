using AuditCompliance.Api.Errors;
using AuditCompliance.Api.Security;
using AuditCompliance.Application.DTOs;
using AuditCompliance.Application.Exports;
using AuditCompliance.Application.Records;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AuditCompliance.Api.Controllers;

[ApiController]
[Route("api/v1/audit-compliance/exports")]
[Authorize(Policy = AuditPolicies.Export)]
[TenantContext]
[EnableRateLimiting(RateLimitPolicies.Exports)]
public class AuditExportsController : ControllerBase
{
    private readonly DownloadAuditExportHandler _downloadHandler;
    private readonly GetAuditExportHandler _getHandler;
    private readonly RequestAuditExportHandler _requestHandler;

    public AuditExportsController(
        RequestAuditExportHandler requestHandler,
        GetAuditExportHandler getHandler,
        DownloadAuditExportHandler downloadHandler)
    {
        _requestHandler = requestHandler;
        _getHandler = getHandler;
        _downloadHandler = downloadHandler;
    }

    /// <summary>
    /// Solicita uma exportação assíncrona (CSV ou JSON). A solicitação é registrada na trilha de
    /// auditoria antes de ser aceita.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ResponseBaseDTO<AuditExportDTO>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> RequestExport([FromBody] AuditExportRequestDTO request,
        CancellationToken cancellationToken)
    {
        var result = await _requestHandler.HandleAsync(new RequestAuditExportCommand
        {
            TenantId = TenantContextAttribute.GetTenantId(HttpContext),
            RequestedBy = RequesterId(),
            Format = request.Format,
            Filter = new AuditFilterInput
            {
                From = request.From,
                To = request.To,
                EventType = request.EventType,
                ActorId = request.ActorId,
                SourceService = request.SourceService,
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId,
                CorrelationId = request.CorrelationId
            },
            CorrelationId = ApiProblems.TraceId(HttpContext)
        }, cancellationToken);

        if (!result.IsSuccess) return ApiProblems.FromError(HttpContext, result.Error!);

        return AcceptedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ResponseBaseDTO<AuditExportDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _getHandler.HandleAsync(
            new GetAuditExportQuery(TenantContextAttribute.GetTenantId(HttpContext), id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ApiProblems.FromError(HttpContext, result.Error!);
    }

    /// <summary>
    /// Baixa o arquivo de uma exportação concluída e ainda válida. Cada download é auditado.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "text/csv", "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await _downloadHandler.HandleAsync(
            new DownloadAuditExportQuery(TenantContextAttribute.GetTenantId(HttpContext), id, RequesterId(),
                ApiProblems.TraceId(HttpContext)),
            cancellationToken);

        if (!result.IsSuccess) return ApiProblems.FromError(HttpContext, result.Error!);

        Response.Headers.CacheControl = "no-store";
        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName);
    }

    private string RequesterId()
    {
        // A política de autorização exige usuário autenticado; o "sub" identifica o solicitante.
        return User.FindFirst(AuditClaims.Subject)?.Value ??
               throw new InvalidOperationException("Authenticated identity has no subject claim.");
    }
}

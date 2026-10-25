using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiskEvaluation.Api.Contracts;
using RiskEvaluation.Api.Security;
using RiskEvaluation.Application.Commands;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Application.Queries;

namespace RiskEvaluation.Api.Controllers;

[ApiController]
[Route(BasePath)]
public class RiskEvaluationV1Controller : ControllerBase
{
    public const string BasePath = "api/v1/risk-evaluation";

    private readonly IMediator _mediator;

    public RiskEvaluationV1Controller(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Evaluates the risk of a location. Coordinates are whole degrees (latitude -90..90, longitude -180..180), the
    /// precision currently stored by the service.
    /// </summary>
    [HttpPost("evaluations")]
    [Authorize(Policy = RiskEvaluationPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<EvaluateRiskResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Evaluate([FromBody] EvaluateRiskRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new EvaluateRiskCommand
        {
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Timestamp = request.Timestamp,
            Metrics = request.Metrics,
            Events = request.Events
        }, cancellationToken);

        return Created($"/{BasePath}/evaluations/{result.Id}", Envelope(StatusCodes.Status201Created, result));
    }

    /// <summary>Lists the evaluations of a location, newest first and then by id.</summary>
    [HttpGet("evaluations")]
    [Authorize(Policy = RiskEvaluationPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<RiskEvaluationDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> History(
        [FromQuery] int? latitude,
        [FromQuery] int? longitude,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        Pagination.TryResolve(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var errors);
        if (latitude is null or < -90 or > 90) errors["latitude"] = ["The latitude must be between -90 and 90."];
        if (longitude is null or < -180 or > 180)
            errors["longitude"] = ["The longitude must be between -180 and 180."];
        if (errors.Count > 0)
            return new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status400BadRequest,
                ApiProblems.ValidationError, "One or more fields are invalid.", errors));

        var result = await _mediator.Send(new GetRiskHistoryPageQuery
        {
            Latitude = latitude!.Value,
            Longitude = longitude!.Value,
            Page = resolvedPage,
            PageSize = resolvedPageSize
        }, cancellationToken);

        return Ok(Envelope(StatusCodes.Status200OK, result));
    }

    [HttpGet("evaluations/{id:guid}")]
    [Authorize(Policy = RiskEvaluationPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<RiskEvaluationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRiskEvaluationByIdQuery { Id = id }, cancellationToken);

        return result is null
            ? new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status404NotFound,
                "RISK_EVALUATION_NOT_FOUND", "Risk evaluation not found."))
            : Ok(Envelope(StatusCodes.Status200OK, result));
    }

    /// <summary>Replaces the risk model used by every evaluation. Restricted to RiskEvaluation.Admin.</summary>
    [HttpPut("model")]
    [Authorize(Roles = RiskEvaluationRoles.Admin)]
    [ProducesResponseType<ResponseBaseDTO<UpdateRiskModelResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> UpdateModel([FromBody] UpdateRiskModelRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateRiskModelCommand
        {
            Version = request.Version,
            Parameters = request.Parameters,
            Formula = request.Formula
        }, cancellationToken);

        // The handler reports invalid models through Success=false; its message is not echoed to the client.
        return result.Success
            ? Ok(Envelope(StatusCodes.Status200OK, result))
            : new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status400BadRequest,
                "INVALID_RISK_MODEL", "The risk model is invalid."));
    }

    private static ResponseBaseDTO<T> Envelope<T>(int statusCode, T data) =>
        new() { StatusCode = statusCode, Success = true, Data = data };
}

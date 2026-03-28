using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiskEvaluation.Application.Commands;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Application.Queries;

namespace RiskEvaluation.Api.Controllers;

[ApiController]
[Route("risk-evaluation")]
public class RiskEvaluationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RiskEvaluationController> _logger;

    public RiskEvaluationController(IMediator mediator, ILogger<RiskEvaluationController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("evaluate")]
    public async Task<ActionResult<EvaluateRiskResponse>> EvaluateRisk([FromBody] EvaluateRiskRequest request)
    {
        _logger.LogInformation("Received risk evaluation request for latitude: {Latitude}; longitude: {Longitude}",
            request.Latitude, request.Longitude);

        if (request.Latitude == 0)
        {
            _logger.LogWarning("Risk evaluation request rejected: Latitude is required");
            return BadRequest("Latitude is required");
        }

        if (request.Longitude == 0)
        {
            _logger.LogWarning("Risk evaluation request rejected: Longitude is required");
            return BadRequest("Longitude is required");
        }

        if (request.Metrics == null)
        {
            _logger.LogWarning(
                "Risk evaluation request rejected: Metrics are required for latitude: {Latitude}; longitude: {Longitude}",
                request.Latitude, request.Longitude);
            return BadRequest("Metrics are required");
        }

        var command = new EvaluateRiskCommand
        {
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Timestamp = request.Timestamp,
            Metrics = request.Metrics,
            Events = request.Events
        };

        var result = await _mediator.Send(command);
        _logger.LogInformation(
            "Risk evaluation completed for latitude: {Latitude}; longitude: {Longitude}, Risk Level: {RiskLevel}, Score: {Score}",
            result.Latitude, result.Longitude, result.Level, result.Score);

        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<RiskEvaluationDto>>> GetRiskHistory(
        [FromQuery] int latitude,
        [FromQuery] int longitude)
    {
        _logger.LogInformation("Fetching risk history for location: {Latitude}, {Longitude}", latitude, longitude);

        if (latitude == 0)
        {
            _logger.LogWarning("Risk history request rejected: Latitude is required");
            return BadRequest("Latitude is required");
        }

        if (longitude == 0)
        {
            _logger.LogWarning("Risk history request rejected: Longitude is required");
            return BadRequest("Longitude is required");
        }

        var query = new GetRiskHistoryQuery { Latitude = latitude, Longitude = longitude };
        var result = await _mediator.Send(query);

        _logger.LogInformation("Retrieved {Count} risk evaluation records for location: {Latitude}, {Longitude}",
            result.Count,
            latitude, longitude);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RiskEvaluationDto>> GetRiskEvaluationById(Guid id)
    {
        _logger.LogInformation("Fetching risk evaluation by ID: {Id}", id);

        var query = new GetRiskEvaluationByIdQuery { Id = id };
        var result = await _mediator.Send(query);

        if (result == null)
        {
            _logger.LogWarning("Risk evaluation not found for ID: {Id}", id);
            return NotFound();
        }

        _logger.LogInformation("Retrieved risk evaluation for ID: {Id}, Location: {Latitude}, {Longitude}", id,
            result.Latitude, result.Longitude);
        return Ok(result);
    }

    [HttpPut("model")]
    public async Task<ActionResult<UpdateRiskModelResponse>> UpdateRiskModel([FromBody] UpdateRiskModelRequest request)
    {
        _logger.LogInformation("Received risk model update request for version: {Version}", request.Version);

        if (string.IsNullOrEmpty(request.Version))
        {
            _logger.LogWarning("Risk model update rejected: Version is required");
            return BadRequest("Version is required");
        }

        var command = new UpdateRiskModelCommand
        {
            Version = request.Version,
            Parameters = request.Parameters,
            Formula = request.Formula
        };

        var result = await _mediator.Send(command);
        _logger.LogInformation("Risk model updated successfully to version: {Version}", result.Version);

        return Ok(result);
    }
}
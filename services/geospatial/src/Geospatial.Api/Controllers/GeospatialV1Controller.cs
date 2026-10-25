using Geospatial.Api.Contracts;
using Geospatial.Api.Security;
using Geospatial.Application.DTOs;
using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Application.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Geospatial.Api.Controllers;

[ApiController]
[Route(BasePath)]
[Authorize(Policy = GeospatialPolicies.Read)]
public class GeospatialV1Controller : ControllerBase
{
    public const string BasePath = "api/v1/geospatial";

    private readonly IContainsPointUseCase _containsPointUseCase;
    private readonly IWithinRadiusUseCase _withinRadiusUseCase;
    private readonly IIntersectsUseCase _intersectsUseCase;
    private readonly IDistanceUseCase _distanceUseCase;
    private readonly IBatchEvaluateUseCase _batchEvaluateUseCase;
    private readonly ISearchGeospatialEventsUseCase _searchUseCase;

    public GeospatialV1Controller(
        IContainsPointUseCase containsPointUseCase,
        IWithinRadiusUseCase withinRadiusUseCase,
        IIntersectsUseCase intersectsUseCase,
        IDistanceUseCase distanceUseCase,
        IBatchEvaluateUseCase batchEvaluateUseCase,
        ISearchGeospatialEventsUseCase searchUseCase)
    {
        _containsPointUseCase = containsPointUseCase;
        _withinRadiusUseCase = withinRadiusUseCase;
        _intersectsUseCase = intersectsUseCase;
        _distanceUseCase = distanceUseCase;
        _batchEvaluateUseCase = batchEvaluateUseCase;
        _searchUseCase = searchUseCase;
    }

    [HttpPost("operations/contains-point")]
    [ProducesResponseType<ResponseBaseDTO<ContainsPointResultDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ContainsPoint([FromBody] ContainsPointRequestDTO request,
        CancellationToken cancellationToken)
    {
        var contains = await _containsPointUseCase.ExecuteAsync(GeoPolygonMapper.ToDomain(request.Area),
            GeoPointMapper.ToDomain(request.Point), cancellationToken);
        return Ok(Envelope(new ContainsPointResultDTO(contains)));
    }

    [HttpPost("operations/within-radius")]
    [ProducesResponseType<ResponseBaseDTO<WithinRadiusResultDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> WithinRadius([FromBody] WithinRadiusRequestDTO request,
        CancellationToken cancellationToken)
    {
        var result = await _withinRadiusUseCase.ExecuteAsync(GeoPointMapper.ToDomain(request.Center),
            GeoPointMapper.ToDomain(request.Point), GeoRadiusMapper.ToDomain(request.Radius), cancellationToken);
        return Ok(Envelope(new WithinRadiusResultDTO(result.WithinRadius, result.Distance)));
    }

    [HttpPost("operations/intersects")]
    [ProducesResponseType<ResponseBaseDTO<IntersectsResultDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Intersects([FromBody] IntersectsRequestDTO request,
        CancellationToken cancellationToken)
    {
        var intersects = await _intersectsUseCase.ExecuteAsync(GeoPolygonMapper.ToDomain(request.PolygonA),
            GeoPolygonMapper.ToDomain(request.PolygonB), cancellationToken);
        return Ok(Envelope(new IntersectsResultDTO(intersects)));
    }

    /// <summary>Approximate distance in meters between two points (planar, see GEO-11).</summary>
    [HttpPost("operations/distance")]
    [ProducesResponseType<ResponseBaseDTO<DistanceResultDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Distance([FromBody] DistanceRequestDTO request,
        CancellationToken cancellationToken)
    {
        var distance = await _distanceUseCase.ExecuteAsync(GeoPointMapper.ToDomain(request.From),
            GeoPointMapper.ToDomain(request.To), cancellationToken);
        return Ok(Envelope(new DistanceResultDTO(distance)));
    }

    [HttpPost("operations/evaluate-batch")]
    [ProducesResponseType<ResponseBaseDTO<BatchResponseDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> EvaluateBatch([FromBody] BatchRequestDTO request,
        CancellationToken cancellationToken)
    {
        return Ok(Envelope(await _batchEvaluateUseCase.ExecuteAsync(request, cancellationToken)));
    }

    /// <summary>Recorded events within a radius around a point, newest first.</summary>
    [HttpPost("events/search-by-radius")]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<GeospatialEventDTO>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> SearchByRadius([FromBody] SearchByRadiusRequestDTO request,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        Pagination.TryResolve(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var errors);
        ValidatePoint(request.Center, "center", errors);
        if (request.RadiusMeters <= 0) errors["radiusMeters"] = ["The radiusMeters must be greater than zero."];
        if (errors.Count > 0) return Invalid(errors);

        var result = await _searchUseCase.SearchByRadiusAsync(request.Center.Latitude, request.Center.Longitude,
            request.RadiusMeters, resolvedPage, resolvedPageSize, cancellationToken);
        return Ok(Envelope(result));
    }

    /// <summary>Recorded events inside a polygon (at least 3 points), newest first.</summary>
    [HttpPost("events/search-by-polygon")]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<GeospatialEventDTO>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> SearchByPolygon([FromBody] SearchByPolygonRequestDTO request,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        Pagination.TryResolve(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var errors);
        if (request.Polygon.Count < 3) errors["polygon"] = ["The polygon must have at least 3 points."];
        for (var i = 0; i < request.Polygon.Count; i++) ValidatePoint(request.Polygon[i], $"polygon[{i}]", errors);
        if (errors.Count > 0) return Invalid(errors);

        var result = await _searchUseCase.SearchByPolygonAsync(
            request.Polygon.Select(p => (p.Latitude, p.Longitude)).ToList(), resolvedPage, resolvedPageSize,
            cancellationToken);
        return Ok(Envelope(result));
    }

    private static void ValidatePoint(PointDTO point, string field, Dictionary<string, string[]> errors)
    {
        if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180)
            errors[field] = ["Latitude must be between -90 and 90 and longitude between -180 and 180."];
    }

    private IActionResult Invalid(Dictionary<string, string[]> errors) =>
        new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationError,
            "One or more fields are invalid.", errors));

    private static ResponseBaseDTO<T> Envelope<T>(T data) =>
        new() { StatusCode = StatusCodes.Status200OK, Success = true, Data = data };
}

using Geospatial.Application.DTOs;
using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Application.Mappers;
using Microsoft.AspNetCore.Mvc;

namespace Geospatial.Api.Controllers;

[ApiController]
[Route("api/geospatial")]
public class GeospatialController : ControllerBase
{
    private readonly IContainsPointUseCase _containsPointUseCase;
    private readonly IWithinRadiusUseCase _withinRadiusUseCase;
    private readonly IIntersectsUseCase _intersectsUseCase;
    private readonly IDistanceUseCase _distanceUseCase;
    private readonly IBatchEvaluateUseCase _batchEvaluateUseCase;

    public GeospatialController(
        IContainsPointUseCase containsPointUseCase,
        IWithinRadiusUseCase withinRadiusUseCase,
        IIntersectsUseCase intersectsUseCase,
        IDistanceUseCase distanceUseCase,
        IBatchEvaluateUseCase batchEvaluateUseCase)
    {
        _containsPointUseCase = containsPointUseCase;
        _withinRadiusUseCase = withinRadiusUseCase;
        _intersectsUseCase = intersectsUseCase;
        _distanceUseCase = distanceUseCase;
        _batchEvaluateUseCase = batchEvaluateUseCase;
    }


    [HttpPost("contains-point")]
    public async Task<IActionResult> ContainsPoint([FromBody] ContainsPointRequestDTO request, CancellationToken cancellationToken)
    {
        var area = GeoPolygonMapper.ToDomain(request.Area);
        var point = GeoPointMapper.ToDomain(request.Point);
        var contains = await _containsPointUseCase.ExecuteAsync(area, point, cancellationToken);

        return Ok(new { contains });
    }


    [HttpPost("within-radius")]
    public async Task<IActionResult> WithinRadius([FromBody] WithinRadiusRequestDTO request, CancellationToken cancellationToken)
    {
        var center = GeoPointMapper.ToDomain(request.Center);
        var target = GeoPointMapper.ToDomain(request.Point);
        var radius = GeoRadiusMapper.ToDomain(request.Radius);
        var result = await _withinRadiusUseCase.ExecuteAsync(center, target, radius, cancellationToken);

        return Ok(new
        {
            withinRadius = result.WithinRadius,
            distance = result.Distance
        });
    }

    [HttpPost("intersects")]
    public async Task<IActionResult> Intersects([FromBody] IntersectsRequestDTO request, CancellationToken cancellationToken)
    {
        var polygonA = GeoPolygonMapper.ToDomain(request.PolygonA);
        var polygonB = GeoPolygonMapper.ToDomain(request.PolygonB);
        var intersects = await _intersectsUseCase.ExecuteAsync(polygonA, polygonB, cancellationToken);

        return Ok(new { intersects });
    }

    [HttpPost("distance")]
    public async Task<IActionResult> Distance([FromBody] DistanceRequestDTO request, CancellationToken cancellationToken)
    {
        var from = GeoPointMapper.ToDomain(request.From);
        var to = GeoPointMapper.ToDomain(request.To);
        var distance = await _distanceUseCase.ExecuteAsync(from, to, cancellationToken);

        return Ok(new { distance });
    }


    [HttpPost("evaluate-batch")]
    public async Task<IActionResult> EvaluateBatch([FromBody] BatchRequestDTO request, CancellationToken cancellationToken)
    {
        var response = await _batchEvaluateUseCase.ExecuteAsync(request, cancellationToken);

        return Ok(response);
    }
}
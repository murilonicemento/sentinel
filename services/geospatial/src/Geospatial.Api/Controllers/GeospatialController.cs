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
    public IActionResult ContainsPoint([FromBody] ContainsPointRequestDTO request)
    {
        var area = GeoPolygonMapper.ToDomain(request.Area);
        var point = GeoPointMapper.ToDomain(request.Point);
        var contains = _containsPointUseCase.Execute(area, point);

        return Ok(new { contains });
    }


    [HttpPost("within-radius")]
    public IActionResult WithinRadius([FromBody] WithinRadiusRequestDTO request)
    {
        var center = GeoPointMapper.ToDomain(request.Center);
        var target = GeoPointMapper.ToDomain(request.Point);
        var radius = GeoRadiusMapper.ToDomain(request.Radius);
        var result = _withinRadiusUseCase.Execute(center, target, radius);

        return Ok(new
        {
            withinRadius = result.WithinRadius,
            distance = result.Distance
        });
    }

    [HttpPost("intersects")]
    public IActionResult Intersects([FromBody] IntersectsRequestDTO request)
    {
        var polygonA = GeoPolygonMapper.ToDomain(request.PolygonA);
        var polygonB = GeoPolygonMapper.ToDomain(request.PolygonB);
        var intersects = _intersectsUseCase.Execute(polygonA, polygonB);

        return Ok(new { intersects });
    }

    [HttpPost("distance")]
    public IActionResult Distance([FromBody] DistanceRequestDTO request)
    {
        var from = GeoPointMapper.ToDomain(request.From);
        var to = GeoPointMapper.ToDomain(request.To);
        var distance = _distanceUseCase.Execute(from, to);

        return Ok(new { distance });
    }


    [HttpPost("evaluate-batch")]
    public IActionResult EvaluateBatch([FromBody] BatchRequestDTO request)
    {
        var response = _batchEvaluateUseCase.Execute(request);

        return Ok(response);
    }
}
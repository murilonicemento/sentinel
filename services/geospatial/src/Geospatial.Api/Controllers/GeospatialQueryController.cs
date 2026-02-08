using Geospatial.Application.DTOs;
using Geospatial.Application.Interfaces.Repositories;
using Geospatial.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Geospatial.Api.Controllers;

[ApiController]
[Route("api/geospatial/query")]
public class GeospatialQueryController : ControllerBase
{
    private readonly IGeospatialEventRepository _repository;

    public GeospatialQueryController(IGeospatialEventRepository repository)
    {
        _repository = repository;
    }

    [HttpPost("search-by-radius")]
    public async Task<ActionResult<IReadOnlyList<GeospatialOperationEvent>>> SearchByRadius(
        [FromBody] SearchByRadiusRequestDTO request,
        CancellationToken cancellationToken)
    {
        var events = await _repository.SearchByPointAsync(
            request.Center.Latitude,
            request.Center.Longitude,
            request.RadiusMeters,
            cancellationToken);

        return Ok(events);
    }

    [HttpPost("search-by-polygon")]
    public async Task<ActionResult<IReadOnlyList<GeospatialOperationEvent>>> SearchByPolygon(
        [FromBody] SearchByPolygonRequestDTO request,
        CancellationToken cancellationToken)
    {
        if (request.Polygon.Count < 3)
        {
            return BadRequest("Polygon must have at least 3 points");
        }

        var polygon = request.Polygon.Select(p => (p.Latitude, p.Longitude)).ToList();
        var events = await _repository.SearchByPolygonAsync(polygon, cancellationToken);

        return Ok(events);
    }
}

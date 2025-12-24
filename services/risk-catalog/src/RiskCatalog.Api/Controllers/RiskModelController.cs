using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Application.DTO;

namespace RiskCatalog.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RiskModelController : ControllerBase
{
    [HttpGet("risk-matrix")]
    public ActionResult<RiskMatrixDTO> GetRiskMatrixForEventType(
        [FromQuery] string eventTypeCode,
        [FromQuery] string severityLevel,
        [FromQuery] int? version
    )
    {
        // Implementation to retrieve event types would go here.
        return Ok();
    }

    [HttpGet("idf-curves")]
    public ActionResult<IDFCurvesDTO> GetIDFCurvesForEventType(
        [FromQuery] string eventTypeCode,
        [FromQuery] int? returnPeriodYears
    )
    {
        // Implementation to retrieve event types would go here.
        return Ok();
    }

    [HttpGet("regional-risk-parameters")]
    public ActionResult<RegionalRiskParametersDTO> GetRegionalRiskParameters([FromQuery] Guid regionId)
    {
        // Implementation to retrieve event types would go here.
        return Ok();
    }
}
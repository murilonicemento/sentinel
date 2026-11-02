using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Api.Contracts;
using RiskCatalog.Application.DTO;

namespace RiskCatalog.Api.Controllers.V1;

public sealed record CreatedResourceDTO(string Key);

internal static class CatalogV1Responses
{
    public const string BasePath = "api/v1/risk-catalog";

    public static ResponseBaseDTO<T> Envelope<T>(int statusCode, T data) =>
        new() { StatusCode = statusCode, Success = true, Data = data };

    public static IActionResult Invalid(HttpContext httpContext, Dictionary<string, string[]> errors) =>
        new ProblemResult(ApiProblems.Create(httpContext, StatusCodes.Status400BadRequest, ApiProblems.ValidationError,
            "One or more fields are invalid.", errors));

    public static IActionResult NotFoundProblem(HttpContext httpContext, string code, string detail) =>
        new ProblemResult(ApiProblems.Create(httpContext, StatusCodes.Status404NotFound, code, detail));

    // The services report a failed write as false only when persistence did not complete.
    public static IActionResult PersistenceFailed(HttpContext httpContext) =>
        new ProblemResult(ApiProblems.Create(httpContext, StatusCodes.Status500InternalServerError,
            ApiProblems.InternalError, "The change could not be saved."));
}

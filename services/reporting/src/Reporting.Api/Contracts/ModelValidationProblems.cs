using Microsoft.AspNetCore.Mvc;

namespace Reporting.Api.Contracts;

public static class ModelValidationProblems
{
    /// <summary>Builds the /api/v1 response for an invalid model state, with errors grouped by field.</summary>
    public static IActionResult For(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => string.IsNullOrEmpty(entry.Key) ? "request" : entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The value is invalid or malformed."
                        : error.ErrorMessage)
                    .Distinct()
                    .ToArray());

        return new ProblemResult(ApiProblems.Create(context.HttpContext, StatusCodes.Status400BadRequest,
            ApiProblems.ValidationError, "One or more fields are invalid.", errors));
    }
}

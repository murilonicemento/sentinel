using Microsoft.AspNetCore.Mvc;

namespace AlertOrchestrator.Api.Contracts;

/// <summary>Writes Problem Details from a controller with the problem+json content type and its runtime type.</summary>
public sealed class ProblemResult : ObjectResult
{
    public ProblemResult(ProblemDetails problem) : base(problem)
    {
        StatusCode = problem.Status;
        DeclaredType = problem.GetType();
        ContentTypes.Add("application/problem+json");
    }
}

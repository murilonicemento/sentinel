namespace RiskEvaluation.Application.DTOs;

/// <summary>
/// The HTTP contracts (v1 and the frozen routes) publish coordinates as whole degrees. Evaluations keep the exact
/// position (RE-18), so the responses truncate it the same way the service always did.
/// </summary>
public static class ContractCoordinates
{
    public static int Degree(double coordinate) => (int)coordinate;
}

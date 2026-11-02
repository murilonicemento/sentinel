namespace RiskEvaluation.Domain.Entities;

public class RiskModel
{
    public string Version { get; private set; }
    public Dictionary<string, double> Parameters { get; private set; }
    public string Formula { get; private set; }

    public RiskModel(string version, Dictionary<string, double> parameters, string formula)
    {
        Version = version ?? throw new ArgumentNullException(nameof(version));
        Parameters = parameters ?? new Dictionary<string, double>();
        Formula = formula ?? throw new ArgumentNullException(nameof(formula));
    }
}
using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.EventTypes;

namespace RiskCatalog.Domain.IRepositories;

public interface ISeverityRepository
{
    public Task<Severity?> GetByLevel(SeverityLevelEnum severityLevel, CancellationToken cancellationToken = default);
}
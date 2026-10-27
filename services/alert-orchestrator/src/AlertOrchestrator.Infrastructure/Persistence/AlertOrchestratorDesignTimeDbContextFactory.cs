using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlertOrchestrator.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> to build the model; migrations are generated without a database and are never
/// applied at application startup.
/// </summary>
public sealed class AlertOrchestratorDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AlertOrchestratorDbContext>
{
    public AlertOrchestratorDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AlertOrchestratorDbContext>()
            .UseNpgsql("Host=localhost;Database=alert_orchestrator")
            .Options);
}

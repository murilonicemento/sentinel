using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RiskCatalog.Infrastructure.Persistence.DatabaseContext;

/// <summary>
/// Used only by <c>dotnet ef</c> to build the model; migrations are generated without a database and are never
/// applied at application startup.
/// </summary>
public sealed class RiskCatalogDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RiskCatalogDbContext>
{
    public RiskCatalogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<RiskCatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=risk_catalog")
            .Options);
}

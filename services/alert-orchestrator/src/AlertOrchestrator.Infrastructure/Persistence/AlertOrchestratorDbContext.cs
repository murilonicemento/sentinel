using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Infrastructure.Persistence.EntityConfigurations;
using Microsoft.EntityFrameworkCore;

namespace AlertOrchestrator.Infrastructure.Persistence;

public sealed class AlertOrchestratorDbContext : DbContext
{
    public DbSet<AlertWindow> AlertWindows => Set<AlertWindow>();

    public AlertOrchestratorDbContext(DbContextOptions<AlertOrchestratorDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AlertWindowConfiguration());
    }
}

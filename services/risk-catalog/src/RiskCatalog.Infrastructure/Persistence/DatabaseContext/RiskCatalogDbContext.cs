using Microsoft.EntityFrameworkCore;
using RiskCatalog.Domain.Entities;
using RiskCatalog.Domain.EventTypes;
using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Infrastructure.Persistence.DatabaseContext;

public class RiskCatalogDbContext : DbContext
{
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<EventType> EventTypes { get; set; }
    public virtual DbSet<Severity> Severities { get; set; }
    public virtual DbSet<SeverityCriterion> SeverityCriteria { get; set; }
    public virtual DbSet<IDFCurve> IDFCurves { get; set; }
    public virtual DbSet<RegionalParameter> RegionalParameters { get; set; }
    public virtual DbSet<RiskMatrix> RiskMatrices { get; set; }

    public RiskCatalogDbContext(DbContextOptions<RiskCatalogDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RiskCatalogDbContext).Assembly);
    }
}
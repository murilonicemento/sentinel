using Microsoft.EntityFrameworkCore;
using TenantsBilling.Infrastructure.Persistence.Entities;

namespace TenantsBilling.Infrastructure.Persistence;

public sealed class TenantsBillingDbContext : DbContext
{
    public TenantsBillingDbContext(DbContextOptions<TenantsBillingDbContext> options)
        : base(options)
    {
    }

    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<PlanEntity> Plans => Set<PlanEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantEntity>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Region).IsRequired().HasMaxLength(100);
            entity.Property(x => x.TimeZone).IsRequired().HasMaxLength(80);
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<PlanEntity>(entity =>
        {
            entity.ToTable("plans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.MaxEventsPerMonth).IsRequired();
            entity.Property(x => x.MaxAlertsPerMonth).IsRequired();
            entity.Property(x => x.MaxApiRequestsPerMonth).IsRequired();
            entity.Property(x => x.MaxChannelsPerMonth).IsRequired();
            entity.Property(x => x.SoftLimitPercentage).IsRequired();
            entity.Property(x => x.HardLimitPercentage).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
        });
    }
}

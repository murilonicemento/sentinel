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
    public DbSet<TenantUsageEntity> TenantUsage => Set<TenantUsageEntity>();
    public DbSet<RecordedUsageEventEntity> RecordedUsageEvents => Set<RecordedUsageEventEntity>();

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

        modelBuilder.Entity<TenantUsageEntity>(entity =>
        {
            entity.ToTable("tenant_usage");
            entity.HasKey(x => new { x.TenantId, x.Period });
            entity.Property(x => x.Period).IsRequired().HasMaxLength(7);
            entity.Property(x => x.QuotaStatus).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            // Concurrent consumers updating the same period conflict instead of losing increments.
            entity.Property(x => x.Version).IsRowVersion();
        });

        modelBuilder.Entity<RecordedUsageEventEntity>(entity =>
        {
            entity.ToTable("recorded_usage_events");
            entity.HasKey(x => x.SourceEventId);
            entity.Property(x => x.SourceEventId).HasMaxLength(200);
            entity.Property(x => x.Period).IsRequired().HasMaxLength(7);
            entity.Property(x => x.RecordedAt).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Period });
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

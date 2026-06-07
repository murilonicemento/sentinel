using ChannelsService.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace ChannelsService.Infrastructure.Persistence;

public sealed class ChannelsServiceDbContext : DbContext
{
    public DbSet<DeliveryAttempt> DeliveryAttempts { get; set; }

    public ChannelsServiceDbContext(DbContextOptions<ChannelsServiceDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeliveryAttempt>(entity =>
        {
            entity.ToTable("delivery_attempts");
            entity.HasKey(e => e.AttemptId);
            entity.Property(e => e.AttemptId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.EventId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Channel).IsRequired();
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.AttemptCount).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.Timestamp).IsRequired();
        });
    }
}
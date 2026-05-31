using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Models;
using ChannelsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChannelsService.Infrastructure.Repositories;

public sealed class PostgresDeliveryRepository : IDeliveryRepository
{
    private readonly ChannelsServiceDbContext _dbContext;

    public PostgresDeliveryRepository(ChannelsServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(DeliveryAttempt attempt)
    {
        _dbContext.DeliveryAttempts.Add(attempt);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId)
    {
        return await _dbContext.DeliveryAttempts
            .AsNoTracking()
            .Where(attempt => attempt.EventId == eventId)
            .ToListAsync();
    }
}

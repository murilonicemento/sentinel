namespace RiskCatalog.Application.Interfaces;

public interface ICacheService
{
    public Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default) where T : class;

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) where T : class;

    public Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default);

    public Task RemoveByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default);

    public Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default);
}

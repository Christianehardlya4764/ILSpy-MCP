using ILSpy.Mcp.Application.Configuration;
using Microsoft.Extensions.Options;

namespace ILSpy.Mcp.Application.Services;

/// <summary>
/// Interface for semaphore-based concurrency limiting of decompilation operations.
/// </summary>
public interface IConcurrencyLimiter
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Enforces MaxConcurrentOperations limit using a SemaphoreSlim.
/// Registered as singleton to share the semaphore across all scoped use cases.
/// </summary>
public sealed class ConcurrencyLimiter : IConcurrencyLimiter, IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public ConcurrencyLimiter(IOptions<ILSpyOptions> options)
    {
        _semaphore = new SemaphoreSlim(options.Value.MaxConcurrentOperations);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();
}

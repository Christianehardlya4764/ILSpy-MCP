using Microsoft.Extensions.Options;

namespace ILSpy.Mcp.Application.Services;

/// <summary>
/// Disposable wrapper that owns all CancellationTokenSource objects created for a timeout.
/// Ensures CTS disposal on every code path via the using pattern.
/// </summary>
public sealed class TimeoutToken : IDisposable
{
    private readonly CancellationTokenSource _timeoutCts;
    private readonly CancellationTokenSource _linkedCts;

    internal TimeoutToken(TimeSpan timeout, CancellationToken externalToken)
    {
        _timeoutCts = new CancellationTokenSource(timeout);
        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            externalToken,
            _timeoutCts.Token);
    }

    /// <summary>
    /// The effective cancellation token combining timeout and external cancellation.
    /// </summary>
    public CancellationToken Token => _linkedCts.Token;

    public void Dispose()
    {
        _linkedCts.Dispose();
        _timeoutCts.Dispose();
    }
}

/// <summary>
/// Service for managing timeouts and cancellation tokens.
/// </summary>
public interface ITimeoutService
{
    TimeoutToken CreateTimeoutToken(CancellationToken cancellationToken = default);
    TimeSpan GetDefaultTimeout();
}

public sealed class TimeoutService : ITimeoutService
{
    private readonly ILSpy.Mcp.Application.Configuration.ILSpyOptions _options;

    public TimeoutService(IOptions<ILSpy.Mcp.Application.Configuration.ILSpyOptions> options)
    {
        _options = options.Value;
    }

    public TimeoutToken CreateTimeoutToken(CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds);
        return new TimeoutToken(timeout, cancellationToken);
    }

    public TimeSpan GetDefaultTimeout() => TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds);
}

using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for finding all types that implement an interface or extend a base class.
/// </summary>
public sealed class FindImplementorsUseCase
{
    private readonly ICrossReferenceService _crossRef;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindImplementorsUseCase> _logger;

    public FindImplementorsUseCase(
        ICrossReferenceService crossRef,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindImplementorsUseCase> logger)
    {
        _crossRef = crossRef;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string typeName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);
            var type = TypeName.Create(typeName);

            _logger.LogInformation("Finding implementors of {TypeName} from {Assembly}", typeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _crossRef.FindImplementorsAsync(assembly, type, timeout.Token);

                return FormatResults(typeName, results);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for finding implementors of {TypeName}", typeName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for finding implementors of {TypeName}", typeName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding implementors of {TypeName}", typeName);
            throw;
        }
    }

    private static string FormatResults(string typeName, IReadOnlyList<ImplementorResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Implementors of {typeName}: {results.Count} found");
        sb.AppendLine();

        if (results.Count == 0)
        {
            sb.AppendLine("No implementors found in the assembly.");
            return sb.ToString();
        }

        var direct = results.Where(r => r.IsDirect).ToList();
        var indirect = results.Where(r => !r.IsDirect).ToList();

        if (direct.Count > 0)
        {
            sb.AppendLine("Direct:");
            foreach (var result in direct)
            {
                sb.AppendLine($"  [{result.Kind}] {result.TypeFullName}");
            }
        }

        if (indirect.Count > 0)
        {
            sb.AppendLine("Indirect:");
            foreach (var result in indirect)
            {
                sb.AppendLine($"  [{result.Kind}] {result.TypeFullName}");
            }
        }

        return sb.ToString();
    }
}

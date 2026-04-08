using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for finding all outward dependencies of a method or type.
/// </summary>
public sealed class FindDependenciesUseCase
{
    private readonly ICrossReferenceService _crossRef;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindDependenciesUseCase> _logger;

    public FindDependenciesUseCase(
        ICrossReferenceService crossRef,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindDependenciesUseCase> logger)
    {
        _crossRef = crossRef;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string typeName,
        string? methodName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);
            var type = TypeName.Create(typeName);

            var target = methodName != null ? $"{typeName}.{methodName}" : typeName;
            _logger.LogInformation("Finding dependencies of {Target} from {Assembly}", target, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _crossRef.FindDependenciesAsync(assembly, type, methodName, timeout.Token);

                return FormatResults(target, results);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for finding dependencies of {TypeName}", typeName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for finding dependencies of {TypeName}", typeName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding dependencies of {TypeName}", typeName);
            throw;
        }
    }

    private static string FormatResults(string target, IReadOnlyList<DependencyResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Dependencies of {target}: {results.Count} found");
        sb.AppendLine();

        if (results.Count == 0)
        {
            sb.AppendLine("No outward dependencies found.");
            return sb.ToString();
        }

        var grouped = results.GroupBy(r => r.Kind).OrderBy(g => g.Key);
        foreach (var group in grouped)
        {
            sb.AppendLine($"{group.Key}:");
            foreach (var dep in group)
            {
                sb.AppendLine($"  {dep.TargetMember}");
            }
        }

        return sb.ToString();
    }
}

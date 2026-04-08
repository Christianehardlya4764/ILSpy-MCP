using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for finding all instantiation sites (newobj) of a given type.
/// </summary>
public sealed class FindInstantiationsUseCase
{
    private readonly ICrossReferenceService _crossRef;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindInstantiationsUseCase> _logger;

    public FindInstantiationsUseCase(
        ICrossReferenceService crossRef,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindInstantiationsUseCase> logger)
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

            _logger.LogInformation("Finding instantiations of {TypeName} from {Assembly}", typeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _crossRef.FindInstantiationsAsync(assembly, type, timeout.Token);

                return FormatResults(typeName, results);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for finding instantiations of {TypeName}", typeName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for finding instantiations of {TypeName}", typeName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding instantiations of {TypeName}", typeName);
            throw;
        }
    }

    private static string FormatResults(string typeName, IReadOnlyList<InstantiationResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Instantiations of {typeName}: {results.Count} found");
        sb.AppendLine();

        if (results.Count == 0)
        {
            sb.AppendLine("No instantiation sites found in the assembly.");
            return sb.ToString();
        }

        foreach (var result in results)
        {
            sb.AppendLine($"  {result.DeclaringType}.{result.MethodName} (IL_{result.ILOffset:X4})");
        }

        return sb.ToString();
    }
}

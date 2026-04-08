using System.Text;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for finding compiler-generated types with parent context.
/// </summary>
public sealed class FindCompilerGeneratedTypesUseCase
{
    private readonly IAssemblyInspectionService _inspection;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindCompilerGeneratedTypesUseCase> _logger;

    public FindCompilerGeneratedTypesUseCase(
        IAssemblyInspectionService inspection,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindCompilerGeneratedTypesUseCase> logger)
    {
        _inspection = inspection;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);

            _logger.LogInformation("Finding compiler-generated types in {Assembly}", assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var types = await _inspection.FindCompilerGeneratedTypesAsync(assembly, timeout.Token);
                return FormatCompilerGeneratedTypes(types);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for assembly {Assembly}", assemblyPath);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for assembly {Assembly}", assemblyPath);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding compiler-generated types in {Assembly}", assemblyPath);
            throw;
        }
    }

    private static string FormatCompilerGeneratedTypes(IReadOnlyList<CompilerGeneratedTypeInfo> types)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Compiler-Generated Types ({types.Count})");
        sb.AppendLine();

        if (types.Count == 0)
        {
            sb.AppendLine("No compiler-generated types found.");
            return sb.ToString();
        }

        foreach (var type in types)
        {
            sb.AppendLine($"- {type.FullName}");
            sb.AppendLine($"  Kind: {type.GeneratedKind}");
            if (type.ParentType != null)
                sb.AppendLine($"  Parent Type: {type.ParentType}");
            if (type.ParentMethod != null)
                sb.AppendLine($"  Parent Method: {type.ParentMethod}");
        }

        return sb.ToString();
    }
}

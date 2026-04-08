using System.Text;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for retrieving custom attributes declared on an assembly.
/// </summary>
public sealed class GetAssemblyAttributesUseCase
{
    private readonly IAssemblyInspectionService _inspection;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<GetAssemblyAttributesUseCase> _logger;

    public GetAssemblyAttributesUseCase(
        IAssemblyInspectionService inspection,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<GetAssemblyAttributesUseCase> logger)
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

            _logger.LogInformation("Getting assembly attributes for {Assembly}", assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var attributes = await _inspection.GetAssemblyAttributesAsync(assembly, timeout.Token);
                return FormatAttributes("Assembly", attributes);
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
            _logger.LogError(ex, "Unexpected error getting assembly attributes for {Assembly}", assemblyPath);
            throw;
        }
    }

    internal static string FormatAttributes(string scope, IReadOnlyList<AttributeInfo> attributes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {scope} Attributes ({attributes.Count})");
        sb.AppendLine();

        if (attributes.Count == 0)
        {
            sb.AppendLine("No custom attributes found.");
            return sb.ToString();
        }

        foreach (var attr in attributes)
        {
            sb.AppendLine($"[{attr.AttributeType}]");
            if (attr.ConstructorArguments.Count > 0)
            {
                sb.AppendLine($"  Constructor args: {string.Join(", ", attr.ConstructorArguments)}");
            }
            if (attr.NamedArguments.Count > 0)
            {
                foreach (var kvp in attr.NamedArguments)
                {
                    sb.AppendLine($"  {kvp.Key} = {kvp.Value}");
                }
            }
        }

        return sb.ToString();
    }
}

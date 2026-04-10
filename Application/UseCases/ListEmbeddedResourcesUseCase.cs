using System.Text;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for listing embedded resources in an assembly.
/// </summary>
public sealed class ListEmbeddedResourcesUseCase
{
    private readonly IAssemblyInspectionService _inspection;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<ListEmbeddedResourcesUseCase> _logger;

    public ListEmbeddedResourcesUseCase(
        IAssemblyInspectionService inspection,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<ListEmbeddedResourcesUseCase> logger)
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

            _logger.LogInformation("Listing embedded resources for {Assembly}", assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var resources = await _inspection.ListEmbeddedResourcesAsync(assembly, timeout.Token);
                return FormatResources(resources);
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
            _logger.LogError(ex, "Unexpected error listing embedded resources for {Assembly}", assemblyPath);
            throw;
        }
    }

    private static string FormatResources(IReadOnlyList<ResourceInfo> resources)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Embedded Resources ({resources.Count})");
        sb.AppendLine();

        if (resources.Count == 0)
        {
            sb.AppendLine("No embedded resources found.");
            return sb.ToString();
        }

        foreach (var resource in resources)
        {
            sb.AppendLine($"- {resource.Name}");
            sb.AppendLine($"  Type: {resource.ResourceType}");
            sb.AppendLine($"  Size: {resource.Size} bytes");
            sb.AppendLine($"  Visibility: {(resource.IsPublic ? "Public" : "Private")}");
        }

        return sb.ToString();
    }
}

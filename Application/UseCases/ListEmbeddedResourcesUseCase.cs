using System.Text;
using ILSpy.Mcp.Application.Pagination;
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
        int maxResults = 100,
        int offset = 0,
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
                return FormatResources(resources, maxResults, offset);
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

    private static string FormatResources(IReadOnlyList<ResourceInfo> resources, int maxResults, int offset)
    {
        var sb = new StringBuilder();
        var total = resources.Count;
        var page = resources.Skip(offset).Take(maxResults).ToList();
        var returned = page.Count;

        if (total == 0)
        {
            sb.AppendLine("# Embedded Resources (0)");
            sb.AppendLine();
            sb.AppendLine("No embedded resources found.");
            PaginationEnvelope.AppendFooter(sb, 0, 0, offset);
            return sb.ToString();
        }

        var rangeStart = offset + 1;
        var rangeEnd = offset + returned;
        sb.AppendLine($"# Embedded Resources ({total}) (showing {rangeStart}-{rangeEnd})");
        sb.AppendLine();

        foreach (var resource in page)
        {
            sb.AppendLine($"- {resource.Name}");
            sb.AppendLine($"  Type: {resource.ResourceType}");
            sb.AppendLine($"  Size: {resource.Size} bytes");
            sb.AppendLine($"  Visibility: {(resource.IsPublic ? "Public" : "Private")}");
        }

        PaginationEnvelope.AppendFooter(sb, total, returned, offset);
        return sb.ToString();
    }
}

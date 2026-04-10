using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

[McpServerToolType]
public sealed class SearchMembersByNameTool
{
    private readonly SearchMembersByNameUseCase _useCase;
    private readonly ILogger<SearchMembersByNameTool> _logger;

    public SearchMembersByNameTool(
        SearchMembersByNameUseCase useCase,
        ILogger<SearchMembersByNameTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "search_members_by_name")]
    [Description("Use this when you know what operation you want to perform (like 'Parse', 'Convert', 'Serialize') but don't know which type contains that method. Searches across all types in the assembly to find matching methods, properties, or fields. Helps discover API entry points.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Name or partial name to search for (case-insensitive)")] string searchTerm,
        [Description("Optional: Filter by member kind (method, property, field, event)")] string? memberKind = null,
        [Description("Maximum number of results to return (default: 100)")] int maxResults = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Phase 11 pagination contract: hard ceiling + positive minimum.
            if (maxResults > 500)
            {
                throw new McpToolException("INVALID_PARAMETER",
                    "maxResults cannot exceed 500. Use offset to paginate.");
            }
            if (maxResults <= 0)
            {
                throw new McpToolException("INVALID_PARAMETER",
                    "maxResults must be >= 1.");
            }

            return await _useCase.ExecuteAsync(assemblyPath, searchTerm, memberKind, maxResults, offset, cancellationToken);
        }
        catch (McpToolException)
        {
            throw;  // Rethrow our own INVALID_PARAMETER without mapping it again
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in search_members_by_name tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in search_members_by_name tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in search_members_by_name tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while searching members.");
        }
    }
}

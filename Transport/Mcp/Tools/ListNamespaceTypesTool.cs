using System.ComponentModel;
using ILSpy.Mcp.Application.UseCases;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ILSpy.Mcp.Transport.Mcp.Tools;

/// <summary>
/// MCP tool handler for listing all types in a namespace with summary information.
/// </summary>
[McpServerToolType]
public sealed class ListNamespaceTypesTool
{
    private readonly ListNamespaceTypesUseCase _useCase;
    private readonly ILogger<ListNamespaceTypesTool> _logger;

    public ListNamespaceTypesTool(
        ListNamespaceTypesUseCase useCase,
        ILogger<ListNamespaceTypesTool> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [McpServerTool(Name = "list_namespace_types")]
    [Description("Lists all types in a namespace with full signatures, member counts, and public method signatures. Returns a summary -- use decompile_type to get full source for individual types.")]
    public async Task<string> ExecuteAsync(
        [Description("Path to the .NET assembly file")] string assemblyPath,
        [Description("Full namespace name (e.g., 'System.Collections.Generic')")] string namespaceName,
        [Description("Maximum number of results to return (default: 100)")] int maxResults = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Phase 9 pagination contract: hard ceiling + positive minimum.
            // Validation at the Transport boundary (not the use case) to preserve
            // the layering Phase 8 DEBT-02 restored.
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

            return await _useCase.ExecuteAsync(assemblyPath, namespaceName, maxResults, offset, cancellationToken);
        }
        catch (McpToolException)
        {
            throw;  // Rethrow our own INVALID_PARAMETER without mapping it again
        }
        catch (NamespaceNotFoundException ex)
        {
            _logger.LogWarning("Namespace not found: {Namespace} in {Assembly}", ex.NamespaceName, ex.AssemblyPath);
            throw new McpToolException("NAMESPACE_NOT_FOUND", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (AssemblyLoadException ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", ex.AssemblyPath);
            throw new McpToolException("ASSEMBLY_LOAD_FAILED", ErrorSanitizer.SanitizePath(ex.Message));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("Timeout in list_namespace_types tool: {Message}", ex.Message);
            throw new McpToolException("TIMEOUT", "The operation timed out. The assembly may be too large or the operation took too long.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled in list_namespace_types tool");
            throw new McpToolException("CANCELLED", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in list_namespace_types tool");
            throw new McpToolException("INTERNAL_ERROR", "An unexpected error occurred while listing namespace types.");
        }
    }
}

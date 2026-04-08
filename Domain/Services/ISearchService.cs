using ILSpy.Mcp.Domain.Models;

namespace ILSpy.Mcp.Domain.Services;

/// <summary>
/// Port for IL bytecode search operations. Abstracts scanning for string literals
/// and numeric constants across assembly method bodies.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Searches for string literals matching a regex pattern by scanning ldstr IL instructions.
    /// </summary>
    Task<SearchResults<StringSearchResult>> SearchStringsAsync(
        AssemblyPath assemblyPath,
        string regexPattern,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for numeric integer constants by exact value, scanning ldc.i4 and ldc.i8 IL instructions.
    /// </summary>
    Task<SearchResults<ConstantSearchResult>> SearchConstantsAsync(
        AssemblyPath assemblyPath,
        long value,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);
}

using ILSpy.Mcp.Domain.Models;

namespace ILSpy.Mcp.Domain.Services;

/// <summary>
/// Port for cross-reference analysis operations. Abstracts IL scanning for
/// usage tracking, implementor discovery, dependency analysis, and instantiation finding.
/// </summary>
public interface ICrossReferenceService
{
    /// <summary>
    /// Finds all sites where the specified member (method, field, or property) is used
    /// across the assembly. Scans IL for call/callvirt/ldfld/stfld opcodes.
    /// </summary>
    Task<IReadOnlyList<UsageResult>> FindUsagesAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string memberName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all types that implement the given interface or extend the given base class.
    /// </summary>
    Task<IReadOnlyList<ImplementorResult>> FindImplementorsAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all outward dependencies of a method or type — what it calls and references.
    /// </summary>
    Task<IReadOnlyList<DependencyResult>> FindDependenciesAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string? methodName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all sites where the given type is instantiated (newobj instruction).
    /// </summary>
    Task<IReadOnlyList<InstantiationResult>> FindInstantiationsAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        CancellationToken cancellationToken = default);
}

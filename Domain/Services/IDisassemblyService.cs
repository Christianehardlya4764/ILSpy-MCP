using ILSpy.Mcp.Domain.Models;

namespace ILSpy.Mcp.Domain.Services;

/// <summary>
/// Port for IL disassembly operations. Abstracts the disassembler implementation.
/// </summary>
public interface IDisassemblyService
{
    /// <summary>
    /// Disassembles a type showing structure and method signatures only (no IL bodies).
    /// Per D-02: headers-only view for type-level output.
    /// </summary>
    /// <param name="resolveDeep">When true, expand full type signatures for parameters and generics inline. Default false preserves abbreviated form.</param>
    Task<string> DisassembleTypeAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        bool showTokens = false,
        bool resolveDeep = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disassembles a specific method with complete IL body.
    /// Per D-03: full IL with .maxstack, IL_xxxx labels, resolved names.
    /// </summary>
    /// <param name="resolveDeep">When true, expand full type signatures for parameters and generics inline. Default false preserves abbreviated form.</param>
    Task<string> DisassembleMethodAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string methodName,
        bool showBytes = false,
        bool showTokens = false,
        bool resolveDeep = false,
        CancellationToken cancellationToken = default);
}

namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Result of a cross-reference usage search — where a member is called/accessed.
/// </summary>
public sealed record UsageResult
{
    /// <summary>Full name of the type containing the usage site.</summary>
    public required string DeclaringType { get; init; }

    /// <summary>Name of the method containing the usage.</summary>
    public required string MethodName { get; init; }

    /// <summary>IL offset where the usage occurs.</summary>
    public int ILOffset { get; init; }

    /// <summary>Kind of usage (Call, FieldRead, FieldWrite, PropertyGet, PropertySet).</summary>
    public required UsageKind Kind { get; init; }

    /// <summary>Full signature of the method containing the usage, for disambiguation.</summary>
    public string? MethodSignature { get; init; }
}

/// <summary>
/// Describes how a member is used at a reference site.
/// </summary>
public enum UsageKind
{
    Call,
    FieldRead,
    FieldWrite,
    PropertyGet,
    PropertySet,
    VirtualCall
}

/// <summary>
/// Result of finding types that implement an interface or extend a base class.
/// </summary>
public sealed record ImplementorResult
{
    /// <summary>Full name of the implementing/derived type.</summary>
    public required string TypeFullName { get; init; }

    /// <summary>Short name of the implementing/derived type.</summary>
    public required string TypeShortName { get; init; }

    /// <summary>Whether this is a direct or indirect implementation/derivation.</summary>
    public bool IsDirect { get; init; }

    /// <summary>The kind of the implementing type (class, struct, etc.).</summary>
    public TypeKind Kind { get; init; }
}

/// <summary>
/// Result of finding outward dependencies of a method or type.
/// </summary>
public sealed record DependencyResult
{
    /// <summary>Full name of the referenced member (type.method or type.field).</summary>
    public required string TargetMember { get; init; }

    /// <summary>Full name of the type that owns the referenced member.</summary>
    public required string TargetType { get; init; }

    /// <summary>Kind of dependency.</summary>
    public required DependencyKind Kind { get; init; }

    /// <summary>
    /// Full name of the assembly that terminally defines the referenced member.
    /// When deep type-forward resolution succeeds, this is the terminal assembly
    /// (e.g. System.Runtime type-forwards through to System.Private.CoreLib →
    /// DefiningAssembly = "System.Private.CoreLib"). When the referenced assembly
    /// cannot be loaded from the analyzed assembly's directory, this falls back
    /// to the immediate AssemblyReference name from metadata and ResolutionNote
    /// is populated.
    /// </summary>
    public required string DefiningAssembly { get; init; }

    /// <summary>
    /// Populated ONLY on fail-soft degradation — e.g. "unresolved: referenced
    /// assembly not present in analyzed assembly directory". Null on the happy
    /// path. Agents can treat a non-null value as "DefiningAssembly is a
    /// best-effort guess, not the confirmed terminal assembly".
    /// </summary>
    public string? ResolutionNote { get; init; }
}

/// <summary>
/// Describes the nature of an outward dependency.
/// </summary>
public enum DependencyKind
{
    MethodCall,
    FieldAccess,
    TypeReference,
    VirtualCall
}

/// <summary>
/// Result of finding instantiation sites for a type.
/// </summary>
public sealed record InstantiationResult
{
    /// <summary>Full name of the type containing the instantiation.</summary>
    public required string DeclaringType { get; init; }

    /// <summary>Name of the method containing the newobj instruction.</summary>
    public required string MethodName { get; init; }

    /// <summary>IL offset where the newobj occurs.</summary>
    public int ILOffset { get; init; }

    /// <summary>Full signature of the method containing the instantiation.</summary>
    public string? MethodSignature { get; init; }
}

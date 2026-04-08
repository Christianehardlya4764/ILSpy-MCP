namespace ILSpy.Mcp.Domain.Models;

/// <summary>
/// Summary of all types in a namespace with member counts and public method signatures.
/// </summary>
public sealed record NamespaceTypeSummary
{
    public required string Namespace { get; init; }
    public required IReadOnlyList<TypeSummaryEntry> Types { get; init; }
    public int TotalTypeCount { get; init; }
}

/// <summary>
/// Summary entry for a single type within a namespace listing.
/// </summary>
public sealed record TypeSummaryEntry
{
    public required string FullName { get; init; }
    public required string ShortName { get; init; }
    public TypeKind Kind { get; init; }
    public string? BaseType { get; init; }
    public int MethodCount { get; init; }
    public int PropertyCount { get; init; }
    public int FieldCount { get; init; }
    public IReadOnlyList<string> PublicMethodSignatures { get; init; } = Array.Empty<string>();
    public IReadOnlyList<TypeSummaryEntry>? NestedTypes { get; init; }
}

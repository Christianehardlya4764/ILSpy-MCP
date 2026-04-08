namespace ILSpy.Mcp.Domain.Errors;

public sealed class NamespaceNotFoundException : DomainException
{
    public string NamespaceName { get; }
    public string AssemblyPath { get; }

    public NamespaceNotFoundException(string namespaceName, string assemblyPath)
        : base("NAMESPACE_NOT_FOUND", $"Namespace '{namespaceName}' not found in assembly '{assemblyPath}'. Use list_namespaces to see available namespaces.")
    {
        NamespaceName = namespaceName;
        AssemblyPath = assemblyPath;
    }
}

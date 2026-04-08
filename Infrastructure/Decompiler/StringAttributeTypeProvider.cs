using System.Reflection.Metadata;

namespace ILSpy.Mcp.Infrastructure.Decompiler;

/// <summary>
/// Implements ICustomAttributeTypeProvider for decoding custom attribute constructor arguments
/// into string representations. Used by DecodeValue to parse ECMA-335 attribute blobs.
/// </summary>
internal sealed class StringAttributeTypeProvider : ICustomAttributeTypeProvider<string>
{
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        var ns = reader.GetString(typeDef.Namespace);
        var name = reader.GetString(typeDef.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var typeRef = reader.GetTypeReference(handle);
        var ns = reader.GetString(typeRef.Namespace);
        var name = reader.GetString(typeRef.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public string GetTypeFromSerializedName(string name) => name;

    public string GetSZArrayType(string elementType) => $"{elementType}[]";

    public string GetSystemType() => "System.Type";

    public bool IsSystemType(string type) => type == "System.Type" || type == "Type";

    public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32; // safe default for display
}

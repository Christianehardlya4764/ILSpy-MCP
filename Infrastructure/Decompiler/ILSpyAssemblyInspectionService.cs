using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using AssemblyPath = ILSpy.Mcp.Domain.Models.AssemblyPath;
using TypeName = ILSpy.Mcp.Domain.Models.TypeName;

namespace ILSpy.Mcp.Infrastructure.Decompiler;

/// <summary>
/// Adapter that implements IAssemblyInspectionService using System.Reflection.Metadata APIs
/// and ICSharpCode.Decompiler type system for metadata reading, attribute inspection,
/// resource extraction, and compiler-generated type discovery.
/// </summary>
public sealed class ILSpyAssemblyInspectionService : IAssemblyInspectionService
{
    private readonly ILogger<ILSpyAssemblyInspectionService> _logger;
    private readonly DecompilerSettings _settings;

    public ILSpyAssemblyInspectionService(ILogger<ILSpyAssemblyInspectionService> logger)
    {
        _logger = logger;
        _settings = new DecompilerSettings
        {
            ThrowOnAssemblyResolveErrors = false,
            ShowXmlDocumentation = false
        };
    }

    public async Task<AssemblyMetadata> GetAssemblyMetadataAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;

                // Access PEHeaders: open a PEFile directly since MetadataFile doesn't expose PEReader
                using var peFile = new PEFile(assemblyPath.Value);
                var peHeaders = peFile.Reader.PEHeaders;

                var assemblyDef = reader.GetAssemblyDefinition();
                var name = reader.GetString(assemblyDef.Name);
                var version = assemblyDef.Version.ToString();
                var culture = reader.GetString(assemblyDef.Culture);

                // PE kind from COFF header machine and COR flags
                var peKind = GetPEKind(peHeaders);

                // Target framework from assembly attributes
                string? targetFramework = null;
                foreach (var attrHandle in assemblyDef.GetCustomAttributes())
                {
                    var attr = reader.GetCustomAttribute(attrHandle);
                    var attrTypeName = GetAttributeTypeName(reader, attr);
                    if (attrTypeName == "System.Runtime.Versioning.TargetFrameworkAttribute")
                    {
                        try
                        {
                            var decoded = attr.DecodeValue(new StringAttributeTypeProvider());
                            if (decoded.FixedArguments.Length > 0)
                                targetFramework = decoded.FixedArguments[0].Value?.ToString();
                        }
                        catch
                        {
                            // Skip if decode fails
                        }
                        break;
                    }
                }

                // Runtime version from COR header
                string? runtimeVersion = null;
                if (peHeaders.CorHeader != null)
                {
                    runtimeVersion = $"{peHeaders.CorHeader.MajorRuntimeVersion}.{peHeaders.CorHeader.MinorRuntimeVersion}";
                }

                // Entry point
                string? entryPoint = null;
                if (peHeaders.CorHeader != null && peHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress != 0)
                {
                    try
                    {
                        var entryPointToken = peHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress;
                        var entryPointHandle = MetadataTokens.EntityHandle(entryPointToken);
                        if (entryPointHandle.Kind == HandleKind.MethodDefinition)
                        {
                            var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)entryPointHandle);
                            var methodName = reader.GetString(methodDef.Name);
                            var declaringType = methodDef.GetDeclaringType();
                            var typeDef = reader.GetTypeDefinition(declaringType);
                            var typeNs = reader.GetString(typeDef.Namespace);
                            var typeName = reader.GetString(typeDef.Name);
                            var fullTypeName = string.IsNullOrEmpty(typeNs) ? typeName : $"{typeNs}.{typeName}";
                            entryPoint = $"{fullTypeName}.{methodName}";
                        }
                    }
                    catch
                    {
                        // Skip if entry point resolution fails
                    }
                }

                // Strong name / public key token
                string? strongName = null;
                string? publicKeyToken = null;
                if (!assemblyDef.PublicKey.IsNil)
                {
                    var publicKeyBytes = reader.GetBlobBytes(assemblyDef.PublicKey);
                    strongName = BitConverter.ToString(publicKeyBytes).Replace("-", "").ToLowerInvariant();

                    // Compute public key token (last 8 bytes of SHA-1 hash, reversed)
                    using var sha1 = System.Security.Cryptography.SHA1.Create();
                    var hash = sha1.ComputeHash(publicKeyBytes);
                    var tokenBytes = new byte[8];
                    Array.Copy(hash, hash.Length - 8, tokenBytes, 0, 8);
                    Array.Reverse(tokenBytes);
                    publicKeyToken = BitConverter.ToString(tokenBytes).Replace("-", "").ToLowerInvariant();
                }

                // Assembly references
                var references = new List<AssemblyReferenceInfo>();
                foreach (var refHandle in reader.AssemblyReferences)
                {
                    var asmRef = reader.GetAssemblyReference(refHandle);
                    string? refPublicKeyToken = null;
                    if (!asmRef.PublicKeyOrToken.IsNil)
                    {
                        var tokenBytes = reader.GetBlobBytes(asmRef.PublicKeyOrToken);
                        refPublicKeyToken = BitConverter.ToString(tokenBytes).Replace("-", "").ToLowerInvariant();
                    }

                    references.Add(new AssemblyReferenceInfo
                    {
                        Name = reader.GetString(asmRef.Name),
                        Version = asmRef.Version.ToString(),
                        Culture = NullIfEmpty(reader.GetString(asmRef.Culture)),
                        PublicKeyToken = refPublicKeyToken
                    });
                }

                return new AssemblyMetadata
                {
                    Name = name,
                    Version = version,
                    TargetFramework = targetFramework,
                    RuntimeVersion = runtimeVersion,
                    PEKind = peKind,
                    StrongName = strongName,
                    EntryPoint = entryPoint,
                    Culture = NullIfEmpty(culture),
                    PublicKeyToken = publicKeyToken,
                    References = references
                };
            }
            catch (MetadataFileNotSupportedException ex)
            {
                _logger.LogWarning("Assembly is not a .NET assembly: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value,
                    $"'{assemblyPath.FileName}' is not a .NET assembly. The file does not contain managed (.NET) metadata and cannot be decompiled.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get assembly metadata for {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeInfo>> GetAssemblyAttributesAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var reader = decompiler.TypeSystem.MainModule.MetadataFile.Metadata;
                var assemblyDef = reader.GetAssemblyDefinition();

                return DecodeAttributes(reader, assemblyDef.GetCustomAttributes());
            }
            catch (MetadataFileNotSupportedException ex)
            {
                _logger.LogWarning("Assembly is not a .NET assembly: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value,
                    $"'{assemblyPath.FileName}' is not a .NET assembly. The file does not contain managed (.NET) metadata and cannot be decompiled.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get assembly attributes for {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeInfo>> GetTypeAttributesAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var type = decompiler.TypeSystem.MainModule.GetTypeDefinition(
                    new FullTypeName(typeName.FullName));

                if (type == null)
                    throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

                var reader = decompiler.TypeSystem.MainModule.MetadataFile.Metadata;
                var typeDefHandle = (TypeDefinitionHandle)type.MetadataToken;
                var typeDef = reader.GetTypeDefinition(typeDefHandle);

                return DecodeAttributes(reader, typeDef.GetCustomAttributes());
            }
            catch (TypeNotFoundException) { throw; }
            catch (MetadataFileNotSupportedException ex)
            {
                _logger.LogWarning("Assembly is not a .NET assembly: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value,
                    $"'{assemblyPath.FileName}' is not a .NET assembly. The file does not contain managed (.NET) metadata and cannot be decompiled.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get type attributes for {TypeName} from {Assembly}",
                    typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeInfo>> GetMemberAttributesAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string memberName,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var type = decompiler.TypeSystem.MainModule.GetTypeDefinition(
                    new FullTypeName(typeName.FullName));

                if (type == null)
                    throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

                var reader = decompiler.TypeSystem.MainModule.MetadataFile.Metadata;
                var results = new List<AttributeInfo>();
                bool foundMember = false;

                // Search methods (including constructors)
                foreach (var method in type.Methods.Where(m => m.Name == memberName))
                {
                    foundMember = true;
                    var methodHandle = (MethodDefinitionHandle)method.MetadataToken;
                    var methodDef = reader.GetMethodDefinition(methodHandle);
                    results.AddRange(DecodeAttributes(reader, methodDef.GetCustomAttributes()));
                }

                // Search fields
                foreach (var field in type.Fields.Where(f => f.Name == memberName))
                {
                    foundMember = true;
                    var fieldHandle = (FieldDefinitionHandle)field.MetadataToken;
                    var fieldDef = reader.GetFieldDefinition(fieldHandle);
                    results.AddRange(DecodeAttributes(reader, fieldDef.GetCustomAttributes()));
                }

                // Search properties
                foreach (var prop in type.Properties.Where(p => p.Name == memberName))
                {
                    foundMember = true;
                    var propHandle = (PropertyDefinitionHandle)prop.MetadataToken;
                    var propDef = reader.GetPropertyDefinition(propHandle);
                    results.AddRange(DecodeAttributes(reader, propDef.GetCustomAttributes()));

                    // Also check getter/setter method attributes
                    if (prop.Getter != null)
                    {
                        var getterHandle = (MethodDefinitionHandle)prop.Getter.MetadataToken;
                        var getterDef = reader.GetMethodDefinition(getterHandle);
                        results.AddRange(DecodeAttributes(reader, getterDef.GetCustomAttributes()));
                    }
                    if (prop.Setter != null)
                    {
                        var setterHandle = (MethodDefinitionHandle)prop.Setter.MetadataToken;
                        var setterDef = reader.GetMethodDefinition(setterHandle);
                        results.AddRange(DecodeAttributes(reader, setterDef.GetCustomAttributes()));
                    }
                }

                // Search events
                foreach (var evt in type.Events.Where(e => e.Name == memberName))
                {
                    foundMember = true;
                    var evtHandle = (EventDefinitionHandle)evt.MetadataToken;
                    var evtDef = reader.GetEventDefinition(evtHandle);
                    results.AddRange(DecodeAttributes(reader, evtDef.GetCustomAttributes()));
                }

                if (!foundMember)
                    throw new MethodNotFoundException(memberName, typeName.FullName);

                return (IReadOnlyList<AttributeInfo>)results;
            }
            catch (TypeNotFoundException) { throw; }
            catch (MethodNotFoundException) { throw; }
            catch (MetadataFileNotSupportedException ex)
            {
                _logger.LogWarning("Assembly is not a .NET assembly: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value,
                    $"'{assemblyPath.FileName}' is not a .NET assembly. The file does not contain managed (.NET) metadata and cannot be decompiled.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get member attributes for {MemberName} on {TypeName} from {Assembly}",
                    memberName, typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ResourceInfo>> ListEmbeddedResourcesAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;
                var results = new List<ResourceInfo>();

                foreach (var resourceHandle in reader.ManifestResources)
                {
                    var resource = reader.GetManifestResource(resourceHandle);
                    var name = reader.GetString(resource.Name);
                    var isEmbedded = resource.Implementation.IsNil; // Nil means embedded
                    var isPublic = (resource.Attributes & ManifestResourceAttributes.VisibilityMask)
                                   == ManifestResourceAttributes.Public;

                    long size = 0;
                    if (isEmbedded)
                    {
                        try
                        {
                            // Try to get size by opening the resource stream
                            var res = metadataFile.Resources.FirstOrDefault(r => r.Name == name);
                            if (res != null)
                            {
                                using var stream = res.TryOpenStream();
                                if (stream != null)
                                    size = stream.Length;
                            }
                        }
                        catch
                        {
                            // Size unknown if stream fails
                        }
                    }

                    results.Add(new ResourceInfo
                    {
                        Name = name,
                        Size = size,
                        ResourceType = isEmbedded ? "Embedded" : "Linked",
                        IsPublic = isPublic
                    });
                }

                return (IReadOnlyList<ResourceInfo>)results;
            }
            catch (MetadataFileNotSupportedException ex)
            {
                _logger.LogWarning("Assembly is not a .NET assembly: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value,
                    $"'{assemblyPath.FileName}' is not a .NET assembly. The file does not contain managed (.NET) metadata and cannot be decompiled.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list embedded resources for {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<ResourceContent> ExtractResourceAsync(
        AssemblyPath assemblyPath,
        string resourceName,
        int? offset = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;

                var resource = metadataFile.Resources.FirstOrDefault(r => r.Name == resourceName);
                if (resource == null)
                    throw new InvalidOperationException($"Resource '{resourceName}' not found in assembly.");

                using var stream = resource.TryOpenStream();
                if (stream == null)
                    throw new InvalidOperationException($"Cannot open resource stream for '{resourceName}'.");

                // Read all bytes
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var allBytes = ms.ToArray();
                var totalSize = allBytes.Length;

                // Apply offset/limit on raw bytes
                var actualOffset = offset ?? 0;
                var actualLimit = limit ?? totalSize;
                if (actualOffset > totalSize) actualOffset = totalSize;
                var remaining = totalSize - actualOffset;
                var sliceLength = Math.Min(actualLimit, remaining);

                var slice = new byte[sliceLength];
                if (sliceLength > 0)
                    Array.Copy(allBytes, actualOffset, slice, 0, sliceLength);

                // Determine content type: try UTF-8 decode
                var contentType = IsTextContent(allBytes) ? "text" : "binary";
                string content;
                if (contentType == "text")
                {
                    content = Encoding.UTF8.GetString(slice);
                }
                else
                {
                    content = Convert.ToBase64String(slice);
                }

                return new ResourceContent
                {
                    Name = resourceName,
                    ContentType = contentType,
                    Content = content,
                    TotalSize = totalSize,
                    Offset = offset.HasValue ? actualOffset : null,
                    Length = limit.HasValue ? sliceLength : null
                };
            }
            catch (InvalidOperationException) { throw; }
            catch (MetadataFileNotSupportedException ex)
            {
                _logger.LogWarning("Assembly is not a .NET assembly: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value,
                    $"'{assemblyPath.FileName}' is not a .NET assembly. The file does not contain managed (.NET) metadata and cannot be decompiled.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract resource '{ResourceName}' from {Assembly}",
                    resourceName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<CompilerGeneratedTypeInfo>> FindCompilerGeneratedTypesAsync(
        AssemblyPath assemblyPath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var mainModule = decompiler.TypeSystem.MainModule;
                var reader = mainModule.MetadataFile.Metadata;
                var results = new List<CompilerGeneratedTypeInfo>();

                foreach (var type in mainModule.TypeDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (type.ParentModule != mainModule)
                        continue;

                    var shortName = type.Name;
                    var fullName = type.FullName;

                    // Only consider nested types or types with compiler-generated naming
                    var isNested = type.DeclaringType != null;
                    var hasCompilerGeneratedName = shortName.Contains('<') || shortName.Contains('>');

                    if (!isNested && !hasCompilerGeneratedName)
                        continue;

                    // Check compiler-generated attribute
                    bool hasCompilerGeneratedAttr = false;
                    try
                    {
                        var typeDefHandle = (TypeDefinitionHandle)type.MetadataToken;
                        var typeDef = reader.GetTypeDefinition(typeDefHandle);
                        foreach (var attrHandle in typeDef.GetCustomAttributes())
                        {
                            var attr = reader.GetCustomAttribute(attrHandle);
                            var attrTypeName = GetAttributeTypeName(reader, attr);
                            if (attrTypeName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute")
                            {
                                hasCompilerGeneratedAttr = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Skip attribute check errors
                    }

                    if (!hasCompilerGeneratedName && !hasCompilerGeneratedAttr)
                        continue;

                    // Classify the kind
                    var kind = ClassifyCompilerGeneratedType(type, shortName);
                    if (kind == null)
                        continue;

                    // Extract parent method from naming convention: text between < and >
                    string? parentMethod = null;
                    var ltIndex = shortName.IndexOf('<');
                    var gtIndex = shortName.IndexOf('>');
                    if (ltIndex >= 0 && gtIndex > ltIndex + 1)
                    {
                        parentMethod = shortName.Substring(ltIndex + 1, gtIndex - ltIndex - 1);
                    }

                    // Parent type is the declaring type
                    string? parentType = type.DeclaringType?.FullName;

                    results.Add(new CompilerGeneratedTypeInfo
                    {
                        FullName = fullName,
                        ShortName = shortName,
                        GeneratedKind = kind,
                        ParentMethod = parentMethod,
                        ParentType = parentType
                    });
                }

                return (IReadOnlyList<CompilerGeneratedTypeInfo>)results;
            }
            catch (MetadataFileNotSupportedException ex)
            {
                _logger.LogWarning("Assembly is not a .NET assembly: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value,
                    $"'{assemblyPath.FileName}' is not a .NET assembly. The file does not contain managed (.NET) metadata and cannot be decompiled.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find compiler-generated types in {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    // ---- Private helpers ----

    private static string GetPEKind(PEHeaders peHeaders)
    {
        var machine = peHeaders.CoffHeader.Machine;
        var corFlags = peHeaders.CorHeader?.Flags ?? 0;

        return machine switch
        {
            Machine.Amd64 => "x64",
            Machine.Arm64 => "ARM64",
            Machine.I386 when (corFlags & CorFlags.Requires32Bit) != 0 => "x86",
            Machine.I386 => "AnyCPU",
            _ => machine.ToString()
        };
    }

    private static string GetAttributeTypeName(MetadataReader reader, CustomAttribute attr)
    {
        var ctorHandle = attr.Constructor;
        if (ctorHandle.Kind == HandleKind.MemberReference)
        {
            var memberRef = reader.GetMemberReference((MemberReferenceHandle)ctorHandle);
            var parent = memberRef.Parent;
            if (parent.Kind == HandleKind.TypeReference)
            {
                var typeRef = reader.GetTypeReference((TypeReferenceHandle)parent);
                var ns = reader.GetString(typeRef.Namespace);
                var name = reader.GetString(typeRef.Name);
                return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }
            else if (parent.Kind == HandleKind.TypeDefinition)
            {
                var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)parent);
                var ns = reader.GetString(typeDef.Namespace);
                var name = reader.GetString(typeDef.Name);
                return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }
        }
        else if (ctorHandle.Kind == HandleKind.MethodDefinition)
        {
            var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)ctorHandle);
            var declaringType = methodDef.GetDeclaringType();
            var typeDef = reader.GetTypeDefinition(declaringType);
            var ns = reader.GetString(typeDef.Namespace);
            var name = reader.GetString(typeDef.Name);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        return "Unknown";
    }

    private static IReadOnlyList<AttributeInfo> DecodeAttributes(
        MetadataReader reader,
        CustomAttributeHandleCollection attrHandles)
    {
        var results = new List<AttributeInfo>();
        var provider = new StringAttributeTypeProvider();

        foreach (var attrHandle in attrHandles)
        {
            var attr = reader.GetCustomAttribute(attrHandle);
            var typeName = GetAttributeTypeName(reader, attr);

            var constructorArgs = new List<string>();
            var namedArgs = new Dictionary<string, string>();

            try
            {
                var decoded = attr.DecodeValue(provider);
                foreach (var fixedArg in decoded.FixedArguments)
                {
                    constructorArgs.Add(fixedArg.Value?.ToString() ?? "null");
                }
                foreach (var namedArg in decoded.NamedArguments)
                {
                    var argName = namedArg.Name ?? "unknown";
                    namedArgs[argName] = namedArg.Value?.ToString() ?? "null";
                }
            }
            catch
            {
                // If decode fails, still include the attribute with no args
            }

            results.Add(new AttributeInfo
            {
                AttributeType = typeName,
                ConstructorArguments = constructorArgs,
                NamedArguments = namedArgs
            });
        }

        return results;
    }

    private static string? ClassifyCompilerGeneratedType(ITypeDefinition type, string shortName)
    {
        // DisplayClass (lambda capture)
        if (shortName.Contains("DisplayClass") || shortName.Contains("AnonStorey") || shortName.StartsWith("<>c__"))
            return "DisplayClass";

        // Check for state machine types (d__ suffix pattern)
        if (shortName.Contains("d__") && type.DeclaringType != null)
        {
            // Check for IAsyncStateMachine
            foreach (var baseType in type.DirectBaseTypes)
            {
                if (baseType.FullName == "System.Runtime.CompilerServices.IAsyncStateMachine")
                    return "AsyncStateMachine";
                if (baseType.FullName.Contains("IEnumerator"))
                    return "Iterator";
            }
            // Default to AsyncStateMachine for d__ types (common case)
            return "AsyncStateMachine";
        }

        // Static lambda cache class
        if (shortName == "<>c")
            return "Closure";

        // Has compiler-generated naming but not classified above
        if (shortName.Contains('<') || shortName.Contains('>'))
            return "CompilerGenerated";

        return null;
    }

    /// <summary>
    /// Checks if byte content appears to be valid UTF-8 text without binary control characters.
    /// </summary>
    private static bool IsTextContent(byte[] bytes)
    {
        if (bytes.Length == 0) return true;

        // Check for UTF-8 BOM or valid text
        try
        {
            var text = Encoding.UTF8.GetString(bytes);
            foreach (var c in text)
            {
                if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                    return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;
}

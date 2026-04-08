using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using AssemblyPath = ILSpy.Mcp.Domain.Models.AssemblyPath;
using TypeName = ILSpy.Mcp.Domain.Models.TypeName;
using DomainTypeKind = ILSpy.Mcp.Domain.Models.TypeKind;

namespace ILSpy.Mcp.Infrastructure.Decompiler;

/// <summary>
/// Adapter that implements ICrossReferenceService using System.Reflection.Metadata IL scanning
/// and ICSharpCode.Decompiler type system for name resolution.
/// </summary>
public sealed class ILSpyCrossReferenceService : ICrossReferenceService
{
    private readonly ILogger<ILSpyCrossReferenceService> _logger;
    private readonly DecompilerSettings _settings;

    public ILSpyCrossReferenceService(ILogger<ILSpyCrossReferenceService> logger)
    {
        _logger = logger;
        _settings = new DecompilerSettings
        {
            ThrowOnAssemblyResolveErrors = false,
            ShowXmlDocumentation = false
        };
    }

    public async Task<IReadOnlyList<UsageResult>> FindUsagesAsync(
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

                // Find the target member's metadata tokens
                var targetTokens = GetMemberTokens(type, memberName, decompiler);
                if (targetTokens.Count == 0)
                    throw new MethodNotFoundException(memberName, typeName.FullName);

                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;
                var results = new List<UsageResult>();

                // Scan all method bodies in the assembly
                foreach (var scanType in decompiler.TypeSystem.MainModule.TypeDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (scanType.ParentModule != decompiler.TypeSystem.MainModule)
                        continue;

                    foreach (var method in scanType.Methods)
                    {
                        if (method.MetadataToken.IsNil)
                            continue;

                        var methodHandle = (MethodDefinitionHandle)method.MetadataToken;
                        var methodDef = reader.GetMethodDefinition(methodHandle);

                        if (methodDef.RelativeVirtualAddress == 0)
                            continue; // Abstract or extern

                        try
                        {
                            var body = metadataFile.GetMethodBody(methodDef.RelativeVirtualAddress);
                            if (body == null) continue;

                            var ilReader = body.GetILReader();
                            ScanILForUsages(ref ilReader, reader, targetTokens, scanType, method, results);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogDebug(ex, "Skipping method {Method} during IL scan", method.FullName);
                        }
                    }
                }

                return results;
            }
            catch (TypeNotFoundException) { throw; }
            catch (MethodNotFoundException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find usages of {MemberName} in {TypeName} from {Assembly}",
                    memberName, typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ImplementorResult>> FindImplementorsAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var targetType = decompiler.TypeSystem.MainModule.GetTypeDefinition(
                    new FullTypeName(typeName.FullName));

                if (targetType == null)
                    throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

                var results = new List<ImplementorResult>();
                var mainModule = decompiler.TypeSystem.MainModule;

                foreach (var candidate in mainModule.TypeDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (candidate.ParentModule != mainModule)
                        continue;

                    // Skip the target type itself
                    if (candidate.FullName == targetType.FullName)
                        continue;

                    // Check if candidate directly implements/extends the target
                    bool isDirect = false;
                    foreach (var baseType in candidate.DirectBaseTypes)
                    {
                        if (baseType.FullName == targetType.FullName)
                        {
                            isDirect = true;
                            break;
                        }
                    }

                    if (isDirect)
                    {
                        results.Add(new ImplementorResult
                        {
                            TypeFullName = candidate.FullName,
                            TypeShortName = candidate.Name,
                            IsDirect = true,
                            Kind = MapTypeKind(candidate.Kind)
                        });
                    }
                }

                // Second pass: find indirect implementors (types that extend direct implementors)
                var directNames = new HashSet<string>(results.Select(r => r.TypeFullName));
                foreach (var candidate in mainModule.TypeDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (candidate.ParentModule != mainModule)
                        continue;

                    if (candidate.FullName == targetType.FullName)
                        continue;

                    if (directNames.Contains(candidate.FullName))
                        continue;

                    // Check if any base type is a direct implementor
                    foreach (var baseType in candidate.DirectBaseTypes)
                    {
                        if (directNames.Contains(baseType.FullName))
                        {
                            results.Add(new ImplementorResult
                            {
                                TypeFullName = candidate.FullName,
                                TypeShortName = candidate.Name,
                                IsDirect = false,
                                Kind = MapTypeKind(candidate.Kind)
                            });
                            break;
                        }
                    }
                }

                return results;
            }
            catch (TypeNotFoundException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find implementors of {TypeName} from {Assembly}",
                    typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DependencyResult>> FindDependenciesAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string? methodName = null,
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

                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;
                var results = new List<DependencyResult>();
                var seen = new HashSet<string>();

                // Determine which methods to scan
                IEnumerable<IMethod> methods;
                if (!string.IsNullOrEmpty(methodName))
                {
                    methods = type.Methods.Where(m => m.Name == methodName);
                    if (!methods.Any())
                        throw new MethodNotFoundException(methodName, typeName.FullName);
                }
                else
                {
                    methods = type.Methods;
                }

                foreach (var method in methods)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (method.MetadataToken.IsNil)
                        continue;

                    var methodHandle = (MethodDefinitionHandle)method.MetadataToken;
                    var methodDef = reader.GetMethodDefinition(methodHandle);

                    if (methodDef.RelativeVirtualAddress == 0)
                        continue;

                    try
                    {
                        var body = metadataFile.GetMethodBody(methodDef.RelativeVirtualAddress);
                        if (body == null) continue;

                        var ilReader = body.GetILReader();
                        ScanILForDependencies(ref ilReader, reader, typeName.FullName, results, seen);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogDebug(ex, "Skipping method {Method} during dependency scan", method.FullName);
                    }
                }

                return results;
            }
            catch (TypeNotFoundException) { throw; }
            catch (MethodNotFoundException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find dependencies of {TypeName} from {Assembly}",
                    typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<InstantiationResult>> FindInstantiationsAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var targetType = decompiler.TypeSystem.MainModule.GetTypeDefinition(
                    new FullTypeName(typeName.FullName));

                if (targetType == null)
                    throw new TypeNotFoundException(typeName.FullName, assemblyPath.Value);

                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;
                var results = new List<InstantiationResult>();

                // Scan all method bodies for newobj targeting this type
                foreach (var scanType in decompiler.TypeSystem.MainModule.TypeDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (scanType.ParentModule != decompiler.TypeSystem.MainModule)
                        continue;

                    foreach (var method in scanType.Methods)
                    {
                        if (method.MetadataToken.IsNil)
                            continue;

                        var methodHandle = (MethodDefinitionHandle)method.MetadataToken;
                        var methodDef = reader.GetMethodDefinition(methodHandle);

                        if (methodDef.RelativeVirtualAddress == 0)
                            continue;

                        try
                        {
                            var body = metadataFile.GetMethodBody(methodDef.RelativeVirtualAddress);
                            if (body == null) continue;

                            var ilReader = body.GetILReader();
                            ScanILForInstantiations(ref ilReader, reader, typeName.FullName, scanType, method, results);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogDebug(ex, "Skipping method {Method} during instantiation scan", method.FullName);
                        }
                    }
                }

                return results;
            }
            catch (TypeNotFoundException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find instantiations of {TypeName} from {Assembly}",
                    typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    // ---- Private IL scanning helpers ----

    /// <summary>
    /// Gets metadata tokens for all members of a type matching the given name.
    /// Returns a dictionary mapping token int -> UsageKind category.
    /// </summary>
    private static Dictionary<int, UsageKind> GetMemberTokens(
        ITypeDefinition type, string memberName, CSharpDecompiler decompiler)
    {
        var tokens = new Dictionary<int, UsageKind>();

        foreach (var method in type.Methods.Where(m => m.Name == memberName))
        {
            if (!method.MetadataToken.IsNil)
            {
                var kind = method.IsVirtual || method.IsAbstract ? UsageKind.VirtualCall : UsageKind.Call;
                tokens[MetadataTokens.GetToken(method.MetadataToken)] = kind;
            }
        }

        foreach (var field in type.Fields.Where(f => f.Name == memberName))
        {
            if (!field.MetadataToken.IsNil)
                tokens[MetadataTokens.GetToken(field.MetadataToken)] = UsageKind.FieldRead;
        }

        foreach (var prop in type.Properties.Where(p => p.Name == memberName))
        {
            if (prop.Getter != null && !prop.Getter.MetadataToken.IsNil)
                tokens[MetadataTokens.GetToken(prop.Getter.MetadataToken)] = UsageKind.PropertyGet;
            if (prop.Setter != null && !prop.Setter.MetadataToken.IsNil)
                tokens[MetadataTokens.GetToken(prop.Setter.MetadataToken)] = UsageKind.PropertySet;
        }

        return tokens;
    }

    /// <summary>
    /// Scans IL bytes for call/callvirt/ldfld/stfld/ldsfld/stsfld instructions
    /// and checks if they reference any of the target tokens.
    /// </summary>
    private static void ScanILForUsages(
        ref BlobReader ilReader,
        MetadataReader reader,
        Dictionary<int, UsageKind> targetTokens,
        ITypeDefinition scanType,
        IMethod method,
        List<UsageResult> results)
    {
        while (ilReader.RemainingBytes > 0)
        {
            int offset = ilReader.Offset;
            var opCode = ReadILOpCode(ref ilReader);

            if (IsTokenReferenceOpCode(opCode))
            {
                int token = ilReader.ReadInt32();
                if (targetTokens.TryGetValue(token, out var baseKind))
                {
                    // Refine the kind based on the actual opcode
                    var kind = RefineUsageKind(opCode, baseKind);
                    results.Add(new UsageResult
                    {
                        DeclaringType = scanType.FullName,
                        MethodName = method.Name,
                        ILOffset = offset,
                        Kind = kind,
                        MethodSignature = FormatMethodSignature(method)
                    });
                }
            }
            else
            {
                SkipOperand(ref ilReader, opCode);
            }
        }
    }

    /// <summary>
    /// Scans IL bytes for outward references (calls, field accesses) from a method.
    /// </summary>
    private static void ScanILForDependencies(
        ref BlobReader ilReader,
        MetadataReader reader,
        string sourceTypeName,
        List<DependencyResult> results,
        HashSet<string> seen)
    {
        while (ilReader.RemainingBytes > 0)
        {
            var opCode = ReadILOpCode(ref ilReader);

            if (IsTokenReferenceOpCode(opCode))
            {
                int token = ilReader.ReadInt32();
                var handle = MetadataTokens.EntityHandle(token);

                string? targetMember = null;
                string? targetType = null;
                DependencyKind kind = DependencyKind.MethodCall;

                try
                {
                    if (handle.Kind == HandleKind.MemberReference)
                    {
                        var memberRef = reader.GetMemberReference((MemberReferenceHandle)handle);
                        var memberRefName = reader.GetString(memberRef.Name);
                        targetType = GetMemberReferenceDeclaringType(reader, memberRef);
                        targetMember = $"{targetType}.{memberRefName}";

                        kind = memberRef.GetKind() == MemberReferenceKind.Method
                            ? (opCode == ILOpCode.Callvirt ? DependencyKind.VirtualCall : DependencyKind.MethodCall)
                            : DependencyKind.FieldAccess;
                    }
                    else if (handle.Kind == HandleKind.MethodDefinition)
                    {
                        var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                        var methodName = reader.GetString(methodDef.Name);
                        var declaringTypeHandle = methodDef.GetDeclaringType();
                        targetType = GetTypeFullName(reader, declaringTypeHandle);
                        targetMember = $"{targetType}.{methodName}";
                        kind = opCode == ILOpCode.Callvirt ? DependencyKind.VirtualCall : DependencyKind.MethodCall;
                    }
                    else if (handle.Kind == HandleKind.FieldDefinition)
                    {
                        var fieldDef = reader.GetFieldDefinition((FieldDefinitionHandle)handle);
                        var fieldName = reader.GetString(fieldDef.Name);
                        var declaringTypeHandle = fieldDef.GetDeclaringType();
                        targetType = GetTypeFullName(reader, declaringTypeHandle);
                        targetMember = $"{targetType}.{fieldName}";
                        kind = DependencyKind.FieldAccess;
                    }
                }
                catch
                {
                    // Skip unresolvable tokens
                    continue;
                }

                if (targetMember != null && targetType != null
                    && targetType != sourceTypeName
                    && seen.Add(targetMember))
                {
                    results.Add(new DependencyResult
                    {
                        TargetMember = targetMember,
                        TargetType = targetType,
                        Kind = kind
                    });
                }
            }
            else
            {
                SkipOperand(ref ilReader, opCode);
            }
        }
    }

    /// <summary>
    /// Scans IL bytes for newobj instructions targeting the specified type.
    /// </summary>
    private static void ScanILForInstantiations(
        ref BlobReader ilReader,
        MetadataReader reader,
        string targetTypeName,
        ITypeDefinition scanType,
        IMethod method,
        List<InstantiationResult> results)
    {
        while (ilReader.RemainingBytes > 0)
        {
            int offset = ilReader.Offset;
            var opCode = ReadILOpCode(ref ilReader);

            if (opCode == ILOpCode.Newobj)
            {
                int token = ilReader.ReadInt32();
                var handle = MetadataTokens.EntityHandle(token);

                string? constructorType = null;
                try
                {
                    if (handle.Kind == HandleKind.MemberReference)
                    {
                        var memberRef = reader.GetMemberReference((MemberReferenceHandle)handle);
                        constructorType = GetMemberReferenceDeclaringType(reader, memberRef);
                    }
                    else if (handle.Kind == HandleKind.MethodDefinition)
                    {
                        var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                        constructorType = GetTypeFullName(reader, methodDef.GetDeclaringType());
                    }
                }
                catch
                {
                    // Skip unresolvable tokens
                }

                if (constructorType == targetTypeName)
                {
                    results.Add(new InstantiationResult
                    {
                        DeclaringType = scanType.FullName,
                        MethodName = method.Name,
                        ILOffset = offset,
                        MethodSignature = FormatMethodSignature(method)
                    });
                }
            }
            else if (IsTokenReferenceOpCode(opCode))
            {
                ilReader.ReadInt32(); // Skip the token
            }
            else
            {
                SkipOperand(ref ilReader, opCode);
            }
        }
    }

    // ---- IL helper methods ----

    /// <summary>
    /// Reads an IL opcode, handling both single-byte and two-byte (0xFE prefix) opcodes.
    /// </summary>
    private static ILOpCode ReadILOpCode(ref BlobReader reader)
    {
        byte b = reader.ReadByte();
        if (b == 0xFE && reader.RemainingBytes > 0)
        {
            byte b2 = reader.ReadByte();
            return (ILOpCode)(0xFE00 | b2);
        }
        return (ILOpCode)b;
    }

    /// <summary>
    /// Returns true for opcodes that take an inline metadata token operand.
    /// </summary>
    private static bool IsTokenReferenceOpCode(ILOpCode opCode)
    {
        return opCode switch
        {
            ILOpCode.Call => true,
            ILOpCode.Callvirt => true,
            ILOpCode.Newobj => true,
            ILOpCode.Ldfld => true,
            ILOpCode.Stfld => true,
            ILOpCode.Ldsfld => true,
            ILOpCode.Stsfld => true,
            ILOpCode.Ldflda => true,
            ILOpCode.Ldsflda => true,
            ILOpCode.Ldtoken => true,
            ILOpCode.Ldftn => true,
            ILOpCode.Ldvirtftn => true,
            _ => false
        };
    }

    /// <summary>
    /// Refines the usage kind based on the actual IL opcode encountered.
    /// </summary>
    private static UsageKind RefineUsageKind(ILOpCode opCode, UsageKind baseKind)
    {
        return opCode switch
        {
            ILOpCode.Call or ILOpCode.Ldftn => baseKind == UsageKind.PropertyGet ? UsageKind.PropertyGet
                : baseKind == UsageKind.PropertySet ? UsageKind.PropertySet
                : UsageKind.Call,
            ILOpCode.Callvirt or ILOpCode.Ldvirtftn => baseKind == UsageKind.PropertyGet ? UsageKind.PropertyGet
                : baseKind == UsageKind.PropertySet ? UsageKind.PropertySet
                : UsageKind.VirtualCall,
            ILOpCode.Ldfld or ILOpCode.Ldsfld or ILOpCode.Ldflda or ILOpCode.Ldsflda => UsageKind.FieldRead,
            ILOpCode.Stfld or ILOpCode.Stsfld => UsageKind.FieldWrite,
            _ => baseKind
        };
    }

    /// <summary>
    /// Skips the operand of an IL instruction based on its opcode.
    /// </summary>
    private static void SkipOperand(ref BlobReader reader, ILOpCode opCode)
    {
        switch (GetOperandSize(opCode))
        {
            case 0: break;
            case 1: reader.ReadByte(); break;
            case 2: reader.ReadInt16(); break;
            case 4: reader.ReadInt32(); break;
            case 8: reader.ReadInt64(); break;
            case -1: // Switch instruction
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                    reader.ReadInt32();
                break;
        }
    }

    /// <summary>
    /// Returns the operand size for an IL opcode. Returns -1 for switch (variable length).
    /// </summary>
    private static int GetOperandSize(ILOpCode opCode)
    {
        return opCode switch
        {
            // No operand
            ILOpCode.Nop or ILOpCode.Break or ILOpCode.Ldarg_0 or ILOpCode.Ldarg_1 or ILOpCode.Ldarg_2
            or ILOpCode.Ldarg_3 or ILOpCode.Ldloc_0 or ILOpCode.Ldloc_1 or ILOpCode.Ldloc_2
            or ILOpCode.Ldloc_3 or ILOpCode.Stloc_0 or ILOpCode.Stloc_1 or ILOpCode.Stloc_2
            or ILOpCode.Stloc_3 or ILOpCode.Ldnull or ILOpCode.Ldc_i4_m1 or ILOpCode.Ldc_i4_0
            or ILOpCode.Ldc_i4_1 or ILOpCode.Ldc_i4_2 or ILOpCode.Ldc_i4_3 or ILOpCode.Ldc_i4_4
            or ILOpCode.Ldc_i4_5 or ILOpCode.Ldc_i4_6 or ILOpCode.Ldc_i4_7 or ILOpCode.Ldc_i4_8
            or ILOpCode.Dup or ILOpCode.Pop or ILOpCode.Ret or ILOpCode.Ldind_i1 or ILOpCode.Ldind_u1
            or ILOpCode.Ldind_i2 or ILOpCode.Ldind_u2 or ILOpCode.Ldind_i4 or ILOpCode.Ldind_u4
            or ILOpCode.Ldind_i8 or ILOpCode.Ldind_i or ILOpCode.Ldind_r4 or ILOpCode.Ldind_r8
            or ILOpCode.Ldind_ref or ILOpCode.Stind_ref or ILOpCode.Stind_i1 or ILOpCode.Stind_i2
            or ILOpCode.Stind_i4 or ILOpCode.Stind_i8 or ILOpCode.Stind_r4 or ILOpCode.Stind_r8
            or ILOpCode.Add or ILOpCode.Sub or ILOpCode.Mul or ILOpCode.Div or ILOpCode.Div_un
            or ILOpCode.Rem or ILOpCode.Rem_un or ILOpCode.And or ILOpCode.Or or ILOpCode.Xor
            or ILOpCode.Shl or ILOpCode.Shr or ILOpCode.Shr_un or ILOpCode.Neg or ILOpCode.Not
            or ILOpCode.Conv_i1 or ILOpCode.Conv_i2 or ILOpCode.Conv_i4 or ILOpCode.Conv_i8
            or ILOpCode.Conv_r4 or ILOpCode.Conv_r8 or ILOpCode.Conv_u4 or ILOpCode.Conv_u8
            or ILOpCode.Conv_r_un or ILOpCode.Throw or ILOpCode.Conv_ovf_i1_un or ILOpCode.Conv_ovf_i2_un
            or ILOpCode.Conv_ovf_i4_un or ILOpCode.Conv_ovf_i8_un or ILOpCode.Conv_ovf_u1_un
            or ILOpCode.Conv_ovf_u2_un or ILOpCode.Conv_ovf_u4_un or ILOpCode.Conv_ovf_u8_un
            or ILOpCode.Conv_ovf_i_un or ILOpCode.Conv_ovf_u_un or ILOpCode.Ldlen
            or ILOpCode.Ldelem_i1 or ILOpCode.Ldelem_u1 or ILOpCode.Ldelem_i2 or ILOpCode.Ldelem_u2
            or ILOpCode.Ldelem_i4 or ILOpCode.Ldelem_u4 or ILOpCode.Ldelem_i8 or ILOpCode.Ldelem_i
            or ILOpCode.Ldelem_r4 or ILOpCode.Ldelem_r8 or ILOpCode.Ldelem_ref
            or ILOpCode.Stelem_i or ILOpCode.Stelem_i1 or ILOpCode.Stelem_i2 or ILOpCode.Stelem_i4
            or ILOpCode.Stelem_i8 or ILOpCode.Stelem_r4 or ILOpCode.Stelem_r8 or ILOpCode.Stelem_ref
            or ILOpCode.Conv_ovf_i1 or ILOpCode.Conv_ovf_u1 or ILOpCode.Conv_ovf_i2 or ILOpCode.Conv_ovf_u2
            or ILOpCode.Conv_ovf_i4 or ILOpCode.Conv_ovf_u4 or ILOpCode.Conv_ovf_i8 or ILOpCode.Conv_ovf_u8
            or ILOpCode.Ckfinite or ILOpCode.Conv_u2 or ILOpCode.Conv_u1 or ILOpCode.Conv_i
            or ILOpCode.Conv_ovf_i or ILOpCode.Conv_ovf_u or ILOpCode.Add_ovf or ILOpCode.Add_ovf_un
            or ILOpCode.Mul_ovf or ILOpCode.Mul_ovf_un or ILOpCode.Sub_ovf or ILOpCode.Sub_ovf_un
            or ILOpCode.Endfinally or ILOpCode.Stind_i or ILOpCode.Conv_u or ILOpCode.Rethrow
            or ILOpCode.Refanytype or ILOpCode.Readonly
            => 0,

            // 1-byte operand (short branch, short local/arg)
            ILOpCode.Ldarg_s or ILOpCode.Ldarga_s or ILOpCode.Starg_s or ILOpCode.Ldloc_s
            or ILOpCode.Ldloca_s or ILOpCode.Stloc_s or ILOpCode.Ldc_i4_s
            or ILOpCode.Br_s or ILOpCode.Brfalse_s or ILOpCode.Brtrue_s
            or ILOpCode.Beq_s or ILOpCode.Bge_s or ILOpCode.Bgt_s or ILOpCode.Ble_s or ILOpCode.Blt_s
            or ILOpCode.Bne_un_s or ILOpCode.Bge_un_s or ILOpCode.Bgt_un_s or ILOpCode.Ble_un_s
            or ILOpCode.Blt_un_s or ILOpCode.Leave_s or ILOpCode.Unaligned
            => 1,

            // 2-byte operand (arg/local index in two-byte prefix form)
            ILOpCode.Ldarg or ILOpCode.Ldarga or ILOpCode.Starg or ILOpCode.Ldloc or ILOpCode.Ldloca
            or ILOpCode.Stloc
            => 2,

            // 4-byte operand (inline int, float, token, branch target)
            ILOpCode.Br or ILOpCode.Brfalse or ILOpCode.Brtrue
            or ILOpCode.Beq or ILOpCode.Bge or ILOpCode.Bgt or ILOpCode.Ble or ILOpCode.Blt
            or ILOpCode.Bne_un or ILOpCode.Bge_un or ILOpCode.Bgt_un or ILOpCode.Ble_un or ILOpCode.Blt_un
            or ILOpCode.Leave or ILOpCode.Ldc_i4 or ILOpCode.Ldc_r4
            or ILOpCode.Jmp or ILOpCode.Call or ILOpCode.Calli or ILOpCode.Callvirt
            or ILOpCode.Cpobj or ILOpCode.Ldobj or ILOpCode.Ldstr or ILOpCode.Newobj
            or ILOpCode.Castclass or ILOpCode.Isinst or ILOpCode.Unbox
            or ILOpCode.Ldfld or ILOpCode.Ldflda or ILOpCode.Stfld or ILOpCode.Ldsfld
            or ILOpCode.Ldsflda or ILOpCode.Stsfld or ILOpCode.Stobj
            or ILOpCode.Box or ILOpCode.Newarr or ILOpCode.Ldelema or ILOpCode.Ldelem
            or ILOpCode.Stelem or ILOpCode.Unbox_any
            or ILOpCode.Refanyval or ILOpCode.Mkrefany
            or ILOpCode.Ldtoken or ILOpCode.Ldftn or ILOpCode.Ldvirtftn
            or ILOpCode.Initobj or ILOpCode.Constrained or ILOpCode.Sizeof
            => 4,

            // 8-byte operand
            ILOpCode.Ldc_i8 or ILOpCode.Ldc_r8
            => 8,

            // Switch (variable)
            ILOpCode.Switch => -1,

            // Default: assume no operand (safest for unknown two-byte prefixed ops)
            _ => 0
        };
    }

    /// <summary>
    /// Gets the declaring type name from a MemberReference.
    /// </summary>
    private static string? GetMemberReferenceDeclaringType(MetadataReader reader, MemberReference memberRef)
    {
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
            return GetTypeFullName(reader, (TypeDefinitionHandle)parent);
        }
        return null;
    }

    /// <summary>
    /// Gets the full name of a type from its TypeDefinitionHandle.
    /// </summary>
    private static string GetTypeFullName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        var ns = reader.GetString(typeDef.Namespace);
        var name = reader.GetString(typeDef.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    /// <summary>
    /// Formats a method signature for display.
    /// </summary>
    private static string FormatMethodSignature(IMethod method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.Name} {p.Name}"));
        return $"{method.ReturnType.Name} {method.Name}({parameters})";
    }

    private static DomainTypeKind MapTypeKind(ICSharpCode.Decompiler.TypeSystem.TypeKind kind) => kind switch
    {
        ICSharpCode.Decompiler.TypeSystem.TypeKind.Class => DomainTypeKind.Class,
        ICSharpCode.Decompiler.TypeSystem.TypeKind.Interface => DomainTypeKind.Interface,
        ICSharpCode.Decompiler.TypeSystem.TypeKind.Struct => DomainTypeKind.Struct,
        ICSharpCode.Decompiler.TypeSystem.TypeKind.Enum => DomainTypeKind.Enum,
        ICSharpCode.Decompiler.TypeSystem.TypeKind.Delegate => DomainTypeKind.Delegate,
        _ => DomainTypeKind.Unknown
    };
}

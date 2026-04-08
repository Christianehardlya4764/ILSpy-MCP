using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using AssemblyPath = ILSpy.Mcp.Domain.Models.AssemblyPath;

namespace ILSpy.Mcp.Infrastructure.Decompiler;

/// <summary>
/// Adapter that implements ISearchService using System.Reflection.Metadata IL scanning
/// for string literal and numeric constant search across assembly method bodies.
/// </summary>
public sealed class ILSpySearchService : ISearchService
{
    private readonly ILogger<ILSpySearchService> _logger;
    private readonly DecompilerSettings _settings;

    public ILSpySearchService(ILogger<ILSpySearchService> logger)
    {
        _logger = logger;
        _settings = new DecompilerSettings
        {
            ThrowOnAssemblyResolveErrors = false,
            ShowXmlDocumentation = false
        };
    }

    public async Task<SearchResults<StringSearchResult>> SearchStringsAsync(
        AssemblyPath assemblyPath,
        string regexPattern,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        // Validate regex before starting scan — throws ArgumentException on invalid pattern
        var regex = new Regex(regexPattern, RegexOptions.Compiled);

        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;
                var allMatches = new List<StringSearchResult>();

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
                            ScanILForStrings(ref ilReader, reader, regex, scanType, method, allMatches);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogDebug(ex, "Skipping method {Method} during string scan", method.FullName);
                        }
                    }
                }

                return new SearchResults<StringSearchResult>
                {
                    TotalCount = allMatches.Count,
                    Offset = offset,
                    Limit = maxResults,
                    Results = allMatches.Skip(offset).Take(maxResults).ToList()
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                _logger.LogError(ex, "Failed to search strings in {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<SearchResults<ConstantSearchResult>> SearchConstantsAsync(
        AssemblyPath assemblyPath,
        long value,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var decompiler = new CSharpDecompiler(assemblyPath.Value, _settings);
                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
                var reader = metadataFile.Metadata;
                var allMatches = new List<ConstantSearchResult>();

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
                            ScanILForConstants(ref ilReader, value, scanType, method, allMatches);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogDebug(ex, "Skipping method {Method} during constant scan", method.FullName);
                        }
                    }
                }

                return new SearchResults<ConstantSearchResult>
                {
                    TotalCount = allMatches.Count,
                    Offset = offset,
                    Limit = maxResults,
                    Results = allMatches.Skip(offset).Take(maxResults).ToList()
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search constants in {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    // ---- Private IL scanning methods ----

    private static void ScanILForStrings(
        ref BlobReader ilReader,
        MetadataReader reader,
        Regex regex,
        ITypeDefinition scanType,
        IMethod method,
        List<StringSearchResult> results)
    {
        while (ilReader.RemainingBytes > 0)
        {
            int offset = ilReader.Offset;
            var opCode = ReadILOpCode(ref ilReader);

            if (opCode == ILOpCode.Ldstr)
            {
                int token = ilReader.ReadInt32();
                var handle = MetadataTokens.UserStringHandle(token & 0x00FFFFFF);
                var stringValue = reader.GetUserString(handle);

                if (regex.IsMatch(stringValue))
                {
                    results.Add(new StringSearchResult
                    {
                        MatchedValue = stringValue,
                        DeclaringType = scanType.FullName,
                        MethodName = method.Name,
                        MethodSignature = FormatMethodSignature(method),
                        ILOffset = offset
                    });
                }
            }
            else if (IsTokenReferenceOpCode(opCode))
            {
                ilReader.ReadInt32(); // Skip token
            }
            else
            {
                SkipOperand(ref ilReader, opCode);
            }
        }
    }

    private static void ScanILForConstants(
        ref BlobReader ilReader,
        long targetValue,
        ITypeDefinition scanType,
        IMethod method,
        List<ConstantSearchResult> results)
    {
        while (ilReader.RemainingBytes > 0)
        {
            int offset = ilReader.Offset;
            var opCode = ReadILOpCode(ref ilReader);

            long? extractedValue = null;
            string constantType = "Int32";

            switch (opCode)
            {
                case ILOpCode.Ldc_i4_m1: extractedValue = -1; break;
                case ILOpCode.Ldc_i4_0: extractedValue = 0; break;
                case ILOpCode.Ldc_i4_1: extractedValue = 1; break;
                case ILOpCode.Ldc_i4_2: extractedValue = 2; break;
                case ILOpCode.Ldc_i4_3: extractedValue = 3; break;
                case ILOpCode.Ldc_i4_4: extractedValue = 4; break;
                case ILOpCode.Ldc_i4_5: extractedValue = 5; break;
                case ILOpCode.Ldc_i4_6: extractedValue = 6; break;
                case ILOpCode.Ldc_i4_7: extractedValue = 7; break;
                case ILOpCode.Ldc_i4_8: extractedValue = 8; break;
                case ILOpCode.Ldc_i4_s:
                    extractedValue = ilReader.ReadSByte();
                    break;
                case ILOpCode.Ldc_i4:
                    extractedValue = ilReader.ReadInt32();
                    break;
                case ILOpCode.Ldc_i8:
                    extractedValue = ilReader.ReadInt64();
                    constantType = "Int64";
                    break;
                default:
                    if (IsTokenReferenceOpCode(opCode))
                        ilReader.ReadInt32();
                    else
                        SkipOperand(ref ilReader, opCode);
                    continue;
            }

            if (extractedValue == targetValue)
            {
                results.Add(new ConstantSearchResult
                {
                    MatchedValue = extractedValue.Value,
                    ConstantType = constantType,
                    DeclaringType = scanType.FullName,
                    MethodName = method.Name,
                    MethodSignature = FormatMethodSignature(method),
                    ILOffset = offset
                });
            }
        }
    }

    // ---- IL helper methods (duplicated from ILSpyCrossReferenceService to avoid coupling) ----

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

            // 1-byte operand
            ILOpCode.Ldarg_s or ILOpCode.Ldarga_s or ILOpCode.Starg_s or ILOpCode.Ldloc_s
            or ILOpCode.Ldloca_s or ILOpCode.Stloc_s or ILOpCode.Ldc_i4_s
            or ILOpCode.Br_s or ILOpCode.Brfalse_s or ILOpCode.Brtrue_s
            or ILOpCode.Beq_s or ILOpCode.Bge_s or ILOpCode.Bgt_s or ILOpCode.Ble_s or ILOpCode.Blt_s
            or ILOpCode.Bne_un_s or ILOpCode.Bge_un_s or ILOpCode.Bgt_un_s or ILOpCode.Ble_un_s
            or ILOpCode.Blt_un_s or ILOpCode.Leave_s or ILOpCode.Unaligned
            => 1,

            // 2-byte operand
            ILOpCode.Ldarg or ILOpCode.Ldarga or ILOpCode.Starg or ILOpCode.Ldloc or ILOpCode.Ldloca
            or ILOpCode.Stloc
            => 2,

            // 4-byte operand
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

            // Default: assume no operand
            _ => 0
        };
    }

    private static string FormatMethodSignature(IMethod method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.Name} {p.Name}"));
        return $"{method.ReturnType.Name} {method.Name}({parameters})";
    }
}

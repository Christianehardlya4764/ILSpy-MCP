using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.TypeSystem;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using AssemblyPath = ILSpy.Mcp.Domain.Models.AssemblyPath;
using TypeName = ILSpy.Mcp.Domain.Models.TypeName;

namespace ILSpy.Mcp.Infrastructure.Decompiler;

/// <summary>
/// Adapter that wraps ICSharpCode.Decompiler's ReflectionDisassembler to implement IDisassemblyService.
/// </summary>
public sealed class ILSpyDisassemblyService : IDisassemblyService
{
    private readonly ILogger<ILSpyDisassemblyService> _logger;
    private readonly DecompilerSettings _settings;

    public ILSpyDisassemblyService(ILogger<ILSpyDisassemblyService> logger)
    {
        _logger = logger;
        _settings = new DecompilerSettings
        {
            ThrowOnAssemblyResolveErrors = false,
            ShowXmlDocumentation = false
        };
    }

    public async Task<string> DisassembleTypeAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        bool showTokens = false,
        bool resolveDeep = false,
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

                using var writer = new StringWriter();
                var output = new PlainTextOutput(writer);
                var disassembler = new ReflectionDisassembler(output, cancellationToken)
                {
                    ShowMetadataTokens = showTokens,
                    DetectControlStructure = true
                };

                // D-01: Summary header with type metadata for orientation
                output.WriteLine($"// Type: {type.FullName}");
                output.WriteLine($"// Assembly: {assemblyPath.FileName}");
                output.WriteLine($"// Methods: {type.Methods.Count()}");
                output.WriteLine();

                // D-02: Headers-only iteration — do NOT call DisassembleType which includes bodies
                foreach (var field in type.Fields)
                {
                    disassembler.DisassembleFieldHeader(metadataFile, (FieldDefinitionHandle)field.MetadataToken);
                    output.WriteLine();
                }

                foreach (var method in type.Methods)
                {
                    disassembler.DisassembleMethodHeader(metadataFile, (MethodDefinitionHandle)method.MetadataToken);
                    output.WriteLine();
                }

                foreach (var prop in type.Properties)
                {
                    disassembler.DisassembleProperty(metadataFile, (PropertyDefinitionHandle)prop.MetadataToken);
                    output.WriteLine();
                }

                foreach (var evt in type.Events)
                {
                    disassembler.DisassembleEvent(metadataFile, (EventDefinitionHandle)evt.MetadataToken);
                    output.WriteLine();
                }

                var result = writer.ToString();
                return resolveDeep ? ApplyDeepResolution(result) : result;
            }
            catch (TypeNotFoundException)
            {
                throw;
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Assembly file not found: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
            catch (MetadataFileNotSupportedException ex)
            {
                _logger.LogWarning("Assembly is not a .NET assembly: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value,
                    $"'{assemblyPath.FileName}' is not a .NET assembly. The file does not contain managed (.NET) metadata and cannot be decompiled.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disassemble type {TypeName} from {Assembly}",
                    typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    public async Task<string> DisassembleMethodAsync(
        AssemblyPath assemblyPath,
        TypeName typeName,
        string methodName,
        bool showBytes = false,
        bool showTokens = false,
        bool resolveDeep = false,
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

                var methods = type.Methods.Where(m => m.Name == methodName).ToList();

                if (methods.Count == 0)
                    throw new MethodNotFoundException(methodName, typeName.FullName);

                if (methods.Count > 1)
                {
                    var overloads = string.Join(", ",
                        methods.Select(m =>
                        {
                            var parameters = string.Join(", ",
                                m.Parameters.Select(p => $"{p.Type.Name} {p.Name}"));
                            return $"{methodName}({parameters})";
                        }));
                    throw new MethodNotFoundException(methodName, typeName.FullName,
                        $"Multiple overloads found: {overloads}. Specify parameters to disambiguate.");
                }

                var method = methods[0];
                var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;

                using var writer = new StringWriter();
                var output = new PlainTextOutput(writer);
                var disassembler = new ReflectionDisassembler(output, cancellationToken)
                {
                    ShowMetadataTokens = showTokens,
                    ShowRawRVAOffsetAndBytes = showBytes,
                    DetectControlStructure = true
                };

                // D-03: Full IL body with .maxstack, IL_xxxx labels, resolved names
                disassembler.DisassembleMethod(metadataFile, (MethodDefinitionHandle)method.MetadataToken);

                var result = writer.ToString();
                return resolveDeep ? ApplyDeepResolution(result) : result;
            }
            catch (TypeNotFoundException)
            {
                throw;
            }
            catch (MethodNotFoundException)
            {
                throw;
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Assembly file not found: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
            catch (MetadataFileNotSupportedException ex)
            {
                _logger.LogWarning("Assembly is not a .NET assembly: {Assembly}", assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value,
                    $"'{assemblyPath.FileName}' is not a .NET assembly. The file does not contain managed (.NET) metadata and cannot be decompiled.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disassemble method {MethodName} from {TypeName} in {Assembly}",
                    methodName, typeName.FullName, assemblyPath.Value);
                throw new AssemblyLoadException(assemblyPath.Value, ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Post-processes IL output to expand IL type abbreviations to fully-qualified .NET type names.
    /// This helps AI agents understand types without needing follow-up resolution calls.
    /// </summary>
    private static string ApplyDeepResolution(string ilOutput)
    {
        // IL type abbreviation -> fully-qualified .NET type name mapping
        // These are the built-in IL type keywords that ReflectionDisassembler emits
        var typeExpansions = new (string pattern, string replacement)[]
        {
            // Order matters: longer patterns first to avoid partial matches
            (@"(?<=\W|^)unsigned int64(?=\W|$)", "System.UInt64"),
            (@"(?<=\W|^)unsigned int32(?=\W|$)", "System.UInt32"),
            (@"(?<=\W|^)unsigned int16(?=\W|$)", "System.UInt16"),
            (@"(?<=\W|^)unsigned int8(?=\W|$)", "System.Byte"),
            (@"(?<=\W|^)float64(?=\W|$)", "System.Double"),
            (@"(?<=\W|^)float32(?=\W|$)", "System.Single"),
            (@"(?<=\W|^)int64(?=\W|$)", "System.Int64"),
            (@"(?<=\W|^)int32(?=\W|$)", "System.Int32"),
            (@"(?<=\W|^)int16(?=\W|$)", "System.Int16"),
            (@"(?<=\W|^)int8(?=\W|$)", "System.SByte"),
            (@"(?<=\W|^)bool(?=\W|$)", "System.Boolean"),
            (@"(?<=\W|^)char(?=\W|$)", "System.Char"),
            (@"(?<=\W|^)void(?=\W|$)", "System.Void"),
        };

        // Process line by line to only expand types in operand/signature contexts,
        // not in opcode names or labels
        var lines = ilOutput.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Skip comment lines (summary header)
            if (line.TrimStart().StartsWith("//"))
                continue;

            // Apply 'string' -> 'System.String' and 'object' -> 'System.Object' carefully
            // to avoid matching inside identifiers. These appear as standalone IL type keywords.
            line = Regex.Replace(line, @"(?<=[\s\(\[,])string(?=[\s\)\],\r\n]|$)", "System.String");
            line = Regex.Replace(line, @"(?<=[\s\(\[,])object(?=[\s\)\],\r\n]|$)", "System.Object");

            // Apply other type expansions
            foreach (var (pattern, replacement) in typeExpansions)
            {
                line = Regex.Replace(line, pattern, replacement);
            }

            lines[i] = line;
        }

        return string.Join('\n', lines);
    }
}

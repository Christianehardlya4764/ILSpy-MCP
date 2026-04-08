using System.Text;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for retrieving assembly metadata including PE headers and references.
/// </summary>
public sealed class GetAssemblyMetadataUseCase
{
    private readonly IAssemblyInspectionService _inspection;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<GetAssemblyMetadataUseCase> _logger;

    public GetAssemblyMetadataUseCase(
        IAssemblyInspectionService inspection,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<GetAssemblyMetadataUseCase> logger)
    {
        _inspection = inspection;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);

            _logger.LogInformation("Getting assembly metadata for {Assembly}", assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var metadata = await _inspection.GetAssemblyMetadataAsync(assembly, timeout.Token);
                return FormatMetadata(metadata);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for assembly {Assembly}", assemblyPath);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for assembly {Assembly}", assemblyPath);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting assembly metadata for {Assembly}", assemblyPath);
            throw;
        }
    }

    private static string FormatMetadata(AssemblyMetadata metadata)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Assembly Metadata");
        sb.AppendLine();
        sb.AppendLine($"Name: {metadata.Name}");
        sb.AppendLine($"Version: {metadata.Version}");
        sb.AppendLine($"Target Framework: {metadata.TargetFramework ?? "N/A"}");
        sb.AppendLine($"Runtime Version: {metadata.RuntimeVersion ?? "N/A"}");
        sb.AppendLine($"PE Kind: {metadata.PEKind}");
        sb.AppendLine($"Strong Name: {metadata.StrongName ?? "None"}");
        sb.AppendLine($"Entry Point: {metadata.EntryPoint ?? "None"}");
        sb.AppendLine($"Culture: {metadata.Culture ?? "neutral"}");
        sb.AppendLine($"Public Key Token: {metadata.PublicKeyToken ?? "null"}");
        sb.AppendLine();
        sb.AppendLine($"## Referenced Assemblies ({metadata.References.Count})");
        sb.AppendLine();

        if (metadata.References.Count == 0)
        {
            sb.AppendLine("No referenced assemblies.");
        }
        else
        {
            foreach (var reference in metadata.References)
            {
                sb.AppendLine($"- {reference.Name}, Version={reference.Version}, Culture={reference.Culture ?? "neutral"}, PublicKeyToken={reference.PublicKeyToken ?? "null"}");
            }
        }

        return sb.ToString();
    }
}

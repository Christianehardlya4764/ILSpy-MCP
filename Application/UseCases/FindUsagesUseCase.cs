using System.Text;
using ILSpy.Mcp.Application.Pagination;
using ILSpy.Mcp.Application.Services;
using ILSpy.Mcp.Domain.Errors;
using ILSpy.Mcp.Domain.Models;
using ILSpy.Mcp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ILSpy.Mcp.Application.UseCases;

/// <summary>
/// Use case for finding all usage sites of a member (method, field, property) across an assembly.
/// </summary>
public sealed class FindUsagesUseCase
{
    private readonly ICrossReferenceService _crossRef;
    private readonly ITimeoutService _timeout;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<FindUsagesUseCase> _logger;

    public FindUsagesUseCase(
        ICrossReferenceService crossRef,
        ITimeoutService timeout,
        IConcurrencyLimiter limiter,
        ILogger<FindUsagesUseCase> logger)
    {
        _crossRef = crossRef;
        _timeout = timeout;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string assemblyPath,
        string typeName,
        string memberName,
        int maxResults = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = AssemblyPath.Create(assemblyPath);
            var type = TypeName.Create(typeName);

            _logger.LogInformation("Finding usages of {MemberName} in {TypeName} from {Assembly}",
                memberName, typeName, assemblyPath);

            return await _limiter.ExecuteAsync(async () =>
            {
                using var timeout = _timeout.CreateTimeoutToken(cancellationToken);
                var results = await _crossRef.FindUsagesAsync(assembly, type, memberName, timeout.Token);

                var sorted = results
                    .OrderBy(r => r.DeclaringType, StringComparer.Ordinal)
                    .ThenBy(r => r.ILOffset)
                    .ToList();
                var total = sorted.Count;
                var page = sorted.Skip(offset).Take(maxResults).ToList();
                return FormatResults(typeName, memberName, page, total, offset);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation cancelled for finding usages of {MemberName}", memberName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation timed out for finding usages of {MemberName}", memberName);
            throw new TimeoutException($"Operation timed out after {_timeout.GetDefaultTimeout().TotalSeconds} seconds");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error finding usages of {MemberName}", memberName);
            throw;
        }
    }

    private static string FormatResults(string typeName, string memberName, IReadOnlyList<UsageResult> page, int total, int offset)
    {
        var sb = new StringBuilder();

        // Header — three branches
        if (total == 0)
        {
            sb.AppendLine($"Usages of {typeName}.{memberName}: {total} found");
        }
        else if (page.Count == 0)
        {
            sb.AppendLine($"Usages of {typeName}.{memberName}: {total} found (offset {offset} is beyond last page)");
        }
        else
        {
            sb.AppendLine($"Usages of {typeName}.{memberName}: {total} found (showing {offset + 1}-{offset + page.Count})");
        }

        // Body — one line per match
        foreach (var result in page)
        {
            var signature = result.MethodSignature != null ? $" {result.MethodSignature}" : "";
            sb.AppendLine($"  [{result.Kind}] {result.DeclaringType}.{result.MethodName} (IL_{result.ILOffset:X4}){signature}");
        }

        // Footer — the parseable contract. ALWAYS present.
        PaginationEnvelope.AppendFooter(sb, total, page.Count, offset);

        return sb.ToString();
    }
}

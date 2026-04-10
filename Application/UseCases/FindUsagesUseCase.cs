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
                var total = results.Count;
                var page = results.Skip(offset).Take(maxResults).ToList();

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
        var sb = new System.Text.StringBuilder();
        var returned = page.Count;

        if (total == 0)
        {
            sb.AppendLine($"Usages of {typeName}.{memberName}: 0 found");
            sb.AppendLine();
            sb.AppendLine("No usages found in the assembly.");
            PaginationEnvelope.AppendFooter(sb, 0, 0, offset);
            return sb.ToString();
        }

        if (offset >= total)
        {
            sb.AppendLine($"Usages of {typeName}.{memberName}: {total} found (offset {offset} is beyond last page)");
            PaginationEnvelope.AppendFooter(sb, total, 0, offset);
            return sb.ToString();
        }

        var rangeStart = offset + 1;
        var rangeEnd = offset + returned;
        sb.AppendLine($"Usages of {typeName}.{memberName}: {total} found (showing {rangeStart}-{rangeEnd})");
        sb.AppendLine();

        foreach (var result in page)
        {
            sb.AppendLine($"  [{result.Kind}] {result.DeclaringType}.{result.MethodName} (IL_{result.ILOffset:X4})");
        }

        PaginationEnvelope.AppendFooter(sb, total, returned, offset);
        return sb.ToString();
    }
}

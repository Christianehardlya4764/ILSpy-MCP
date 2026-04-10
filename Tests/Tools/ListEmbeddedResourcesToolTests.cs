using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class ListEmbeddedResourcesToolTests
{
    private readonly ToolTestFixture _fixture;

    public ListEmbeddedResourcesToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListResources_TestAssembly_ContainsSampleTxt()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("sample.txt");
    }

    [Fact]
    public async Task ListResources_TestAssembly_ContainsSampleBin()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("sample.bin");
    }

    [Fact]
    public async Task ListResources_TestAssembly_ShowsResourceType()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Embedded");
    }

    [Fact]
    public async Task ListResources_TestAssembly_ShowsSize()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            cancellationToken: CancellationToken.None);

        // Size should contain numeric values (at least "bytes" with a number)
        result.Should().MatchRegex(@"\d+ bytes");
    }

    [Fact]
    public async Task ListResources_InvalidPath_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }

    // ===== Pagination tests (Phase 11 — PAGE-03 contract) =====

    [Fact]
    public async Task Pagination_DefaultReturnsFooter()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var result = await tool.ExecuteAsync(_fixture.TestAssemblyPath, cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
        result.Should().Contain("\"truncated\":");
    }

    [Fact]
    public async Task Pagination_MaxResultsCapsOutput()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var result = await tool.ExecuteAsync(_fixture.TestAssemblyPath, maxResults: 1, cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
        result.Should().Contain("\"returned\":1");
    }

    [Fact]
    public async Task Pagination_OffsetSkipsItems()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var defaultResult = await tool.ExecuteAsync(_fixture.TestAssemblyPath, maxResults: 1, cancellationToken: CancellationToken.None);
        var offsetResult = await tool.ExecuteAsync(_fixture.TestAssemblyPath, maxResults: 1, offset: 1, cancellationToken: CancellationToken.None);

        // The two single-item pages should show different resources
        var defaultResourceLines = defaultResult.Split('\n').Where(l => l.StartsWith("- ")).ToList();
        var offsetResourceLines = offsetResult.Split('\n').Where(l => l.StartsWith("- ")).ToList();

        defaultResourceLines.Should().HaveCount(1);
        offsetResourceLines.Should().HaveCount(1);
        defaultResourceLines[0].Should().NotBe(offsetResourceLines[0]);
    }

    [Fact]
    public async Task Pagination_TruncatedTrueWhenMoreExist()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var result = await tool.ExecuteAsync(_fixture.TestAssemblyPath, maxResults: 1, cancellationToken: CancellationToken.None);

        result.Should().Contain("\"truncated\":true");
    }

    [Fact]
    public async Task Pagination_ExceedingCapRejectsWithInvalidParameter()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var act = () => tool.ExecuteAsync(_fixture.TestAssemblyPath, maxResults: 501, cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain("500");
    }

    [Fact]
    public async Task Pagination_ZeroMaxResultsRejects()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var act = () => tool.ExecuteAsync(_fixture.TestAssemblyPath, maxResults: 0, cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain(">= 1");
    }

    [Fact]
    public async Task Pagination_NegativeMaxResultsRejects()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var act = () => tool.ExecuteAsync(_fixture.TestAssemblyPath, maxResults: -1, cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain(">= 1");
    }
}

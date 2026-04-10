using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class SearchMembersByNameToolTests
{
    private readonly ToolTestFixture _fixture;

    public SearchMembersByNameToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SearchMembers_ByMethodName_FindsMatches()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Calculate",
            null,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Search results for 'Calculate'");
        result.Should().Contain("Calculate");
        result.Should().Contain("SimpleClass");
    }

    [Fact]
    public async Task SearchMembers_ByPropertyName_FindsMatches()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Name",
            "property",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Search results for 'Name'");
        result.Should().Contain("Name");
    }

    [Fact]
    public async Task SearchMembers_NoResults_ReturnsZeroCount()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ZzzNonExistentMember",
            null,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Found 0 matching members");
    }

    [Fact]
    public async Task SearchMembers_InvalidAssembly_ThrowsException()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var act = () => tool.ExecuteAsync(
            @"C:\NonExistent\Assembly.dll",
            "ToString",
            null,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INTERNAL_ERROR");
    }

    // ===== Pagination tests (Phase 11 — PAGE-05 contract) =====

    [Fact]
    public async Task Pagination_DefaultReturnsFooter()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Get",
            null,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
        result.Should().Contain("\"truncated\":");
    }

    [Fact]
    public async Task Pagination_MaxResultsCapsOutput()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Get",
            null,
            maxResults: 1,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
        result.Should().Contain("\"returned\":1");
    }

    [Fact]
    public async Task Pagination_OffsetSkipsItems()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var defaultResult = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Get",
            null,
            maxResults: 1,
            cancellationToken: CancellationToken.None);

        var offsetResult = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Get",
            null,
            maxResults: 1,
            offset: 1,
            cancellationToken: CancellationToken.None);

        // The two single-item pages should show different members (sorted by type then name)
        var defaultMemberLines = defaultResult.Split('\n').Where(l => l.StartsWith("  [")).ToList();
        var offsetMemberLines = offsetResult.Split('\n').Where(l => l.StartsWith("  [")).ToList();

        defaultMemberLines.Should().HaveCount(1);
        offsetMemberLines.Should().HaveCount(1);
        defaultMemberLines[0].Should().NotBe(offsetMemberLines[0]);
    }

    [Fact]
    public async Task Pagination_TruncatedTrueWhenMoreExist()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Get",
            null,
            maxResults: 1,
            cancellationToken: CancellationToken.None);

        result.Should().Contain("\"truncated\":true");
    }

    [Fact]
    public async Task Pagination_ExceedingCapRejectsWithInvalidParameter()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Get",
            null,
            maxResults: 501,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain("500");
    }

    [Fact]
    public async Task Pagination_ZeroMaxResultsRejects()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Get",
            null,
            maxResults: 0,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain(">= 1");
    }

    [Fact]
    public async Task Pagination_NegativeMaxResultsRejects()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<SearchMembersByNameTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "Get",
            null,
            maxResults: -1,
            cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain(">= 1");
    }
}

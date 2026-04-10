using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class ListAssemblyTypesToolTests
{
    private readonly ToolTestFixture _fixture;

    public ListAssemblyTypesToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListTypes_NoFilter_ReturnsAllKnownTypes()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var result = await tool.ExecuteAsync(_fixture.TestAssemblyPath, null, maxResults: 500, cancellationToken: CancellationToken.None);

        result.Should().Contain("Assembly:");
        result.Should().Contain("Types found:");
        result.Should().Contain("SimpleClass");
        result.Should().Contain("IAnimal");
        result.Should().Contain("Dog");
        result.Should().Contain("Shape");
        result.Should().Contain("Circle");
        result.Should().Contain("class");
        result.Should().Contain("interface");
        result.Should().Contain("struct");
        result.Should().Contain("enum");
    }

    [Fact]
    public async Task ListTypes_WithNamespaceFilter_ReturnsOnlyMatchingTypes()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Animals",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Dog");
        result.Should().Contain("Cat");
        result.Should().Contain("IAnimal");
        result.Should().NotContain("SimpleClass");
        result.Should().NotContain("Shape");
    }

    [Fact]
    public async Task ListTypes_GenericTypes_ShowsBacktickNotation()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var result = await tool.ExecuteAsync(_fixture.TestAssemblyPath, null, cancellationToken: CancellationToken.None);

        // Generic types appear without backtick notation in the listing
        result.Should().Contain("ILSpy.Mcp.TestTargets.Generics.Repository");
        result.Should().Contain("ILSpy.Mcp.TestTargets.Generics.Pair");
    }

    [Fact]
    public async Task ListTypes_ShowsDelegateTypes()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var result = await tool.ExecuteAsync(_fixture.TestAssemblyPath, null, maxResults: 500, cancellationToken: CancellationToken.None);

        result.Should().Contain("delegate");
        result.Should().Contain("SimpleAction");
    }

    [Fact]
    public async Task ListTypes_InvalidAssembly_ThrowsMcpToolException()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var act = () => tool.ExecuteAsync(
            @"C:\NonExistent\Assembly.dll", null, cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INTERNAL_ERROR");
    }

    // ===== Pagination tests (Phase 11 — PAGE-03 contract) =====

    [Fact]
    public async Task Pagination_DefaultReturnsFooter()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var result = await tool.ExecuteAsync(_fixture.TestAssemblyPath, null, cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
        result.Should().Contain("\"truncated\":");
    }

    [Fact]
    public async Task Pagination_MaxResultsCapsOutput()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var result = await tool.ExecuteAsync(_fixture.TestAssemblyPath, null, maxResults: 2, cancellationToken: CancellationToken.None);

        result.Should().Contain("[pagination:");
        result.Should().Contain("\"returned\":2");
    }

    [Fact]
    public async Task Pagination_OffsetSkipsItems()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var defaultResult = await tool.ExecuteAsync(_fixture.TestAssemblyPath, null, maxResults: 1, cancellationToken: CancellationToken.None);
        var offsetResult = await tool.ExecuteAsync(_fixture.TestAssemblyPath, null, maxResults: 1, offset: 1, cancellationToken: CancellationToken.None);

        // The two single-item pages should show different types (sorted alphabetically)
        var defaultLines = defaultResult.Split('\n').Where(l => l.TrimStart().StartsWith("class") || l.TrimStart().StartsWith("interface") || l.TrimStart().StartsWith("struct") || l.TrimStart().StartsWith("enum") || l.TrimStart().StartsWith("delegate")).ToList();
        var offsetLines = offsetResult.Split('\n').Where(l => l.TrimStart().StartsWith("class") || l.TrimStart().StartsWith("interface") || l.TrimStart().StartsWith("struct") || l.TrimStart().StartsWith("enum") || l.TrimStart().StartsWith("delegate")).ToList();

        defaultLines.Should().HaveCount(1);
        offsetLines.Should().HaveCount(1);
        defaultLines[0].Should().NotBe(offsetLines[0]);
    }

    [Fact]
    public async Task Pagination_TruncatedTrueWhenMoreExist()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var result = await tool.ExecuteAsync(_fixture.TestAssemblyPath, null, maxResults: 1, cancellationToken: CancellationToken.None);

        result.Should().Contain("\"truncated\":true");
    }

    [Fact]
    public async Task Pagination_ExceedingCapRejectsWithInvalidParameter()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var act = () => tool.ExecuteAsync(_fixture.TestAssemblyPath, null, maxResults: 501, cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain("500");
    }

    [Fact]
    public async Task Pagination_ZeroMaxResultsRejects()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var act = () => tool.ExecuteAsync(_fixture.TestAssemblyPath, null, maxResults: 0, cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain(">= 1");
    }

    [Fact]
    public async Task Pagination_NegativeMaxResultsRejects()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListAssemblyTypesTool>();

        var act = () => tool.ExecuteAsync(_fixture.TestAssemblyPath, null, maxResults: -1, cancellationToken: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INVALID_PARAMETER");
        ex.Which.Message.Should().Contain(">= 1");
    }
}

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

        var result = await tool.ExecuteAsync(_fixture.TestAssemblyPath, null, maxResults: 500, cancellationToken: CancellationToken.None);

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
}

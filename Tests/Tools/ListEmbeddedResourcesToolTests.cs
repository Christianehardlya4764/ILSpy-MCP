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
            CancellationToken.None);

        result.Should().Contain("sample.txt");
    }

    [Fact]
    public async Task ListResources_TestAssembly_ContainsSampleBin()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        result.Should().Contain("sample.bin");
    }

    [Fact]
    public async Task ListResources_TestAssembly_ShowsResourceType()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        result.Should().Contain("Embedded");
    }

    [Fact]
    public async Task ListResources_TestAssembly_ShowsSize()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ListEmbeddedResourcesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

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
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }
}

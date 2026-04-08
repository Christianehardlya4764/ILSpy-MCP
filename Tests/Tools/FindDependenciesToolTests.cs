using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class FindDependenciesToolTests
{
    private readonly ToolTestFixture _fixture;

    public FindDependenciesToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindDependencies_SpecificMethod_ReturnsDeps()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.DataService",
            "ProcessData",
            CancellationToken.None);

        result.Should().Contain("Dependencies of");
        result.Should().Contain("Save");
    }

    [Fact]
    public async Task FindDependencies_TypeLevel_ReturnsAllMethodDeps()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.DataService",
            null,
            CancellationToken.None);

        result.Should().Contain("Dependencies of");
        // Should include deps from all methods
        result.Should().Contain("Save");
        result.Should().Contain("Load");
    }

    [Fact]
    public async Task FindDependencies_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindDependenciesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            null,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }
}

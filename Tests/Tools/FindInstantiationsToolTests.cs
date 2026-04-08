using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class FindInstantiationsToolTests
{
    private readonly ToolTestFixture _fixture;

    public FindInstantiationsToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindInstantiations_KnownType_ReturnsNewObjSites()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.FileRepository",
            CancellationToken.None);

        result.Should().Contain("Instantiations of");
        result.Should().Contain("DataService");
        result.Should().Contain("FileProcessor");
    }

    [Fact]
    public async Task FindInstantiations_DatabaseRepository_ReturnsNewObjSites()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.DatabaseRepository",
            CancellationToken.None);

        result.Should().Contain("Instantiations of");
        result.Should().Contain("FileProcessor");
    }

    [Fact]
    public async Task FindInstantiations_NeverInstantiated_ReturnsEmpty()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        // IRepository is an interface, never constructed directly
        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.CrossRef.IRepository",
            CancellationToken.None);

        result.Should().Contain("0 found");
        result.Should().Contain("No instantiation sites found");
    }

    [Fact]
    public async Task FindInstantiations_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindInstantiationsTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }
}

using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class FindTypeHierarchyToolTests
{
    private readonly ToolTestFixture _fixture;

    public FindTypeHierarchyToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindTypeHierarchy_AdminUser_ShowsFullChain()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindTypeHierarchyTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.AdminUser",
            CancellationToken.None);

        result.Should().Contain("Type Hierarchy:");
        result.Should().Contain("AdminUser");
        result.Should().Contain("Inherits from:");
        // The hierarchy shows the direct base type
        result.Should().Contain("User");
    }

    [Fact]
    public async Task FindTypeHierarchy_Dog_ShowsInterfaceImplementation()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindTypeHierarchyTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Animals.Dog",
            CancellationToken.None);

        result.Should().Contain("Type Hierarchy:");
        result.Should().Contain("Dog");
        result.Should().Contain("Implements interfaces:");
        result.Should().Contain("IAnimal");
    }

    [Fact]
    public async Task FindTypeHierarchy_Circle_ShowsAbstractBase()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindTypeHierarchyTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Shapes.Circle",
            CancellationToken.None);

        result.Should().Contain("Type Hierarchy:");
        result.Should().Contain("Circle");
        result.Should().Contain("Shape");
    }

    [Fact]
    public async Task FindTypeHierarchy_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindTypeHierarchyTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }
}

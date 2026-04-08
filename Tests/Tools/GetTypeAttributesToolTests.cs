using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class GetTypeAttributesToolTests
{
    private readonly ToolTestFixture _fixture;

    public GetTypeAttributesToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetTypeAttributes_AttributedClass_ReturnsAttributes()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeAttributesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.AttributedClass",
            CancellationToken.None);

        // SerializableAttribute is a pseudo-attribute (stored in metadata flags, not as CustomAttribute)
        // CustomInfoAttribute is a real custom attribute
        result.Should().Contain("Attributes");
        result.Should().Contain("CustomInfoAttribute");
    }

    [Fact]
    public async Task GetTypeAttributes_AttributedClass_ReturnsCustomInfo()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeAttributesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.AttributedClass",
            CancellationToken.None);

        result.Should().Contain("CustomInfoAttribute");
        result.Should().Contain("Test class");
    }

    [Fact]
    public async Task GetTypeAttributes_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeAttributesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }

    [Fact]
    public async Task GetTypeAttributes_InvalidPath_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetTypeAttributesTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            "SomeType",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }
}

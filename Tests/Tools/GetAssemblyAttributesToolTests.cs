using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class GetAssemblyAttributesToolTests
{
    private readonly ToolTestFixture _fixture;

    public GetAssemblyAttributesToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetAssemblyAttributes_TestAssembly_ReturnsDescriptionAttribute()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetAssemblyAttributesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        result.Should().Contain("AssemblyDescriptionAttribute");
        result.Should().Contain("Test assembly for ILSpy MCP");
    }

    [Fact]
    public async Task GetAssemblyAttributes_TestAssembly_ReturnsCompanyAttribute()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetAssemblyAttributesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        result.Should().Contain("AssemblyCompanyAttribute");
        result.Should().Contain("TestCompany");
    }

    [Fact]
    public async Task GetAssemblyAttributes_TestAssembly_ReturnsTargetFrameworkAttribute()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetAssemblyAttributesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        result.Should().Contain("TargetFrameworkAttribute");
    }

    [Fact]
    public async Task GetAssemblyAttributes_InvalidPath_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetAssemblyAttributesTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }
}

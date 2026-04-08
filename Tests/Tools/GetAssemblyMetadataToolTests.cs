using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class GetAssemblyMetadataToolTests
{
    private readonly ToolTestFixture _fixture;

    public GetAssemblyMetadataToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetAssemblyMetadata_TestAssembly_ReturnsName()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetAssemblyMetadataTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        result.Should().Contain("ILSpy.Mcp.TestTargets");
    }

    [Fact]
    public async Task GetAssemblyMetadata_TestAssembly_ReturnsPEKind()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetAssemblyMetadataTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        result.Should().Contain("AnyCPU");
    }

    [Fact]
    public async Task GetAssemblyMetadata_TestAssembly_ReturnsTargetFramework()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetAssemblyMetadataTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        result.Should().Contain(".NETCoreApp");
    }

    [Fact]
    public async Task GetAssemblyMetadata_TestAssembly_ReturnsReferences()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetAssemblyMetadataTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        result.Should().Contain("System.Runtime");
    }

    [Fact]
    public async Task GetAssemblyMetadata_InvalidPath_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetAssemblyMetadataTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }
}

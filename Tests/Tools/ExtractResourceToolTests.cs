using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class ExtractResourceToolTests
{
    private readonly ToolTestFixture _fixture;

    public ExtractResourceToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ExtractResource_TextResource_ReturnsTextContent()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ExtractResourceTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Resources.sample.txt",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("Hello from embedded resource!");
    }

    [Fact]
    public async Task ExtractResource_TextResource_ContentTypeIsText()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ExtractResourceTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Resources.sample.txt",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("text");
    }

    [Fact]
    public async Task ExtractResource_BinaryResource_ReturnsBase64()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ExtractResourceTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Resources.sample.bin",
            cancellationToken: CancellationToken.None);

        result.Should().NotBeEmpty();
        // Base64 characters
        result.Should().MatchRegex(@"[A-Za-z0-9+/=]");
    }

    [Fact]
    public async Task ExtractResource_BinaryResource_ContentTypeIsBinary()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ExtractResourceTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Resources.sample.bin",
            cancellationToken: CancellationToken.None);

        result.Should().Contain("binary");
    }

    [Fact]
    public async Task ExtractResource_WithOffsetAndLimit_ReturnsPaginatedContent()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ExtractResourceTool>();

        var paginatedResult = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.Resources.sample.bin",
            offset: 0,
            limit: 4,
            cancellationToken: CancellationToken.None);

        // Paginated result should include offset/length metadata
        paginatedResult.Should().Contain("Offset:");
        paginatedResult.Should().Contain("Length:");
    }

    [Fact]
    public async Task ExtractResource_NonExistentResource_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<ExtractResourceTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "nonexistent.txt",
            cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<McpToolException>();
    }
}

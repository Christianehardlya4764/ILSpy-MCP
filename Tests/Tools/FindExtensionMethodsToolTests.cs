using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class FindExtensionMethodsToolTests
{
    private readonly ToolTestFixture _fixture;

    public FindExtensionMethodsToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindExtensionMethods_StringType_FindsExtensions()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "System.String",
            CancellationToken.None);

        result.Should().Contain("Extension methods for type: System.String");
        result.Should().Contain("Reverse");
        result.Should().Contain("IsPalindrome");
        result.Should().Contain("Truncate");
    }

    [Fact]
    public async Task FindExtensionMethods_TypeWithNoExtensions_ReturnsEmpty()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "System.Int32",
            CancellationToken.None);

        result.Should().Contain("No extension methods found");
    }

    [Fact]
    public async Task FindExtensionMethods_InvalidAssembly_ThrowsException()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindExtensionMethodsTool>();

        var act = () => tool.ExecuteAsync(
            @"C:\NonExistent\Assembly.dll",
            "System.String",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("INTERNAL_ERROR");
    }
}

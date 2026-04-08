using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class FindCompilerGeneratedTypesToolTests
{
    private readonly ToolTestFixture _fixture;

    public FindCompilerGeneratedTypesToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindCompilerGenerated_TestAssembly_FindsAsyncStateMachine()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        // Async state machines contain "d__" in their name
        result.Should().MatchRegex(@"d__\d+");
    }

    [Fact]
    public async Task FindCompilerGenerated_TestAssembly_FindsDisplayClass()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        result.Should().Contain("DisplayClass");
    }

    [Fact]
    public async Task FindCompilerGenerated_TestAssembly_ShowsParentMethod()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        result.Should().Contain("DoWorkAsync");
    }

    [Fact]
    public async Task FindCompilerGenerated_TestAssembly_ShowsParentType()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            CancellationToken.None);

        // Should show AsyncExample or LambdaExample as parent type
        result.Should().Match(r => r.Contains("AsyncExample") || r.Contains("LambdaExample"));
    }

    [Fact]
    public async Task FindCompilerGenerated_InvalidPath_ThrowsError()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<FindCompilerGeneratedTypesTool>();

        var act = () => tool.ExecuteAsync(
            "nonexistent.dll",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().BeOneOf("ASSEMBLY_LOAD_FAILED", "INTERNAL_ERROR");
    }
}

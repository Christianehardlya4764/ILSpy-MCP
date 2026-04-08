using FluentAssertions;
using ILSpy.Mcp.Tests.Fixtures;
using ILSpy.Mcp.Transport.Mcp.Errors;
using ILSpy.Mcp.Transport.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ILSpy.Mcp.Tests.Tools;

[Collection("ToolTests")]
public class GetMemberAttributesToolTests
{
    private readonly ToolTestFixture _fixture;

    public GetMemberAttributesToolTests(ToolTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetMemberAttributes_OldMethod_ReturnsObsolete()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetMemberAttributesTool>();

        var result = await tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.AttributedClass",
            "OldMethod",
            CancellationToken.None);

        result.Should().Contain("ObsoleteAttribute");
        result.Should().Contain("Use NewMethod instead");
    }

    [Fact]
    public async Task GetMemberAttributes_NonExistentMember_ThrowsMemberNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetMemberAttributesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "ILSpy.Mcp.TestTargets.AttributedClass",
            "NonExistentMember",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task GetMemberAttributes_NonExistentType_ThrowsTypeNotFound()
    {
        using var scope = _fixture.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<GetMemberAttributesTool>();

        var act = () => tool.ExecuteAsync(
            _fixture.TestAssemblyPath,
            "NonExistent.Type",
            "SomeMember",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpToolException>();
        ex.Which.ErrorCode.Should().Be("TYPE_NOT_FOUND");
    }
}

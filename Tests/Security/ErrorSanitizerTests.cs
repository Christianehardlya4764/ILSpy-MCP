using FluentAssertions;
using ILSpy.Mcp.Transport.Mcp.Errors;
using Xunit;

namespace ILSpy.Mcp.Tests.Security;

public class ErrorSanitizerTests
{
    [Fact]
    public void SanitizePath_WindowsPath_StripsDirectoryKeepsFilename()
    {
        var result = ErrorSanitizer.SanitizePath(@"Failed to load C:\Users\admin\secrets\myapp.dll");

        result.Should().Be("Failed to load myapp.dll");
    }

    [Fact]
    public void SanitizePath_UnixPath_StripsDirectoryKeepsFilename()
    {
        var result = ErrorSanitizer.SanitizePath("Error at /opt/app/bin/assembly.dll");

        result.Should().Be("Error at assembly.dll");
    }

    [Fact]
    public void SanitizePath_NoPathsInMessage_ReturnsUnchanged()
    {
        var result = ErrorSanitizer.SanitizePath("Simple error message");

        result.Should().Be("Simple error message");
    }

    [Fact]
    public void SanitizePath_MultiplePaths_SanitizesBoth()
    {
        var result = ErrorSanitizer.SanitizePath(
            @"Could not resolve C:\libs\foo.dll referenced by /opt/assemblies/bar.dll");

        result.Should().Contain("foo.dll");
        result.Should().Contain("bar.dll");
        result.Should().NotContain("libs");
        result.Should().NotContain("assemblies");
    }

    [Fact]
    public void SanitizePath_EmptyString_ReturnsEmpty()
    {
        var result = ErrorSanitizer.SanitizePath("");

        result.Should().BeEmpty();
    }
}

using FluentAssertions;
using ILSpy.Mcp.Application.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ILSpy.Mcp.Tests.Security;

public class ILSpyOptionsValidatorTests
{
    [Fact]
    public void Validate_DefaultOptions_DoesNotThrow()
    {
        var options = new ILSpyOptions();

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MaxDecompilationSize_ZeroOrNegative_Throws(int value)
    {
        var options = new ILSpyOptions { MaxDecompilationSize = value };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be(nameof(ILSpyOptions.MaxDecompilationSize));
    }

    [Fact]
    public void Validate_MaxDecompilationSize_Over500MB_Throws()
    {
        var options = new ILSpyOptions { MaxDecompilationSize = 500_000_001 };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be(nameof(ILSpyOptions.MaxDecompilationSize));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_DefaultTimeoutSeconds_ZeroOrNegative_Throws(int value)
    {
        var options = new ILSpyOptions { DefaultTimeoutSeconds = value };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be(nameof(ILSpyOptions.DefaultTimeoutSeconds));
    }

    [Fact]
    public void Validate_DefaultTimeoutSeconds_Over3600_Throws()
    {
        var options = new ILSpyOptions { DefaultTimeoutSeconds = 3601 };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be(nameof(ILSpyOptions.DefaultTimeoutSeconds));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MaxConcurrentOperations_ZeroOrNegative_Throws(int value)
    {
        var options = new ILSpyOptions { MaxConcurrentOperations = value };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be(nameof(ILSpyOptions.MaxConcurrentOperations));
    }

    [Fact]
    public void Validate_MaxConcurrentOperations_Over100_Throws()
    {
        var options = new ILSpyOptions { MaxConcurrentOperations = 101 };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be(nameof(ILSpyOptions.MaxConcurrentOperations));
    }

    [Fact]
    public void OptionsValidator_ValidOptions_ReturnsSuccess()
    {
        var validator = new ILSpyOptionsValidator();
        var options = new ILSpyOptions();

        var result = validator.Validate(null, options);

        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void OptionsValidator_InvalidOptions_ReturnsFailure()
    {
        var validator = new ILSpyOptionsValidator();
        var options = new ILSpyOptions { MaxDecompilationSize = -1 };

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxDecompilationSize");
    }
}

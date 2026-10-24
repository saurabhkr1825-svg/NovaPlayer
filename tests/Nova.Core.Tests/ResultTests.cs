using FluentAssertions;
using Nova.Core.Common;
using Xunit;

namespace Nova.Core.Tests;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailedResultWithError()
    {
        var result = Result.Failure("File not found");

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("File not found");
    }

    [Fact]
    public void TypedResult_Success_ShouldReturnValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void TypedResult_Failure_AccessingValueShouldThrow()
    {
        var result = Result<int>.Failure("Invalid operation");

        result.IsFailure.Should().BeTrue();
        var act = () => _ = result.Value;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid operation*");
    }
}

using FluentAssertions;
using Nova.Core.Common;
using Xunit;

namespace Nova.Core.Tests;

public class TimeFormatterTests
{
    [Theory]
    [InlineData(-1000, "00:00")]
    [InlineData(0, "00:00")]
    [InlineData(5000, "00:05")]
    [InlineData(65000, "01:05")]
    [InlineData(599000, "09:59")]
    [InlineData(3600000, "01:00:00")]
    [InlineData(3665000, "01:01:05")]
    [InlineData(7325000, "02:02:05")]
    public void FormatMilliseconds_ShouldFormatCorrectly(long ms, string expected)
    {
        var formatted = TimeFormatter.FormatMilliseconds(ms);
        formatted.Should().Be(expected);
    }

    [Fact]
    public void FormatTimeSpan_WithHours_ShouldIncludeHours()
    {
        var span = new TimeSpan(1, 23, 45);
        var formatted = TimeFormatter.FormatTimeSpan(span);
        formatted.Should().Be("01:23:45");
    }

    [Fact]
    public void FormatTimeSpan_UnderOneHour_ShouldOnlyIncludeMinutesAndSeconds()
    {
        var span = new TimeSpan(0, 4, 12);
        var formatted = TimeFormatter.FormatTimeSpan(span);
        formatted.Should().Be("04:12");
    }
}

using FluentAssertions;
using Nova.Infrastructure.Logging;
using Xunit;

namespace Nova.Infrastructure.Tests;

public class LoggingTests
{
    [Fact]
    public void ConfigureLogging_ShouldCreateLogDirectory()
    {
        LoggingConfigurator.ConfigureLogging();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDirectory = Path.Combine(localAppData, "NovaPlayer", "logs");

        Directory.Exists(logDirectory).Should().BeTrue();
    }
}

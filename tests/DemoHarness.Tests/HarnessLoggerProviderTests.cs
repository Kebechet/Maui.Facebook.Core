using DemoApp.Harness;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace DemoHarness.Tests;

public class HarnessLoggerProviderTests
{
    [Fact]
    public void Information_IsLogged_ButNotCountedAsAProblem()
    {
        // Arrange
        var log = new HarnessLog();
        var provider = new HarnessLoggerProvider(log);
        var logger = provider.CreateLogger("Maui.Facebook.Core.Services.FacebookCoreService");

        // Act
        logger.LogInformation("Processing {what}", "something");

        // Assert
        log.Lines.Count.ShouldBe(1);
        log.Lines[0].ShouldContain("[Information] FacebookCoreService: Processing something");
        provider.ProblemCount.ShouldBe(0);
        provider.LastProblem.ShouldBeNull();
    }

    [Theory]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void WarningAndAbove_CountAsProblems_AndKeepTheLastMessage(LogLevel level)
    {
        // Arrange
        var log = new HarnessLog();
        var provider = new HarnessLoggerProvider(log);
        var logger = provider.CreateLogger("Cat");

        // Act
        logger.Log(level, new InvalidOperationException("boom"), "{methodName} error in Facebook SDK", "Flush");

        // Assert
        provider.ProblemCount.ShouldBe(1);
        provider.LastProblem.ShouldBe("Flush error in Facebook SDK -> InvalidOperationException: boom");
        log.Lines[0].ShouldStartWith(DateTime.Now.ToString("HH:mm:ss").Substring(0, 5));
        log.Lines[0].ShouldContain($"[{level}] Cat:");
    }

    [Fact]
    public void Debug_IsBelowTheThreshold_AndIgnored()
    {
        // Arrange
        var log = new HarnessLog();
        var provider = new HarnessLoggerProvider(log);
        var logger = provider.CreateLogger("Cat");

        // Act
        logger.LogDebug("noise");
        logger.LogTrace("more noise");

        // Assert
        logger.IsEnabled(LogLevel.Debug).ShouldBeFalse();
        log.Lines.ShouldBeEmpty();
    }

    [Fact]
    public void Category_IsShortenedToItsLastSegment()
    {
        // Arrange
        var log = new HarnessLog();
        var logger = new HarnessLoggerProvider(log).CreateLogger("A.Very.Long.Namespace.TypeName");

        // Act
        logger.LogInformation("x");

        // Assert
        log.Lines[0].ShouldContain(" TypeName: x");
        log.Lines[0].ShouldNotContain("Namespace");
    }
}

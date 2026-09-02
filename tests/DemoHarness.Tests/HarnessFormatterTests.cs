using DemoApp.Harness;
using Shouldly;
using Xunit;

namespace DemoHarness.Tests;

public class HarnessFormatterTests
{
    [Theory]
    [InlineData(HarnessCheckStatus.Passed, "✓")]
    [InlineData(HarnessCheckStatus.Failed, "✗")]
    [InlineData(HarnessCheckStatus.TimedOut, "⏱")]
    [InlineData(HarnessCheckStatus.Skipped, "–")]
    [InlineData(HarnessCheckStatus.Running, "…")]
    public void Glyph_IsDistinctPerStatus(HarnessCheckStatus status, string expected)
    {
        // Act
        var glyph = HarnessFormatter.Glyph(status);

        // Assert
        glyph.ShouldBe(expected);
    }

    [Fact]
    public void Summary_CountsTimedOutAsFailed()
    {
        // Arrange
        var results = new List<HarnessCheckResult>
        {
            new() { Name = "a", Status = HarnessCheckStatus.Passed },
            new() { Name = "b", Status = HarnessCheckStatus.Passed },
            new() { Name = "c", Status = HarnessCheckStatus.Failed },
            new() { Name = "d", Status = HarnessCheckStatus.TimedOut },
            new() { Name = "e", Status = HarnessCheckStatus.Skipped },
        };

        // Act
        var summary = HarnessFormatter.Summary(results);

        // Assert
        summary.ShouldBe("2 passed, 2 failed, 1 skipped of 5");
    }

    [Fact]
    public void Log_Clear_RaisesChanged()
    {
        // Arrange
        var log = new HarnessLog();
        var raised = 0;
        log.Changed += () => raised++;

        // Act
        log.Add("one");
        log.Clear();

        // Assert
        raised.ShouldBe(2);
        log.Lines.ShouldBeEmpty();
    }
}

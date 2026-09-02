using DemoApp.Harness;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace DemoHarness.Tests;

public class HarnessRunnerTests
{
    private const string AppId = "123456789012345";
    private const string ClientToken = "0123456789abcdef0123456789abcdef";

    private static (HarnessRunner Runner, FakeFacebookCoreService Fake, HarnessLog Log) Build(Action<FakeFacebookCoreService>? configure = null, TimeSpan? timeout = null)
    {
        var log = new HarnessLog();
        var provider = new HarnessLoggerProvider(log);
        var fake = new FakeFacebookCoreService(provider.CreateLogger("Maui.Facebook.Core.Services.FacebookCoreService"));
        configure?.Invoke(fake);
        var runner = new HarnessRunner(fake, log, provider, timeout);
        return (runner, fake, log);
    }

    [Fact]
    public async Task HealthySdk_EveryCheckPasses()
    {
        // Arrange
        var (runner, _, _) = Build();

        // Act
        var results = await runner.RunAllChecksAsync(AppId, ClientToken);

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(x => x.Status == HarnessCheckStatus.Passed, string.Join("\n", results.Where(x => x.Status != HarnessCheckStatus.Passed).Select(x => $"{x.Name}: {x.Error}")));
    }

    [Fact]
    public async Task InitializeRunsFirst_AndEveryMemberIsExercised()
    {
        // Arrange
        var (runner, fake, _) = Build();

        // Act
        var results = await runner.RunAllChecksAsync(AppId, ClientToken);

        // Assert
        results[0].Name.ShouldBe(nameof(FakeFacebookCoreService.Initialize));
        fake.Calls.First().ShouldBe(nameof(FakeFacebookCoreService.Initialize));
        foreach (var member in new[] { "SdkVersion", "AnonymousId", "UserId", "SetAutoLogAppEventsEnabled", "SetAdvertiserIdCollectionEnabled", "SetAdvertiserTrackingEnabled", "LogEvent", "LogPurchase", "Flush" })
        {
            fake.Calls.ShouldContain(member);
        }
    }

    [Fact]
    public async Task AnonymousIdCheck_RecordsTheObservedValue()
    {
        // Arrange
        var (runner, fake, _) = Build(x => x.AnonymousIdToReturn = "XZ00000000-1111-2222-3333-444444444444");

        // Act
        await runner.RunAllChecksAsync(AppId, ClientToken);

        // Assert
        runner.ObservedAnonymousId.ShouldBe(fake.AnonymousIdToReturn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    public async Task AnonymousIdCheck_FailsWhenTheSdkReturnsNothingUsable(string? anonymousId)
    {
        // Arrange
        var (runner, _, _) = Build(x => x.AnonymousIdToReturn = anonymousId);

        // Act
        var results = await runner.RunAllChecksAsync(AppId, ClientToken);

        // Assert
        var check = results.Single(x => x.Name == "AnonymousId");
        check.Status.ShouldBe(HarnessCheckStatus.Failed);
        check.Error.ShouldContain("expected a persisted");
    }

    [Fact]
    public async Task AnonymousIdStabilityCheck_FailsWhenTheValueChangesBetweenReads()
    {
        // Arrange
        var (runner, _, _) = Build(x => x.AnonymousIdChangesBetweenReads = true);

        // Act
        var results = await runner.RunAllChecksAsync(AppId, ClientToken);

        // Assert
        results.Single(x => x.Name == "AnonymousId").Status.ShouldBe(HarnessCheckStatus.Passed);
        var stability = results.Single(x => x.Name == "AnonymousId is stable");
        stability.Status.ShouldBe(HarnessCheckStatus.Failed);
        stability.Error.ShouldContain("second read returned");
    }

    [Fact]
    public async Task UserIdRoundTrip_FailsWhenClearDoesNotClear()
    {
        // Arrange
        var (runner, _, _) = Build(x => x.ClearUserIdIsBroken = true);

        // Act
        var results = await runner.RunAllChecksAsync(AppId, ClientToken);

        // Assert
        var check = results.Single(x => x.Name.StartsWith("UserId"));
        check.Status.ShouldBe(HarnessCheckStatus.Failed);
        check.Error.ShouldContain("cleared read back 'harness-user'");
    }

    [Fact]
    public async Task ACheckFails_WhenTheWrapperLogsAnErrorWhileItRuns()
    {
        // The wrapper swallows native exceptions and logs them, so this is the only signal a broken
        // LogPurchase would give. The runner has to turn it into a failure.
        // Arrange
        var (runner, _, _) = Build(x => x.MembersThatLogErrors.Add(nameof(FakeFacebookCoreService.LogPurchase)));

        // Act
        var results = await runner.RunAllChecksAsync(AppId, ClientToken);

        // Assert
        var purchase = results.Single(x => x.Name == nameof(FakeFacebookCoreService.LogPurchase));
        purchase.Status.ShouldBe(HarnessCheckStatus.Failed);
        purchase.Error.ShouldContain("native SDK exploded");
        results.Where(x => x.Name != nameof(FakeFacebookCoreService.LogPurchase)).ShouldAllBe(x => x.Status == HarnessCheckStatus.Passed);
    }

    [Fact]
    public async Task NotImplementedMember_IsReportedAsSkipped_NotFailed()
    {
        // Arrange
        var (runner, _, _) = Build(x => x.MembersNotImplemented.Add(nameof(FakeFacebookCoreService.Flush)));

        // Act
        var results = await runner.RunAllChecksAsync(AppId, ClientToken);

        // Assert
        var flush = results.Single(x => x.Name == nameof(FakeFacebookCoreService.Flush));
        flush.Status.ShouldBe(HarnessCheckStatus.Skipped);
        flush.Summary.ShouldContain("not supported on this platform");
    }

    [Fact]
    public async Task HangingMember_IsReportedAsTimedOut_AndTheSweepContinues()
    {
        // Arrange
        var (runner, _, _) = Build(x => x.MembersThatHang.Add(nameof(FakeFacebookCoreService.Flush)), timeout: TimeSpan.FromMilliseconds(200));

        // Act
        var results = await runner.RunAllChecksAsync(AppId, ClientToken);

        // Assert
        var flush = results.Single(x => x.Name == nameof(FakeFacebookCoreService.Flush));
        flush.Status.ShouldBe(HarnessCheckStatus.TimedOut);
        flush.Error.ShouldContain("No response within");
        results.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task Progress_ReportsRunningThenFinal_ForEveryCheck()
    {
        // Arrange
        var (runner, _, _) = Build();
        var reports = new List<HarnessCheckResult>();
        var progress = new SynchronousProgress<HarnessCheckResult>(reports.Add);

        // Act
        var results = await runner.RunAllChecksAsync(AppId, ClientToken, progress);

        // Assert
        reports.Count.ShouldBe(results.Count * 2);
        foreach (var result in results)
        {
            var forThisCheck = reports.Where(x => x.Name == result.Name).ToList();
            forThisCheck[0].Status.ShouldBe(HarnessCheckStatus.Running);
            forThisCheck[1].Status.ShouldBe(result.Status);
        }
    }

    [Fact]
    public async Task Log_ContainsOneVerdictLinePerCheck()
    {
        // Arrange
        var (runner, _, log) = Build();

        // Act
        var results = await runner.RunAllChecksAsync(AppId, ClientToken);

        // Assert
        log.Lines.Count(x => x.Contains(" PASS ")).ShouldBe(results.Count);
    }

    /// <summary><see cref="Progress{T}"/> posts to a SynchronizationContext; tests want the callback inline.</summary>
    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}

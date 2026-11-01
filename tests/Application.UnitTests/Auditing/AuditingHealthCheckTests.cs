// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

public class AuditingHealthCheckTests
{
    [Fact]
    public async Task AddAuditing_RepeatedRegistration_RegistersOneHealthyMemoryCheck()
    {
        var services = new ServiceCollection();
        services.AddAuditing();
        services.AddAuditing();
        services.TryAddAuditingHealthCheck();
        using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var report = await host.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        report.Status.ShouldBe(HealthStatus.Healthy);
        var entry = report.Entries.ShouldHaveSingleItem();
        entry.Key.ShouldBe("Auditing");
        entry.Value.Tags.ShouldBe(["ready", "storage", "auditing"], ignoreOrder: true);
        entry.Value.Data["provider"].ShouldBe("Memory");
        entry.Value.Data["durable"].ShouldBe(false);
        entry.Value.Data["persisted"].ShouldBe(0L);
        host.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ShouldHaveSingleItem();
        host.GetRequiredService<AuditRuntime>().SnapshotObservations().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddAuditing_DisabledOrNoOp_DoesNotRegisterHealthCheck(bool noOp)
    {
        var services = Services();
        if (noOp) services.AddNoOpAuditContext();
        else services.AddAuditing(options => options.Enabled(false));
        using var host = services.BuildServiceProvider();

        host.GetService<HealthCheckService>().ShouldBeNull();
    }

    [Fact]
    public async Task CheckHealthAsync_Disabled_DoesNotResolveStorage()
    {
        var services = Services();
        services.AddAuditing(options => options.Enabled(false)).WithStorageProvider<LegacyProvider>(_ => throw new InvalidOperationException("Storage must not be resolved."));
        services.TryAddAuditingHealthCheck();
        using var host = services.BuildServiceProvider();

        (await host.GetRequiredService<HealthCheckService>().CheckHealthAsync()).Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_UnsupportedProvider_ReportsUnhealthy()
    {
        var services = Services();
        services.AddAuditing().WithStorageProvider<LegacyProvider>();
        using var host = services.BuildServiceProvider();

        var report = await host.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        report.Status.ShouldBe(HealthStatus.Unhealthy);
        report.Entries["Auditing"].Description.ShouldContain("does not support readiness");
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("throw")]
    [InlineData("null")]
    public async Task CheckHealthAsync_UnavailableProvider_UsesFailureStatusWithoutLeakingDetails(string mode)
    {
        var provider = ReadyProvider();
        provider.CheckReadinessAsync(Arg.Any<CancellationToken>()).Returns(_ => mode switch
        {
            "throw" => throw new InvalidOperationException("sensitive connection details"),
            "null" => Task.FromResult(Result<AuditProviderCapabilities>.Success(null)),
            _ => Task.FromResult(Result<AuditProviderCapabilities>.Failure(new AuditError("sensitive provider failure")))
        });
        var services = Services();
        services.TryAddAuditingHealthCheck(failureStatus: HealthStatus.Degraded);
        services.AddAuditing().WithStorageProvider(_ => provider);
        using var host = services.BuildServiceProvider();

        var report = await host.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        report.Status.ShouldBe(HealthStatus.Degraded);
        report.Entries["Auditing"].Description.ShouldNotContain("sensitive");
        report.Entries["Auditing"].Exception.ShouldBeNull();
        provider.CheckReadinessAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result<AuditProviderCapabilities>.Success(new())));
        var recovered = await host.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        recovered.Status.ShouldBe(HealthStatus.Healthy);
        recovered.Entries["Auditing"].Data["deliveryStatusAvailable"].ShouldBe(false);
        recovered.Entries["Auditing"].Data.ContainsKey("failed").ShouldBeFalse();
        await provider.DidNotReceive().AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
        await provider.DidNotReceive().FlushAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_PendingWrites_ProbesUnderSuppressionWithoutFlushingOrReadScope()
    {
        var provider = ReadyProvider();
        provider.DeliveryStatus.Returns(new AuditFlushResult { Pending = 3, Persisted = 7 });
        var services = Services();
        services.AddAuditing().WithStorageProvider(_ => provider)
            .WithReadScope((_, _) => throw new InvalidOperationException("A health probe must not query business evidence."));
        using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var runtime = host.GetRequiredService<AuditRuntime>();
        provider.CheckReadinessAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            runtime.Suppressed.ShouldBeTrue();
            return Task.FromResult(Result<AuditProviderCapabilities>.Success(new() { Provider = "Test", Buffered = true }));
        });

        var report = await host.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        report.Status.ShouldBe(HealthStatus.Healthy);
        report.Entries["Auditing"].Data["pending"].ShouldBe(3L);
        report.Entries["Auditing"].Data["persisted"].ShouldBe(7L);
        runtime.Suppressed.ShouldBeFalse();
        runtime.SnapshotFailures().ShouldBeEmpty();
        await provider.DidNotReceive().AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
        await provider.DidNotReceive().FlushAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckHealthAsync_ObservedCaptureOrDeliveryFailure_ReportsDegraded(bool capture)
    {
        var provider = ReadyProvider();
        provider.DeliveryStatus.Returns(new AuditFlushResult { Failed = capture ? 0 : 1 });
        var services = Services();
        services.AddAuditing().WithStorageProvider(_ => provider);
        using var host = services.BuildServiceProvider();
        if (capture) host.GetRequiredService<AuditRuntime>().Report("AuditAdmissionFailed");

        var report = await host.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        report.Status.ShouldBe(HealthStatus.Degraded);
        report.Entries["Auditing"].Data["captureFailureReasonCount"].ShouldBe(capture ? 1 : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckHealthAsync_QuietRecoveryWindow_RecoversWithoutClearingHistory(bool capture)
    {
        var clock = new FakeTimeProvider();
        var provider = ReadyProvider();
        var delivery = new AuditFlushResult { ReceiptEpoch = Guid.NewGuid(), Failed = capture ? 0 : 1 };
        provider.DeliveryStatus.Returns(_ => delivery);
        var services = Services();
        services.AddSingleton<TimeProvider>(clock);
        services.AddAuditing(options => options.Configure(value => value with { HealthFailureWindow = TimeSpan.FromMinutes(2) })).WithStorageProvider(_ => provider);
        using var host = services.BuildServiceProvider();
        var runtime = host.GetRequiredService<AuditRuntime>();
        var health = host.GetRequiredService<HealthCheckService>();
        if (capture) runtime.Report("AuditConfigurationFailed");

        (await health.CheckHealthAsync()).Status.ShouldBe(HealthStatus.Degraded);
        clock.Advance(TimeSpan.FromMinutes(1));
        (await health.CheckHealthAsync()).Status.ShouldBe(HealthStatus.Degraded);
        clock.Advance(TimeSpan.FromMinutes(1));
        var recovered = await health.CheckHealthAsync();

        recovered.Status.ShouldBe(HealthStatus.Healthy);
        recovered.Entries["Auditing"].Data["recentFailures"].ShouldBe(false);
        recovered.Entries["Auditing"].Data["captureFailureReasonCount"].ShouldBe(capture ? 1 : 0);
        recovered.Entries["Auditing"].Data["failed"].ShouldBe(capture ? 0L : 1L);
        if (capture) runtime.SnapshotFailures()["AuditConfigurationFailed"].ShouldBe(1);

        if (capture) runtime.Report("AuditConfigurationFailed");
        else delivery = delivery with { Failed = 2 };
        (await health.CheckHealthAsync()).Status.ShouldBe(HealthStatus.Degraded);
        clock.Advance(TimeSpan.FromMinutes(2));
        (await health.CheckHealthAsync()).Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public void HasRecentFailures_DeliveryEpochsAndStaleSnapshots_ObserveNewFailuresOnce()
    {
        var clock = new FakeTimeProvider();
        using var runtime = new AuditRuntime(new(), clock, new LegacyProvider(), []);
        var delivery = new AuditFlushResult { ReceiptEpoch = Guid.NewGuid(), Failed = 2 };
        runtime.HasRecentFailures(delivery).ShouldBeTrue();
        clock.Advance(TimeSpan.FromMinutes(4));
        Parallel.For(0, 20, _ => runtime.HasRecentFailures(delivery).ShouldBeTrue());
        runtime.HasRecentFailures(null).ShouldBeTrue();
        runtime.HasRecentFailures(delivery with { Failed = 1 }).ShouldBeTrue();
        clock.Advance(TimeSpan.FromMinutes(1));
        runtime.HasRecentFailures(delivery).ShouldBeFalse();
        runtime.HasRecentFailures(delivery with { ReceiptEpoch = Guid.NewGuid() }).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(86401)]
    public void Build_UnboundedHealthWindow_RejectsConfiguration(int seconds)
    {
        Should.Throw<ArgumentException>(() => new AuditingOptionsBuilder().Configure(options => options with
        { HealthFailureWindow = TimeSpan.FromSeconds(seconds) }).Build());
    }

    [Fact]
    public void HasRecentFailures_ClockAdjustmentsAndNewFailure_UseElapsedQuietTime()
    {
        var elapsedClock = new FakeTimeProvider();
        var clock = new OffsetTimeProvider(elapsedClock);
        using var runtime = new AuditRuntime(new(), clock, new LegacyProvider(), []);
        runtime.Report("AuditConfigurationFailed");
        clock.Offset = TimeSpan.FromDays(365);
        runtime.HasRecentFailures().ShouldBeTrue();
        elapsedClock.Advance(TimeSpan.FromMinutes(4));
        runtime.Report("AuditConfigurationFailed");
        clock.Offset = TimeSpan.FromDays(-365);
        elapsedClock.Advance(TimeSpan.FromMinutes(1));
        runtime.HasRecentFailures().ShouldBeTrue();
        elapsedClock.Advance(TimeSpan.FromMinutes(4));
        runtime.HasRecentFailures().ShouldBeFalse();
        runtime.SnapshotFailures()["AuditConfigurationFailed"].ShouldBe(2);
    }

    [Fact]
    public async Task CheckHealthAsync_Cancellation_PropagatesAndRestoresSuppression()
    {
        var provider = ReadyProvider();
        provider.CheckReadinessAsync(Arg.Any<CancellationToken>()).Returns(call => Task.FromCanceled<Result<AuditProviderCapabilities>>(call.Arg<CancellationToken>()));
        var services = Services();
        services.AddAuditing().WithStorageProvider(_ => provider);
        using var host = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var check = ActivatorUtilities.CreateInstance<AuditingHealthCheck>(host);

        await Should.ThrowAsync<OperationCanceledException>(() => check.CheckHealthAsync(new(), cancellation.Token));

        host.GetRequiredService<AuditRuntime>().Suppressed.ShouldBeFalse();
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static IAuditingStorageProvider ReadyProvider()
    {
        var provider = Substitute.For<IAuditingStorageProvider>();
        provider.CheckReadinessAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result<AuditProviderCapabilities>.Success(new() { Provider = "Test" })));
        return provider;
    }

    private sealed class LegacyProvider : IAuditingStorageProvider
    {
        public Task<Result<AuditAppendReceipt>> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<AuditRecord>> FindAsync(Guid auditId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<AuditFlushResult>> FlushAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OffsetTimeProvider(FakeTimeProvider elapsedClock) : TimeProvider
    {
        public TimeSpan Offset { get; set; }

        public override DateTimeOffset GetUtcNow() => elapsedClock.GetUtcNow() + this.Offset;

        public override long GetTimestamp() => elapsedClock.GetTimestamp();

        public override long TimestampFrequency => elapsedClock.TimestampFrequency;

        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
            => elapsedClock.CreateTimer(callback, state, dueTime, period);
    }
}

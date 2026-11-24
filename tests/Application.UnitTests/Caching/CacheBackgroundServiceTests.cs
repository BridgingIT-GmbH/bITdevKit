// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

/// <summary>Proves shared worker registration, disabled opt-ins, and nonoverlapping bounded sweeps.</summary>
public class CacheBackgroundServiceTests
{
    /// <summary>The monitored hosted registrations resolve the same two singleton worker instances.</summary>
    [Fact]
    public async Task Registration_UsesExactlyTwoSharedHostedWorkers()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards"));
        var hosted = fixture.Services.GetServices<IHostedService>().ToArray();

        hosted.OfType<CacheBackgroundService>().Count().ShouldBe(2);
        hosted.OfType<CacheRefreshService>().Single().ShouldBeSameAs(fixture.Services.GetRequiredService<CacheRefreshService>());
        hosted.OfType<CacheMaintenanceService>().Single().ShouldBeSameAs(fixture.Services.GetRequiredService<CacheMaintenanceService>());
        hosted.Any(s => s.GetType() == typeof(CacheWarmupService)).ShouldBeFalse();
    }

    /// <summary>No refresh/warm-up opt-in starts a polling loop or a source.</summary>
    [Fact]
    public async Task DisabledRefresh_StartsNoPollingAndInvokesNoSource()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards"));
        var sut = fixture.Services.GetRequiredService<CacheRefreshService>();
        await sut.StartAsync(default);
        await sut.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5));
        (await sut.SweepOnceAsync()).Value.ShouldBeEmpty();
        fixture.Source.Calls.ShouldBe(0);
    }

    /// <summary>Host startup and the configured delay must both complete before the first scheduled unit.</summary>
    [Fact]
    public async Task HostedSweep_WaitsForApplicationStartAndConfiguredDelay()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards"));
        var clock = new ObservedClock();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sut = new ProbeWorker(fixture.Catalog, fixture.ApplicationLifetime, clock, entered);
        await sut.StartAsync(default);
        clock.Advance(TimeSpan.FromHours(1));
        entered.Task.IsCompleted.ShouldBeFalse();
        clock.TimerCreated.Task.IsCompleted.ShouldBeFalse();
        fixture.ApplicationLifetime.Started.Cancel();
        (await clock.TimerCreated.Task.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(TimeSpan.FromSeconds(15));
        clock.Advance(TimeSpan.FromSeconds(14));
        entered.Task.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(1));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.StopAsync(default);
    }

    /// <summary>A running source sweep cannot overlap an explicit second sweep.</summary>
    [Fact]
    public async Task ActiveSweep_RejectsOverlapAndDisposesTheOwnedLoaderScope()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards", c => c.With(x => x.Refresh.Enabled, true)));
        await fixture.MakeDueAsync();
        fixture.Source.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var sut = fixture.Services.GetRequiredService<CacheRefreshService>();
        var first = sut.SweepOnceAsync();
        await fixture.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var second = await sut.SweepOnceAsync();
        second.IsFailure.ShouldBeTrue();
        fixture.Source.Calls.ShouldBe(1);
        fixture.Source.Release.SetResult();
        (await first).IsSuccess.ShouldBeTrue();
        await fixture.Source.DisposedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Source.Disposed.ShouldBe(1);
    }
    /// <summary>Opt-in history uses the maintenance timer without accelerating native cleanup or creating a third cache worker.</summary>
    [Fact]
    public async Task HistorySampling_UsesMaintenanceWorkerAndPreservesNativeInterval()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithOptions(o => o.With(x => x.Diagnostics.HistoryEnabled, true))
            .WithCollection<string>("cards", c => c.With(x => x.Expiration.AbsoluteTtl, TimeSpan.FromSeconds(1))
                .With(x => x.Expiration.RetentionGrace, TimeSpan.Zero)));
        await fixture.Collection().SetAsync(new("expires"), "value");
        var sut = fixture.Services.GetRequiredService<CacheMaintenanceService>();
        CacheWorkerOptions.Maintenance(fixture.Catalog).Interval.ShouldBe(TimeSpan.FromSeconds(5));
        await sut.SweepOnceAsync();
        fixture.Time.Advance(TimeSpan.FromSeconds(5));
        (await sut.SweepOnceAsync()).Value.ShouldBeEmpty();
        (await fixture.Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(1);
        var history = fixture.Services.GetRequiredService<CacheMetricHistory>();
        (await history.QueryAsync(new("cards"))).Value.AvailableTo.ShouldNotBeNull();
        fixture.Time.Advance(TimeSpan.FromSeconds(55));
        await sut.SweepOnceAsync();
        (await fixture.Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(0);
        fixture.Services.GetServices<IHostedService>().OfType<CacheBackgroundService>().Count().ShouldBe(2);
    }

    private sealed class ProbeWorker(CacheCatalog catalog, IHostApplicationLifetime lifetime, TimeProvider clock, TaskCompletionSource entered)
        : CacheBackgroundService(catalog, new(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(1)), lifetime, clock)
    {
        protected override Task<IReadOnlyList<CacheWorkerOutcome>> ExecuteSweepAsync(CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            return Task.FromResult<IReadOnlyList<CacheWorkerOutcome>>([]);
        }
    }

    private sealed class ObservedClock : TimeProvider
    {
        private readonly FakeTimeProvider clock = new();
        internal readonly TaskCompletionSource<TimeSpan> TimerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Advance(TimeSpan duration) => this.clock.Advance(duration);

        public override DateTimeOffset GetUtcNow() => this.clock.GetUtcNow();

        public override long GetTimestamp() => this.clock.GetTimestamp();

        public override long TimestampFrequency => this.clock.TimestampFrequency;

        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = this.clock.CreateTimer(callback, state, dueTime, period);
            this.TimerCreated.TrySetResult(dueTime);
            return timer;
        }
    }

}
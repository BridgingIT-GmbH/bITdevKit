// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks bounded intrinsic observations and real process-local history.</summary>
public sealed class CacheDiagnosticsTests
{
    /// <summary>Counter downsampling weights actual observation durations rather than averaging interval rates.</summary>
    [Fact]
    public async Task History_UnequalIntervals_DownsamplesDurationWeightedRates()
    {
        var clock = new Clock();
        await using var services = Create(clock: clock);
        var slot = services.GetRequiredService<CacheDiagnostics>().ForCollection("items");
        var sut = services.GetRequiredService<CacheMetricHistory>();
        slot.Record(CacheObservationKind.Request, 10);
        clock.Advance(TimeSpan.FromSeconds(5));
        sut.Sample();
        slot.Record(CacheObservationKind.Request, 60);
        clock.Advance(TimeSpan.FromSeconds(15));
        sut.Sample();
        var id = sut.GetSeries("items").Single(series => series.Metric == "Request").Id;

        var result = await sut.QueryAsync(new("items", [id], MaximumPoints: 1));

        var point = result.Value.Series.Single().Points.Single();
        point.Value.ShouldBe(3.5);
        point.Seconds.ShouldBe(20);
        point.At.ShouldBe(clock.GetUtcNow());
    }

    /// <summary>Sampled quantiles keep the last actual observation rather than manufacturing an average quantile.</summary>
    [Fact]
    public async Task History_QuantileDownsampling_RetainsLastActualValue()
    {
        var clock = new Clock();
        await using var services = Create(clock: clock);
        var slot = services.GetRequiredService<CacheDiagnostics>().ForCollection("items");
        var sut = services.GetRequiredService<CacheMetricHistory>();
        slot.RecordDuration(CacheLatencyKind.Operation, TimeSpan.FromMilliseconds(100));
        clock.Advance(TimeSpan.FromSeconds(5));
        sut.Sample();
        slot.RecordDuration(CacheLatencyKind.Operation, TimeSpan.FromMilliseconds(300));
        clock.Advance(TimeSpan.FromSeconds(15));
        sut.Sample();
        var id = sut.GetSeries("items").Single(series => series.Metric == "OperationP95").Id;

        var result = await sut.QueryAsync(new("items", [id], MaximumPoints: 1));

        var point = result.Value.Series.Single().Points.Single();
        point.Value.ShouldBe(0.3);
        point.At.ShouldBe(clock.GetUtcNow());
    }

    /// <summary>Wrapped sample storage preserves both endpoints of the real retained time window.</summary>
    [Fact]
    public async Task History_WrappedRing_PreservesInclusiveWindowAndChronologicalOrder()
    {
        var clock = new Clock();
        var registrations = new ServiceCollection();
        registrations.AddSingleton<TimeProvider>(clock);
        registrations.AddApplicationCaching().WithOptions(o => o
            .With(x => x.Diagnostics.HistoryEnabled, true)
            .With(x => x.Diagnostics.SamplingInterval, TimeSpan.FromSeconds(1))
            .With(x => x.Diagnostics.HistoryRetention, TimeSpan.FromSeconds(2))
            .With(x => x.Diagnostics.ChartWindow, TimeSpan.FromSeconds(2)))
            .WithCollection<string>("items");
        await using var services = registrations.BuildServiceProvider();
        var sut = services.GetRequiredService<CacheMetricHistory>();
        for (var index = 0; index < 6; index++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            sut.Sample();
        }

        var id = sut.GetSeries("items").Single(series => series.Metric == "ResidentBytes").Id;
        var result = await sut.QueryAsync(new("items", [id]));

        result.Value.Series.Single().Points.Select(point => point.At).ShouldBe(new[]
        {
            clock.GetUtcNow().AddSeconds(-2), clock.GetUtcNow().AddSeconds(-1), clock.GetUtcNow()
        });
    }

    [Fact]
    public async Task Snapshot_NoSamples_DistinguishesZeroCountersAndUnknownQuantiles()
    {
        await using var services = Create();
        var sut = services.GetRequiredService<CacheDiagnostics>().ForCollection("items");

        var snapshot = sut.Snapshot();

        snapshot.Counters[CacheObservationKind.Request].ShouldBe(0);
        snapshot.Latencies[CacheLatencyKind.Operation].P95Seconds.ShouldBeNull();
        snapshot.Latencies[CacheLatencyKind.Operation].Availability.ShouldBe(CacheObservationAvailability.NoSamples);
    }

    [Fact]
    public async Task Duration_ActualRetainedSamples_UsesQuantilesAndExpiresTheWindow()
    {
        var clock = new Clock();
        await using var services = Create(clock: clock);
        var sut = services.GetRequiredService<CacheDiagnostics>().ForCollection("items");
        foreach (var milliseconds in new[] { 10, 20, 900 }) { sut.RecordDuration(CacheLatencyKind.Source, TimeSpan.FromMilliseconds(milliseconds)); }

        var measured = sut.Snapshot().Latencies[CacheLatencyKind.Source];
        measured.P95Seconds.ShouldBe(0.9);
        measured.P50Seconds.ShouldBe(0.02);
        measured.Accuracy.ShouldBe(CacheObservationAccuracy.Sampled);
        measured.RetainedSamples.ShouldBe(3);
        clock.Advance(TimeSpan.FromMinutes(16));
        var expired = sut.Snapshot().Latencies[CacheLatencyKind.Source];
        expired.P95Seconds.ShouldBeNull();
        expired.Availability.ShouldBe(CacheObservationAvailability.NoSamples);
    }

    [Fact]
    public async Task LogicalCalls_PositiveNegativeMiss_CountOnceAndSeparatePublications()
    {
        await using var services = Create();
        var cache = services.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        await cache.SetAsync(new("present"), "value");
        await cache.GetAsync("present");
        await cache.GetAsync("absent");
        await cache.GetOrLoadAsync(new("negative"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(CacheLoadResult<string>.NotFound())));
        await cache.GetAsync("negative");

        var snapshot = services.GetRequiredService<CacheDiagnostics>().ForCollection("items").Snapshot();
        snapshot.Counters[CacheObservationKind.Request].ShouldBe(5);
        snapshot.Counters[CacheObservationKind.PositiveHit].ShouldBe(1);
        snapshot.Counters[CacheObservationKind.NegativeHit].ShouldBe(1);
        snapshot.Counters[CacheObservationKind.Miss].ShouldBe(2);
        snapshot.Counters[CacheObservationKind.SourceAttempt].ShouldBe(1);
        snapshot.Counters[CacheObservationKind.WriteSuccess].ShouldBe(2);
    }

    [Fact]
    public async Task History_HitRatioCapacityAndMissingQuantiles_PreservesActualTimestamps()
    {
        var clock = new Clock();
        await using var services = Create(clock: clock);
        var diagnostics = services.GetRequiredService<CacheDiagnostics>();
        var slot = diagnostics.ForCollection("items");
        var sut = services.GetRequiredService<CacheMetricHistory>();
        slot.Record(CacheObservationKind.PositiveHit, 2);
        slot.Record(CacheObservationKind.NegativeHit);
        slot.Record(CacheObservationKind.Miss);
        clock.Advance(TimeSpan.FromSeconds(5));
        sut.Sample();
        var ids = sut.GetSeries("items").Where(series => series.Metric is "LookupHitRatio" or "HotPinnedBytes" or "OperationP95" or "ResidentBytes").Select(series => series.Id).ToArray();

        var result = await sut.QueryAsync(new("items", ids));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Series.Single(series => series.Descriptor.Metric == "LookupHitRatio").Points.Single().Value.ShouldBe(75);
        result.Value.Series.Single(series => series.Descriptor.Metric == "HotPinnedBytes").Points.Single().Value.ShouldBe(0);
        result.Value.Series.Single(series => series.Descriptor.Metric == "OperationP95").Availability.ShouldBe(CacheObservationAvailability.NoSamples);
        result.Value.Series.Single(series => series.Descriptor.Metric == "ResidentBytes").Points.Single().Value.Value.ShouldBeGreaterThanOrEqualTo(0);
        result.Value.AvailableFrom.ShouldBe(clock.GetUtcNow());
    }

    [Fact]
    public async Task Query_ChartAllowanceOccupied_RejectsBeforeProviderCreation()
    {
        await using var services = Create();
        var gate = services.GetRequiredService<CacheDiagnosticQueryGate>();
        gate.TryEnter(out var first).ShouldBeTrue();
        gate.TryEnter(out var second).ShouldBeTrue();
        using (first)
        using (second)
        {
            var result = await services.GetRequiredService<CacheDiagnosticsQueryService>().GetSnapshotAsync();
            result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Capacity);
            services.GetRequiredService<CacheProviderRegistry>().TryGetCreated("memory", out _).ShouldBeFalse();
        }

        var available = await services.GetRequiredService<CacheDiagnosticsQueryService>().GetSnapshotAsync();
        available.IsSuccess.ShouldBeTrue();
        available.Value.Providers.Single().Quota.Availability.ShouldBe(CacheObservationAvailability.Unavailable);
        services.GetRequiredService<CacheProviderRegistry>().TryGetCreated("memory", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Disabled_RecordAndHistory_ReturnDisabledWithoutProviderCreation()
    {
        await using var services = Create(false);
        var sut = services.GetRequiredService<CacheDiagnostics>().ForCollection("items");
        sut.Record(CacheObservationKind.Request);
        sut.RecordDuration(CacheLatencyKind.Operation, TimeSpan.FromSeconds(1));

        sut.Snapshot().Counters[CacheObservationKind.Request].ShouldBeNull();
        sut.Snapshot().Availability.ShouldBe(CacheObservationAvailability.Disabled);
        (await services.GetRequiredService<CacheMetricHistory>().QueryAsync(new("items"))).Value.Availability.ShouldBe(CacheObservationAvailability.Disabled);
        services.GetRequiredService<CacheProviderRegistry>().TryGetCreated("memory", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task History_ConfiguredSeriesCeiling_OmitsFurtherDeclarations()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithOptions(options => options.With(x => x.Diagnostics.MaximumHistorySeries, 3).With(x => x.Diagnostics.HistoryEnabled, true))
            .WithValueContract<string>("text").WithCollection<string>("items");
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<CacheMetricHistory>();

        sut.GetSeries().Count.ShouldBe(3);
        sut.GetSeries("items").ShouldBeEmpty();
        (await sut.QueryAsync(new("items"))).Value.Availability.ShouldBe(CacheObservationAvailability.Capacity);
        (await sut.QueryAsync(new("items", Enumerable.Repeat("invalid", 17).ToArray()))).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task DetailedHistory_ExplicitlyEnabled_RetainsBoundedProcessKeyedFingerprints()
    {
        var clock = new Clock();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        services.AddApplicationCaching().WithOptions(options => options.With(x => x.Diagnostics.DetailedKeyHistory, true).With(x => x.Diagnostics.LatencySampleCapacity, 3))
            .WithValueContract<string>("text").WithCollection<string>("items");
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        var sut = provider.GetRequiredService<CacheDiagnostics>().ForCollection("items");
        await cache.GetAsync("secret-key");
        await cache.GetAsync("secret-key");
        await cache.GetAsync("other-key");
        var first = sut.GetKeyHistory();
        first.Count.ShouldBe(3);
        first[1].KeyFingerprint.ShouldBe(first[2].KeyFingerprint);
        first[0].KeyFingerprint.ShouldNotBe(first[1].KeyFingerprint);
        first.ShouldAllBe(item => item.KeyFingerprint.Length == 64 && !item.KeyFingerprint.Contains("secret"));

        await cache.GetAsync("last-key");

        sut.GetKeyHistory().Count.ShouldBe(3);
        sut.GetKeyHistory(1)[0].KeyFingerprint.ShouldNotBe(first[0].KeyFingerprint);
        clock.Advance(TimeSpan.FromMinutes(16));
        sut.GetKeyHistory().ShouldBeEmpty();
    }

    private static ServiceProvider Create(bool enabled = true, Clock clock = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock ?? new Clock());
        services.AddApplicationCaching().WithOptions(options => options.With(x => x.Diagnostics.Enabled, enabled).With(x => x.Diagnostics.HistoryEnabled, true))
            .WithValueContract<string>("text").WithCollection<string>("items", collection => collection.With(x => x.NegativeCaching.Enabled, true));
        return services.BuildServiceProvider();
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan duration) => this.now += duration;
    }
}
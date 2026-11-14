// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Checks conditional proactive refresh, persistent scheduler backoff, and occupied source lifetime.</summary>
public class CacheRefreshTests
{
    /// <summary>A due active version refreshes through its trusted source without manufacturing foreground activity.</summary>
    [Fact]
    public async Task Sweep_DueActiveVersion_PublishesWithoutForegroundActivity()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards", c => c.With(x => x.Refresh.Enabled, true)));
        await fixture.MakeDueAsync();
        var sut = fixture.Services.GetRequiredService<CacheRefreshService>();

        var swept = await sut.SweepOnceAsync();
        swept.Value.Single().Completed.ShouldBe(1);
        fixture.Source.Calls.ShouldBe(1);
        var metadata = (await fixture.Provider.LookupAsync(fixture.Identity("key"), new(ObserveAccess: false))).Value.Metadata;
        metadata.HasForegroundAccess.ShouldBeFalse();
        (await fixture.Collection().GetAsync("key")).Value.Value.ShouldBe("fresh");
    }

    /// <summary>Failures retain the old expiry and persist finite version-scoped backoff.</summary>
    [Fact]
    public async Task SourceFailure_PreservesOldExpiryAndStoresVersionBackoff()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards", c => c.With(x => x.Refresh.Enabled, true)));
        await fixture.MakeDueAsync();
        fixture.Source.Fail = true;
        var previous = (await fixture.Provider.LookupAsync(fixture.Identity("key"), new(ObserveAccess: false))).Value.Metadata;
        var sut = fixture.Services.GetRequiredService<CacheRefreshService>();

        await sut.SweepOnceAsync();
        var current = (await fixture.Provider.LookupAsync(fixture.Identity("key"), new(ObserveAccess: false))).Value.Metadata;
        current.Version.ShouldBe(previous.Version);
        current.ExpiresAt.ShouldBe(previous.ExpiresAt);
        current.RefreshFailureCount.ShouldBe(1);
        current.RefreshNotBefore.ShouldNotBeNull();
        current.RefreshNotBefore.Value.ShouldBeGreaterThan(fixture.Time.GetUtcNow());
        await sut.SweepOnceAsync();
        fixture.Source.Calls.ShouldBe(1);
    }

    /// <summary>All collections share the process candidate allowance and a rotating start prevents starvation.</summary>
    [Fact]
    public async Task MultipleCollections_ShareOneCandidateBudgetAndRotateAdmission()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithOptions(o => o.With(x => x.Limits.MaxRefreshCandidates, 1))
            .WithCollection<string>("cards", c => c.With(x => x.Refresh.Enabled, true))
            .WithCollection<string>("other", c => c.With(x => x.Refresh.Enabled, true)));
        foreach (var name in new[] { "cards", "other" })
        {
            await fixture.Collection(name).SetAsync(new("key"), "old");
            await fixture.Collection(name).GetAsync("key");
        }

        fixture.Time.Advance(TimeSpan.FromMinutes(9.5));
        var sut = fixture.Services.GetRequiredService<CacheRefreshService>();
        var first = await sut.SweepOnceAsync();
        first.Value.Sum(o => o.Examined).ShouldBeLessThanOrEqualTo(1);
        fixture.Source.Calls.ShouldBe(1);
        fixture.Time.Advance(TimeSpan.FromSeconds(16));
        var second = await sut.SweepOnceAsync();
        second.Value.Sum(o => o.Examined).ShouldBeLessThanOrEqualTo(1);
        fixture.Source.Calls.ShouldBe(2);
        fixture.Source.Collections.Distinct().Count().ShouldBe(2);
    }

    /// <summary>A selected version replaced before conditional refresh starts no source work.</summary>
    [Fact]
    public async Task SelectedVersionChanged_ConditionalRefreshDoesNotStartSource()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards"));
        var sut = fixture.Collection();
        var first = (await sut.SetAsync(new("key"), "one")).Value;
        await sut.SetAsync(new("key"), "two");
        var calls = 0;

        var result = await sut.RefreshAsync(new("key"), (_, _) =>
        {
            calls++;
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("obsolete")));
        }, new CacheAccessOptions(Background: true, ExpectedVersion: first.Version));

        result.IsFailure.ShouldBeTrue();
        calls.ShouldBe(0);
        (await sut.GetAsync("key")).Value.Value.ShouldBe("two");
    }

    /// <summary>Old validity ends during admitted refresh and callers can join the still-owned fresh work.</summary>
    [Fact]
    public async Task ExpiryDuringRefresh_StopsOldReadsAndAllowsJoiningFreshWork()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards", c => c.With(x => x.Refresh.Enabled, true)
            .With(x => x.Loading.SourceTimeout, TimeSpan.FromMinutes(5)).With(x => x.Loading.WaitTimeout, TimeSpan.FromMinutes(5))));
        await fixture.MakeDueAsync();
        fixture.Source.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var sut = fixture.Services.GetRequiredService<CacheRefreshService>();
        var sweep = sut.SweepOnceAsync();
        await fixture.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        (await fixture.Collection().GetAsync("key")).Value.Value.ShouldBe("old");
        fixture.Time.Advance(TimeSpan.FromMinutes(1));
        (await fixture.Collection().GetAsync("key")).Value.Found.ShouldBeFalse();
        var duplicateCalls = 0;
        var joined = fixture.Collection().GetOrLoadAsync(new("key"), (_, _) =>
        {
            duplicateCalls++;
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("duplicate")));
        }).AsTask();
        fixture.Source.Release.SetResult();

        (await joined).Value.Value.ShouldBe("fresh");
        duplicateCalls.ShouldBe(0);
        await sweep;
    }

    /// <summary>A timed-out wait does not release background admission while its source ignores cancellation.</summary>
    [Fact]
    public async Task IgnoredSourceCancellation_RetainsBackgroundAdmissionUntilActualCompletion()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards", c => c.With(x => x.Refresh.Enabled, true)
            .With(x => x.Loading.WaitTimeout, TimeSpan.FromSeconds(1))));
        await fixture.MakeDueAsync();
        fixture.Source.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var resources = fixture.Services.GetRequiredService<CacheRuntimeResources>();
        var sut = fixture.Services.GetRequiredService<CacheRefreshService>();
        var sweep = sut.SweepOnceAsync();
        await fixture.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Time.Advance(TimeSpan.FromSeconds(2));
        await sweep;

        resources.RefreshSources.Active.ShouldBe(1);
        fixture.Source.Disposed.ShouldBe(0);
        fixture.Source.Release.SetResult();
        await fixture.Source.DisposedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
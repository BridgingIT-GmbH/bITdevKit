// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.Runtime.CompilerServices;
using NSubstitute;

/// <summary>Checks bounded scoped warm-up, node-local pause, and independent named readiness.</summary>
public class CacheWarmupTests
{
    /// <summary>Startup warm-up is a single helper pass and does not mark its stored value as foreground activity.</summary>
    [Fact]
    public async Task StartupWarmup_RunsOnceWithoutForegroundActivityOrHotOptIn()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards", c => c.With(x => x.Warmup.Enabled, true)
            .With(x => x.Warmup.Items, new[] { new CacheItem("seed") })));
        var sut = fixture.Services.GetRequiredService<CacheRefreshService>();
        await sut.SweepOnceAsync();
        await sut.SweepOnceAsync();

        fixture.Source.Calls.ShouldBe(1);
        var metadata = (await fixture.Provider.LookupAsync(fixture.Identity("seed"), new(ObserveAccess: false))).Value.Metadata;
        metadata.HasForegroundAccess.ShouldBeFalse();
        fixture.Catalog.Collections["CARDS"].Options.HotEntries.Enabled.ShouldBeFalse();
    }

    /// <summary>Pause drops scoped asynchronous enumeration at a bounded key boundary and resume creates a fresh pass.</summary>
    [Fact]
    public async Task PauseDuringWarmup_DisposesEnumeratorAndStartsNoFurtherKeys()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = new CacheBackgroundFixture(b => b.WithWarmupSource<Keys>("keys")
            .WithCollection<string>("cards", c => c.With(x => x.Warmup.Enabled, true).With(x => x.Warmup.KeySource, "keys")),
            s => s.AddSingleton(disposed));
        fixture.Source.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var sut = fixture.Services.GetRequiredService<CacheRefreshService>();
        var first = sut.SweepOnceAsync();
        await fixture.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var control = fixture.Services.GetRequiredService<CacheBackgroundControl>();
        control.Pause("cards", "test").IsSuccess.ShouldBeTrue();
        fixture.Source.Release.SetResult();
        await first;
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Source.Calls.ShouldBe(1);

        control.Resume("cards", "test").IsSuccess.ShouldBeTrue();
        await sut.SweepOnceAsync();
        fixture.Source.Calls.ShouldBe(2);
    }

    /// <summary>A blocked named loader gate defers that scope while another ready collection can warm up.</summary>
    [Fact]
    public async Task UnreadyLoader_DoesNotBlockAnUnrelatedReadyCollection()
    {
        var gate = Substitute.For<IDatabaseReadyService>();
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.WaitForReadyAsync(Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>()).Returns(_ => blocked.Task);
        await using var fixture = new CacheBackgroundFixture(b => b.WithRefreshLoader<string, CacheBackgroundFixture.Loader>("database-source", "catalog-db")
            .WithCollection<string>("blocked", c => c.With(x => x.Warmup.Enabled, true).With(x => x.Warmup.Loader, "database-source")
                .With(x => x.Warmup.Items, new[] { new CacheItem("blocked") }))
            .WithCollection<string>("ready", c => c.With(x => x.Warmup.Enabled, true).With(x => x.Warmup.Items, new[] { new CacheItem("ready") })),
            s => s.AddSingleton(gate));
        var sut = fixture.Services.GetRequiredService<CacheRefreshService>();
        var sweep = sut.SweepOnceAsync();
        await fixture.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Source.Collections.Single().ShouldBe("READY");
        blocked.SetException(new TimeoutException());

        var result = await sweep;
        result.Value.Single(o => o.Name == "BLOCKED").Ready.ShouldBeFalse();
        fixture.Source.Calls.ShouldBe(1);
    }

    /// <summary>Resume cannot opt in a collection whose startup refresh and warm-up are disabled.</summary>
    [Fact]
    public async Task ResumeDisabledCollection_DoesNotEnableBackgroundFeatures()
    {
        await using var fixture = new CacheBackgroundFixture(b => b.WithCollection<string>("cards"));
        var sut = fixture.Services.GetRequiredService<CacheBackgroundControl>();
        sut.Pause("cards", "test");
        var state = sut.Resume("cards", "test").Value;
        state.Paused.ShouldBeFalse();
        state.RefreshConfigured.ShouldBeFalse();
        state.WarmupConfigured.ShouldBeFalse();
    }

    private sealed class Keys(TaskCompletionSource disposed) : ICacheWarmupSource
    {
        public async IAsyncEnumerable<CacheItem> ReadAsync(CacheWarmupContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                yield return new("one");
                await Task.CompletedTask;
                cancellationToken.ThrowIfCancellationRequested();
                yield return new("two");
            }
            finally { disposed.TrySetResult(); }
        }
    }
}
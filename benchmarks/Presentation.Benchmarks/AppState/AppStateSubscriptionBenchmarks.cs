// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.AppState;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Measures selector fanout with changed and unchanged selected values.</summary>
[MemoryDiagnoser]
[IterationTime(100)]
[SimpleJob(launchCount: 3, warmupCount: 5, iterationCount: 15)]
public class AppStateSubscriptionBenchmarks
{
    private CatalogViewState state;
    private long callbacks;
    private readonly Guid[][] selections = [[Guid.Empty], [new Guid(1, 0, 0, new byte[8])]];

    /// <summary>Gets or sets the number of independent subscriptions.</summary>
    [Params(0, 1, 10, 100)]
    public int Subscribers { get; set; }

    /// <summary>Creates deterministic selectors outside timing.</summary>
    [GlobalSetup]
    public async Task Setup()
    {
        this.state = new();
        for (var i = 0; i < this.Subscribers; i++)
        {
            this.state.Subscribe(m => m.Page, (change, token) => { this.callbacks++; return Task.CompletedTask; });
        }

        await this.SelectorUnchanged();
        if (this.callbacks != 0) { throw new InvalidOperationException("Unchanged selectors invoked callbacks."); }

        await this.SelectorChanged();
        if (this.callbacks != this.Subscribers) { throw new InvalidOperationException("Changed selector fanout differs."); }
    }

    /// <summary>Changes data outside the selected page value.</summary>
    [Benchmark]
    public Task SelectorUnchanged() => this.state.RestoreSnapshot(new() { Page = this.state.CurrentState.Page, Selected = this.selections[this.state.Version.Revision % 2] });

    /// <summary>Changes the value observed by every subscription.</summary>
    [Benchmark]
    public Task SelectorChanged() => this.state.RestoreSnapshot(new() { Page = this.state.CurrentState.Page + 1, Selected = this.selections[this.state.Version.Revision % 2] });

    /// <summary>Measures complete subscription attachment and removal.</summary>
    [Benchmark]
    public void SubscribeDispose() => this.state.Subscribe(m => m.Page, static (change, token) => Task.CompletedTask).Dispose();

    /// <summary>Releases observers and prevents unbounded retained work.</summary>
    [GlobalCleanup]
    public void Cleanup() => this.state.Dispose();
}

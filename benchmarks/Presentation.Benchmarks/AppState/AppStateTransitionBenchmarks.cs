// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.AppState;

using BenchmarkDotNet.Attributes;

/// <summary>Measures accepted transitions and synchronous observer fanout.</summary>
[MemoryDiagnoser]
[IterationTime(100)]
[SimpleJob(launchCount: 3, warmupCount: 5, iterationCount: 15)]
public class AppStateTransitionBenchmarks
{
    private CatalogViewState state;
    private readonly CatalogViewModel rejected = new();

    /// <summary>Gets or sets the number of observers.</summary>
    [Params(0, 1, 10, 100)]
    public int Subscribers { get; set; }

    /// <summary>Registers observers before timing begins.</summary>
    [GlobalSetup]
    public void Setup()
    {
        this.state = new CatalogViewState();
        for (var i = 0; i < this.Subscribers; i++)
        {
            this.state.StateChanged += static _ => { };
        }
    }

    /// <summary>Copies, changes, and commits a distinct value each time.</summary>
    [Benchmark]
    public Task RestoreAndNotify()
    {
        var next = this.state.TakeSnapshot();
        next.Page++;
        return this.state.RestoreSnapshot(next);
    }

    /// <summary>Measures an accepted conditional transition including the new payload.</summary>
    [Benchmark]
    public Task<bool> ConditionalRestoreAccepted() => this.state.TryRestoreSnapshot(
        new() { Page = this.state.CurrentState.Page + 1 }, this.state.Version);

    /// <summary>Rejects an obsolete runtime identity before cloning a prepared payload.</summary>
    [Benchmark]
    public Task<bool> ConditionalRestoreRejected() => this.state.TryRestoreSnapshot(this.rejected, default);

    /// <summary>Releases the benchmark owner.</summary>
    [GlobalCleanup]
    public void Cleanup() => this.state.Dispose();
}

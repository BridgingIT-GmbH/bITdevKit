// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.AppState;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Measures correct steady-state bounded update/undo/redo triplets.</summary>
[MemoryDiagnoser]
[IterationTime(100)]
[SimpleJob(launchCount: 3, warmupCount: 5, iterationCount: 15)]
public class AppStateHistoryBenchmarks
{
    private CatalogViewState state;

    /// <summary>Gets or sets the bounded history capacity.</summary>
    [Params(10, 100)]
    public int Capacity { get; set; }

    /// <summary>Fills history before measurements.</summary>
    [GlobalSetup]
    public async Task Setup()
    {
        this.state = new(new AppStateOptions { HistoryEnabled = true, MaxHistoryItems = this.Capacity });
        for (var i = 1; i <= this.Capacity; i++) { await this.state.RestoreSnapshot(new() { Page = i }); }

        await this.UpdateUndoRedo();
        if (this.state.CurrentState.Page != this.Capacity + 1 || this.state.UndoHistory.Count != this.Capacity || this.state.CanRedo)
        {
            throw new InvalidOperationException("History triplet failed its bounded final-value check.");
        }
    }

    /// <summary>Accepts an edit, undoes it, and restores it through redo.</summary>
    [Benchmark]
    public async Task UpdateUndoRedo()
    {
        await this.state.RestoreSnapshot(new() { Page = this.state.CurrentState.Page + 1 });
        await this.state.Undo();
        await this.state.Redo();
    }

    /// <summary>Releases the bounded history.</summary>
    [GlobalCleanup]
    public void Cleanup() => this.state.Dispose();
}

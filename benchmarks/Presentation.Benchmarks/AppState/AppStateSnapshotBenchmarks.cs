// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.AppState;

using BenchmarkDotNet.Attributes;

/// <summary>Measures state reads and independent mutable snapshots.</summary>
[MemoryDiagnoser]
[IterationTime(100)]
[SimpleJob(launchCount: 3, warmupCount: 5, iterationCount: 15)]
public class AppStateSnapshotBenchmarks
{
    private CatalogViewState state;
    private CatalogViewState generated;
    private ManualCatalogViewState manual;

    /// <summary>Gets or sets the selected-ID payload size.</summary>
    [Params(16, 256)]
    public int ItemCount { get; set; }

    /// <summary>Creates deterministic data outside timed operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        this.state = new CatalogViewState();
        this.generated = new CatalogViewState(new BridgingIT.DevKit.Presentation.Web.Client.AppStateOptions
        { JsonTypeInfo = CatalogBenchmarkJson.Default.CatalogViewModel });
        this.manual = new ManualCatalogViewState();
        this.state.CurrentState.Selected = Enumerable.Range(0, this.ItemCount).Select(i => new Guid(i, 0, 0, new byte[8])).ToArray();
        this.generated.CurrentState.Selected = this.state.CurrentState.Selected.ToArray();
        this.manual.CurrentState.Selected = this.state.CurrentState.Selected.ToArray();
        foreach (var snapshot in new[] { this.SnapshotJson(), this.SnapshotGeneratedJson(), this.SnapshotManualCopy() })
        {
            if (ReferenceEquals(snapshot.Selected, this.state.CurrentState.Selected)
                || !snapshot.Selected.SequenceEqual(this.state.CurrentState.Selected))
            {
                throw new InvalidOperationException("Snapshot values must be independently mutable and equivalent.");
            }
        }
    }

    /// <summary>Copies using generated JSON metadata.</summary>
    [Benchmark]
    public CatalogViewModel SnapshotGeneratedJson() => this.generated.TakeSnapshot();

    /// <summary>Copies the same model using explicit independent array copying.</summary>
    [Benchmark]
    public CatalogViewModel SnapshotManualCopy() => this.manual.TakeSnapshot();

    /// <summary>Checks an already captured version without allocating.</summary>
    [Benchmark]
    public bool VersionCheck() => this.state.IsCurrent(this.state.Version);

    /// <summary>Reads the current reference without allocating a snapshot.</summary>
    [Benchmark]
    public CatalogViewModel CurrentStateRead() => this.state.CurrentState;

    /// <summary>Copies the complete mutable model using JSON.</summary>
    [Benchmark]
    public CatalogViewModel SnapshotJson() => this.state.TakeSnapshot();

    /// <summary>Releases the benchmark state.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.state.Dispose();
        this.generated.Dispose();
        this.manual.Dispose();
    }
}

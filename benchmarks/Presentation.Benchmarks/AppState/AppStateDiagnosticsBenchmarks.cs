// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.AppState;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Measures steady-state bounded diagnostics separately from ordinary transitions.</summary>
[MemoryDiagnoser]
[IterationTime(100)]
[SimpleJob(launchCount: 3, warmupCount: 5, iterationCount: 15)]
public class AppStateDiagnosticsBenchmarks
{
    private CatalogViewState off;
    private CatalogViewState metadata;
    private CatalogViewState projected;

    /// <summary>Configures bounded tracking outside measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        this.off = new();
        this.metadata = Create(null);
        this.projected = Create(static value => ((CatalogViewModel)value)?.Selected.Length);
    }

    /// <summary>Measures an ordinary transition with diagnostics disabled.</summary>
    [Benchmark(Baseline = true)]
    public Task TransitionDiagnosticsOff() => Update(this.off);

    /// <summary>Includes bounded metadata tracking and eviction.</summary>
    [Benchmark]
    public Task TransitionMetadataTracking() => Update(this.metadata);

    /// <summary>Includes an explicit safe count-only projection.</summary>
    [Benchmark]
    public Task TransitionProjectedValues() => Update(this.projected);

    /// <summary>Releases all benchmark state owners.</summary>
    [GlobalCleanup]
    public void Cleanup() { this.off.Dispose(); this.metadata.Dispose(); this.projected.Dispose(); }

    private static CatalogViewState Create(Func<object, object> projection) => new(new AppStateOptions(),
        debugger: new AppStateDebugger(NullLogger<AppStateDebugger>.Instance, null,
            new AppStateDebugOptions { StateChangesTracked = true, MaxTrackedChanges = 100, ValueProjection = projection }));

    private static Task Update(CatalogViewState state) => state.RestoreSnapshot(new() { Page = state.CurrentState.Page + 1 });
}

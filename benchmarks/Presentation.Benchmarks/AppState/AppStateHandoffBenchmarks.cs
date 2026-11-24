// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.AppState;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable BL0006 // Exercise native persistence using a minimal renderer, without browser or transport timing.

/// <summary>Measures copied capture, fresh-owner initialization and native lifecycle serialization.</summary>
/// <example><code>dotnet run -c Release --project benchmarks/Presentation.Benchmarks -- appstate --filter '*AppStateHandoffBenchmarks*'</code></example>
[MemoryDiagnoser]
[IterationTime(100)]
[SimpleJob(launchCount: 3, warmupCount: 5, iterationCount: 15)]
public class AppStateHandoffBenchmarks
{
    private CatalogViewState source;
    private AppStateOptions options;
    private AppStateHandoff<CatalogViewModel> snapshot;
    private ServiceProvider services;

    /// <summary>Gets or sets the selected-ID payload size.</summary>
    [Params(16, 256)]
    public int ItemCount { get; set; }

    /// <summary>Creates deterministic source data and verifies a native round trip before measurement.</summary>
    /// <returns>Completion of untimed setup.</returns>
    /// <example><code>await benchmarks.Setup();</code></example>
    [GlobalSetup]
    public async Task Setup()
    {
        this.options = new() { JsonTypeInfo = CatalogBenchmarkJson.Default.CatalogViewModel };
        this.source = new(this.options);
        this.source.CurrentState.Page = 17;
        this.source.CurrentState.Selected = Enumerable.Range(0, this.ItemCount)
            .Select(i => new Guid(i, 0, 0, new byte[8])).ToArray();
        this.snapshot = this.source.CaptureHandoff();
        this.services = new ServiceCollection().AddLogging().BuildServiceProvider();
        if (!await this.NativeRoundTrip()) { throw new InvalidOperationException("Native restore must retain independent model values."); }
    }

    /// <summary>Captures an independent model and a context-qualified logical handoff key.</summary>
    /// <returns>The copied transfer snapshot.</returns>
    /// <example><code>var snapshot = benchmarks.Capture();</code></example>
    [Benchmark]
    public AppStateHandoff<CatalogViewModel> Capture() => this.source.CaptureHandoff();

    /// <summary>Creates and disposes a destination while initializing it without history or storage.</summary>
    /// <returns>Whether initialization was accepted.</returns>
    /// <example><code>var restored = benchmarks.InitializeFreshOwner();</code></example>
    [Benchmark]
    public bool InitializeFreshOwner()
    {
        using var destination = new CatalogViewState(this.options);
        return destination.TryRestoreHandoff(this.snapshot, destination.Version);
    }

    /// <summary>Includes native callback registration, both serialization layers and fresh-owner circuit restoration.</summary>
    /// <returns>Whether the model was restored independently.</returns>
    /// <example><code>var restored = await benchmarks.NativeRoundTrip();</code></example>
    [Benchmark]
    public async Task<bool> NativeRoundTrip()
    {
        var native = new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance);
        using var renderer = new PersistenceRenderer(this.services);
        using var capture = AppStateLifecycle.Attach(this.source, native.State,
            CatalogBenchmarkJson.Default.CatalogViewModel, RenderMode.InteractiveServer);
        var store = new MemoryStateStore();
        await renderer.Dispatcher.InvokeAsync(() => native.PersistStateAsync(store, renderer));
        var target = new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance);
        await target.RestoreStateAsync(store, RestoreContext.LastSnapshot);
        using var destination = new CatalogViewState(this.options);
        using var restored = AppStateLifecycle.Attach(destination, target.State,
            CatalogBenchmarkJson.Default.CatalogViewModel, RenderMode.InteractiveServer);
        return restored.Restored && destination.CurrentState.Page == 17
            && destination.CurrentState.Selected.Length == this.ItemCount
            && !ReferenceEquals(destination.CurrentState.Selected, this.source.CurrentState.Selected);
    }

    /// <summary>Releases the source owner and untimed service infrastructure.</summary>
    /// <example><code>benchmarks.Cleanup();</code></example>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.source.Dispose();
        this.services.Dispose();
    }

    private sealed class MemoryStateStore : IPersistentComponentStateStore
    {
        private Dictionary<string, byte[]> values = [];

        /// <inheritdoc/>
        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() =>
            Task.FromResult<IDictionary<string, byte[]>>(this.values.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray()));

        /// <inheritdoc/>
        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state)
        {
            this.values = state.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public bool SupportsRenderMode(IComponentRenderMode renderMode) => renderMode is InteractiveServerRenderMode;
    }

    private sealed class PersistenceRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
    {
        /// <inheritdoc/>
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        /// <inheritdoc/>
        protected override void HandleException(Exception exception) => throw exception;

        /// <inheritdoc/>
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }
}

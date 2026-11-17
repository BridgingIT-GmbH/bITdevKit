// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.AppState;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Measures copied memory persistence without browser transport or wall-clock debounce.</summary>
[MemoryDiagnoser]
[IterationTime(100)]
[SimpleJob(launchCount: 3, warmupCount: 5, iterationCount: 15)]
public class AppStatePersistenceBenchmarks
{
    private CatalogViewState state;
    private CountingStore storage;

    /// <summary>Gets or sets the edits in a coalesced burst.</summary>
    [Params(10, 100)]
    public int Edits { get; set; }

    /// <summary>Initializes a copied-value store before timing.</summary>
    [GlobalSetup]
    public async Task Setup()
    {
        this.storage = new();
        this.state = new(new AppStateOptions { PersistenceType = AppStatePersistenceType.SessionScoped,
            DebounceDelay = TimeSpan.FromDays(1) }, this.storage);
        await this.state.Load();
        await this.CoalescedEdits();
        if (this.storage.Writes != 1 || this.state.CurrentState.Page != this.Edits)
        {
            throw new InvalidOperationException("A burst must contain the declared edits and one acknowledged write.");
        }
    }

    /// <summary>Builds a complete namespaced key.</summary>
    [Benchmark]
    public string BuildStorageKey() => AppStateStorageKey.Create("catalog", "sales", "view", "left", "anonymous:fixture", "none", 1);

    /// <summary>Reads a known missing key using independent serialization semantics.</summary>
    [Benchmark]
    public Task<CatalogViewModel> LoadMissing() => this.storage.LoadAsync<CatalogViewModel>("missing", "fixture");

    /// <summary>Awaits one complete captured write.</summary>
    [Benchmark]
    public Task SaveImmediate() => this.state.Save();

    /// <summary>Coalesces a burst into one explicit flush without waiting for a real timer.</summary>
    [Benchmark]
    public async Task CoalescedEdits()
    {
        for (var i = 0; i < this.Edits; i++) { await this.state.RestoreSnapshot(new() { Page = this.state.CurrentState.Page + 1 }); }

        await this.state.Save();
    }

    /// <summary>Drains owned cancelled delay tasks before releasing the state.</summary>
    [GlobalCleanup]
    public async Task Cleanup() => await this.state.DisposeAsync();

    private sealed class CountingStore : IAppStateStoreProvider
    {
        private readonly SessionScopedStateStoreProvider storage = new();

        /// <summary>Gets the acknowledged write count.</summary>
        public int Writes { get; private set; }

        /// <inheritdoc/>
        public async Task SaveAsync<T>(string stateId, string userContextId, T state)
        {
            await this.storage.SaveAsync(stateId, userContextId, state);
            this.Writes++;
        }

        /// <inheritdoc/>
        public Task<T> LoadAsync<T>(string stateId, string userContextId) => this.storage.LoadAsync<T>(stateId, userContextId);

        /// <inheritdoc/>
        public Task ClearAsync(string stateId, string userContextId) => this.storage.ClearAsync(stateId, userContextId);

        /// <inheritdoc/>
        public Task ClearAllAsync() => this.storage.ClearAllAsync();
    }
}

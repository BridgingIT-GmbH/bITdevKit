// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.AppState;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Mutable fictional preferences used to measure independent state copies.</summary>
public sealed class CatalogViewModel
{
    /// <summary>Gets or sets the selected record IDs.</summary>
    public Guid[] Selected { get; set; } = [];

    /// <summary>Gets or sets the current page.</summary>
    public int Page { get; set; }
}

/// <summary>Exercises the existing derived-state authoring contract.</summary>
public sealed class CatalogViewState : AppState<CatalogViewModel>
{
    private CatalogViewModel current = new();

    /// <summary>Creates nonpersisted state for an isolated benchmark invocation.</summary>
    public CatalogViewState() : base(NullLogger.Instance, new AppStateOptions(), debugger:
        new AppStateDebugger(NullLogger<AppStateDebugger>.Instance, new NoopJsRuntime(), new AppStateDebugOptions()))
    {
    }

    /// <summary>Creates configurable state using the historical constructor shape.</summary>
    /// <param name="options">Per-instance settings.</param>
    /// <param name="storageProvider">Optional copied-value storage.</param>
    /// <param name="userContextProvider">Optional partition provider.</param>
    /// <param name="debugger">Optional bounded observations.</param>
    public CatalogViewState(AppStateOptions options, IAppStateStoreProvider storageProvider = null,
        IUserContextProvider userContextProvider = null, AppStateDebugger debugger = null)
        : base(NullLogger.Instance, options, storageProvider, userContextProvider, debugger) { }

    /// <inheritdoc/>
    protected override CatalogViewModel GetCurrentState() => this.current;

    /// <inheritdoc/>
    protected override void UpdateState(CatalogViewModel state) => this.current = state;

    /// <inheritdoc/>
    protected override CatalogViewModel CreateDefaultState() => new();

    private sealed class NoopJsRuntime : Microsoft.JSInterop.IJSRuntime
    {
        /// <inheritdoc/>
        public ValueTask<T> InvokeAsync<T>(string identifier, object[] args) => ValueTask.FromResult(default(T));

        /// <inheritdoc/>
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object[] args) => ValueTask.FromResult(default(T));
    }
}

/// <summary>Preserves benchmark model metadata for the generated snapshot path.</summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(CatalogViewModel))]
public partial class CatalogBenchmarkJson : System.Text.Json.Serialization.JsonSerializerContext { }

/// <summary>Provides deliberate independent copying for the same mutable model.</summary>
public sealed class ManualCatalogViewState : AppState<CatalogViewModel>
{
    private CatalogViewModel current = new();

    /// <summary>Creates independent state with explicit mutable-value copying.</summary>
    public ManualCatalogViewState() : base(NullLogger.Instance, new AppStateOptions()) { }

    /// <inheritdoc/>
    protected override CatalogViewModel GetCurrentState() => this.current;

    /// <inheritdoc/>
    protected override void UpdateState(CatalogViewModel state) => this.current = state;

    /// <inheritdoc/>
    protected override CatalogViewModel CreateDefaultState() => new();

    /// <inheritdoc/>
    protected override CatalogViewModel CloneState(CatalogViewModel value) => value == null ? null
        : new() { Page = value.Page, Selected = value.Selected.ToArray() };
}

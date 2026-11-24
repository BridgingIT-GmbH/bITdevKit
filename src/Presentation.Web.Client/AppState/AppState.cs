// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;

/// <summary>Owns typed state, independent snapshots, bounded history, and optional persistence.</summary>
/// <typeparam name="TState">The serializable or explicitly copied model type.</typeparam>
/// <remarks>Use snapshot/restore methods for changes. Direct mutation of CurrentState cannot be observed.</remarks>
/// <example>
/// <code><![CDATA[
/// using BridgingIT.DevKit.Presentation.Web.Client;
/// using Microsoft.Extensions.Logging;
///
/// public sealed class CatalogViewModel
/// {
///     public int Page { get; set; } = 1;
///
///     public string Search { get; set; } = string.Empty;
///
///     public List<int> SelectedIds { get; set; } = [];
/// }
///
/// public class CatalogViewState : AppState<CatalogViewModel>
/// {
///     private CatalogViewModel current = new();
///
///     public CatalogViewState(ILogger<CatalogViewState> logger, AppStateOptions options,
///         IAppStateStoreProvider storageProvider = null, IUserContextProvider userContextProvider = null,
///         AppStateDebugger debugger = null)
///         : base(logger, options, storageProvider, userContextProvider, debugger) { }
///
///     public Task SetPage(int page)
///     {
///         var next = this.TakeSnapshot();
///         next.Page = page;
///         return this.SetCurrentState(next);
///     }
///
///     protected override CatalogViewModel GetCurrentState() => this.current;
///
///     protected override void UpdateState(CatalogViewModel state) => this.current = state;
///
///     protected override CatalogViewModel CreateDefaultState() => new();
/// }
///
/// [System.Text.Json.Serialization.JsonSerializable(typeof(CatalogViewModel))]
/// public partial class CatalogJson : System.Text.Json.Serialization.JsonSerializerContext { }
/// ]]></code>
/// </example>
public abstract partial class AppState<TState> : IAppStateRuntime, IDisposable, IAsyncDisposable
    where TState : class, new()
{
    private readonly object sync = new();
    private readonly ILogger logger;
    private readonly AppStateOptions options;
    private readonly IAppStateStoreProvider storageProvider;
    private readonly IUserContextProvider userContextProvider;
    private readonly AppStateDebugger debugger;
    private readonly List<TState> undo = [];
    private readonly List<TState> redo = [];
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource generation = new();
    private readonly HashSet<AppStateSubscription> subscriptions = [];
    private AppStateManager owner;
    private TimeProvider clock = TimeProvider.System;
    private AppStateContext context = new();
    private AppStateContext ownerContext;
    private long revision;
    private long contextRevision;
    private bool disposed;

    /// <summary>Creates an owner without performing browser or storage I/O.</summary>
    /// <param name="logger">The state logger; payload values are not logged.</param>
    /// <param name="options">The state configuration, copied for this instance.</param>
    /// <param name="storageProvider">The optional DI-owned storage adapter.</param>
    /// <param name="userContextProvider">The optional user partition provider.</param>
    /// <param name="debugger">The optional owner-local diagnostic observer.</param>
    protected AppState(ILogger logger, AppStateOptions options, IAppStateStoreProvider storageProvider = null,
        IUserContextProvider userContextProvider = null, AppStateDebugger debugger = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = (options ?? throw new ArgumentNullException(nameof(options))).Copy();
        this.storageProvider = storageProvider;
        this.userContextProvider = userContextProvider;
        this.debugger = debugger;
        this.PersistenceStatus = this.HasPersistence ? AppStatePersistenceStatus.NotLoaded : AppStatePersistenceStatus.NotConfigured;
        if (this.options.JsonTypeInfo != null && this.options.JsonTypeInfo.Type != typeof(TState))
        {
            throw new ArgumentException("JSON metadata must describe the state model.", nameof(options));
        }
    }

    /// <inheritdoc/>
    public virtual string StateId { get; } = typeof(TState).FullName.Replace(".", "_").ToLowerInvariant();

    /// <inheritdoc/>
    public Guid InstanceId { get; } = Guid.NewGuid();

    /// <summary>Gets the current model. Change it through snapshot/restore, not direct property mutation.</summary>
    public TState CurrentState => this.GetCurrentState();

    object IAppState.CurrentState => this.CurrentState;

    /// <inheritdoc/>
    public AppStateVersion Version
    {
        get
        {
            lock (this.sync)
            {
                return new(this.InstanceId, this.revision, this.contextRevision);
            }
        }
    }

    /// <inheritdoc/>
    public AppStatePersistenceStatus PersistenceStatus { get; private set; }

    /// <inheritdoc/>
    public string PersistenceIdentity => this.HasPersistence
        ? AppStateStorageKey.Create(this.options.ApplicationId, this.options.ModuleId,
            this.options.StateName ?? this.StateId, this.options.InstanceKey, string.Empty, string.Empty, this.options.SchemaVersion)
        : null;

    /// <inheritdoc/>
    public bool CanUndo { get { lock (this.sync) { return this.undo.Count > 0; } } }

    /// <inheritdoc/>
    public bool CanRedo { get { lock (this.sync) { return this.redo.Count > 0; } } }

    /// <summary>Gets copied history snapshots in newest-to-oldest order.</summary>
    public IReadOnlyList<TState> UndoHistory => this.CopyHistory(this.undo);

    /// <summary>Gets copied redo snapshots in newest-to-oldest order.</summary>
    public IReadOnlyList<TState> RedoHistory => this.CopyHistory(this.redo);

    /// <inheritdoc/>
    public event Action<object> StateChanged;

    /// <inheritdoc/>
    public event Action<IStateChangeMetadata> StateChangeWithMetadata;

    /// <summary>Gets the current feature-owned model.</summary>
    /// <returns>The current model reference.</returns>
    protected abstract TState GetCurrentState();

    /// <summary>Replaces the feature-owned model during an accepted synchronous commit.</summary>
    /// <param name="state">The independent model owned by this instance.</param>
    protected abstract void UpdateState(TState state);

    /// <summary>Creates defaults without reading browser state.</summary>
    /// <returns>The default model.</returns>
    protected abstract TState CreateDefaultState();

    /// <summary>Copies a model independently of persistence enablement.</summary>
    /// <param name="value">The model to copy.</param>
    /// <returns>An independent model, or null for null input.</returns>
    /// <remarks>Override for immutable values or deliberate copying. Trimmed consumers supply generated JSON metadata.</remarks>
    protected virtual TState CloneState(TState value)
    {
        if (value == null)
        {
            return null;
        }

        if (this.options.JsonTypeInfo is JsonTypeInfo<TState> typeInfo)
        {
            return JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(value, typeInfo), typeInfo);
        }

        return JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(value, (JsonTypeInfo<TState>)JsonSerializerOptions.Default.GetTypeInfo(typeof(TState))), (JsonTypeInfo<TState>)JsonSerializerOptions.Default.GetTypeInfo(typeof(TState)));
    }

    /// <summary>Captures an independent model for edits or asynchronous work.</summary>
    /// <returns>An independent model snapshot.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var next = state.TakeSnapshot();
    /// next.SelectedIds.Add(42);
    /// Debug.Assert(!state.CurrentState.SelectedIds.Contains(42));
    /// ]]></code>
    /// </example>
    public TState TakeSnapshot() => this.CloneState(this.GetCurrentState());

    /// <summary>Accepts an independent snapshot as an ordinary edit.</summary>
    /// <param name="snapshot">The proposed values.</param>
    /// <param name="reason">Optional feature-local transition information.</param>
    /// <returns>Completion of the in-memory transition, without waiting for debounce.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var next = state.TakeSnapshot();
    /// next.Page = 2;
    /// await state.RestoreSnapshot(next);
    /// ]]></code>
    /// </example>
    public Task RestoreSnapshot(TState snapshot, string reason = null) => this.SetCurrentState(snapshot, reason);

    /// <summary>Checks whether an asynchronous result still belongs to the same edit and context.</summary>
    /// <param name="version">The captured version.</param>
    /// <returns>True only for the current nondisposed instance and version.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var version = state.Version;
    /// await Task.Delay(1);
    /// if (state.IsCurrent(version))
    /// {
    ///     Console.WriteLine("The captured catalog view is still current.");
    /// }
    /// ]]></code>
    /// </example>
    public bool IsCurrent(AppStateVersion version)
    {
        lock (this.sync)
        {
            return !this.disposed && this.IsOwnerContextCurrent()
                && version == new AppStateVersion(this.InstanceId, this.revision, this.contextRevision);
        }
    }

    /// <summary>Conditionally accepts an asynchronous result without overwriting newer edits.</summary>
    /// <param name="snapshot">The independently captured result.</param>
    /// <param name="expectedVersion">The originating version.</param>
    /// <param name="reason">Optional transition information.</param>
    /// <param name="cancellationToken">Cancels admission before commit.</param>
    /// <returns>False when the originating version is obsolete.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var version = state.Version;
    /// var next = state.TakeSnapshot();
    /// await Task.Delay(1);
    /// next.Page = 2;
    /// var accepted = await state.TryRestoreSnapshot(next, version, cancellationToken: CancellationToken.None);
    /// Debug.Assert(accepted);
    /// ]]></code>
    /// </example>
    public Task<bool> TryRestoreSnapshot(TState snapshot, AppStateVersion expectedVersion, string reason = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!this.IsCurrent(expectedVersion)) { return Task.FromResult(false); }

        return Task.FromResult(this.Commit(snapshot, "Restore", reason, expectedVersion));
    }

    /// <summary>Accepts an ordinary feature update and schedules optional persistence.</summary>
    /// <param name="newState">The proposed model, copied before acceptance.</param>
    /// <param name="reason">Optional transition information.</param>
    /// <returns>Completion of the accepted in-memory transition.</returns>
    protected virtual Task SetCurrentState(TState newState, string reason = null)
    {
        this.Commit(newState, "Update", reason);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task Undo() => this.MoveHistoryAsync(this.undo, this.redo, "Undo");

    /// <inheritdoc/>
    public Task Redo() => this.MoveHistoryAsync(this.redo, this.undo, "Redo");

    /// <inheritdoc/>
    public void AttachOwner(AppStateManager owner, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (this.owner != null && !ReferenceEquals(this.owner, owner))
        {
            throw new InvalidOperationException("A state cannot join another runtime owner.");
        }

        this.owner = owner;
        this.clock = timeProvider ?? TimeProvider.System;
        this.context = owner.CurrentContext;
        this.ownerContext = this.context;
    }

    /// <summary>Cancels owned work and detaches observers without disposing injected services.</summary>
    /// <example>
    /// <code><![CDATA[
    /// var owned = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
    /// owned.Dispose();
    /// ]]></code>
    /// </example>
    public void Dispose()
    {
        AppStateSubscription[] owned;
        lock (this.sync)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.contextRevision++;
            this.PersistenceStatus = AppStatePersistenceStatus.Disposed;
            owned = this.subscriptions.ToArray();
            this.subscriptions.Clear();
            this.StateChanged = null;
            this.StateChangeWithMetadata = null;
            this.undo.Clear();
            this.redo.Clear();
        }

        this.CancelOwned(this.lifetime);
        this.CancelOwned(this.generation);
        this.CancelPendingSave();
        foreach (var subscription in owned)
        {
            subscription.Dispose();
        }

        this.owner?.UnregisterState(this);
    }

    /// <summary>Waits at most one second for cooperative owned work after detaching immediately.</summary>
    /// <returns>Bounded asynchronous cleanup, without a final browser flush.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await using var owned = new CatalogViewState(
    /// NullLogger<CatalogViewState>.Instance, new AppStateOptions());
    /// await owned.SetPage(2);
    /// ]]></code>
    /// </example>
    public async ValueTask DisposeAsync()
    {
        this.Dispose();
        try
        {
            await Task.WhenAll(this.GetPendingTasks()).WaitAsync(TimeSpan.FromSeconds(1), this.clock);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            this.logger.LogDebug("AppState cleanup ended ({Outcome})", ex.GetType().Name);
        }
    }

    private bool HasPersistence => this.options.Enabled && this.storageProvider != null
        && this.options.PersistenceType != AppStatePersistenceType.ComponentScoped;

    private IReadOnlyList<TState> CopyHistory(List<TState> history)
    {
        TState[] values;
        lock (this.sync) { values = history.ToArray(); }

        return values.Reverse().Select(this.CloneState).ToArray();
    }

    private bool Commit(TState proposed, string operation, string reason = null, AppStateVersion? expected = null,
        List<TState> historySource = null, List<TState> historyDestination = null, string handoffKey = null)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        var value = this.CloneState(proposed);
        TState old;
        AppStateVersion accepted;
        lock (this.sync)
        {
            if (expected.HasValue && !this.IsCurrent(expected.Value))
            {
                return false;
            }

            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (operation == "Handoff")
            {
                if (this.hasHandoff || this.lastEditRevision != 0 || handoffKey != this.GetHandoffKey()) { return false; }
            }
            else if (operation == "Load" && this.hasHandoff) { return false; }

            old = this.CloneState(this.GetCurrentState());
            if (expected.HasValue && !this.IsCurrent(expected.Value)) { return false; }

            if (historySource != null)
            {
                historySource.RemoveAt(historySource.Count - 1);
                this.PushHistory(historyDestination, old);
            }
            else if (this.options.Enabled && this.options.HistoryEnabled && operation is "Update" or "Restore")
            {
                this.PushHistory(this.undo, old);
                this.redo.Clear();
            }

            this.UpdateState(value);
            if (operation == "Handoff") { this.hasHandoff = true; }

            this.revision++;
            if (operation is "Update" or "Restore" or "Undo" or "Redo") { this.lastEditRevision = this.revision; }

            accepted = new(this.InstanceId, this.revision, this.contextRevision);
        }

        this.Notify(old, value, operation, reason, accepted);
        if (this.IsGenerationCurrent(accepted) && operation is "Update" or "Restore" or "Undo" or "Redo")
        {
            this.ScheduleSave();
        }

        return true;
    }

    private Task MoveHistoryAsync(List<TState> source, List<TState> destination, string operation)
    {
        if (!this.options.Enabled || !this.options.HistoryEnabled) { return Task.CompletedTask; }

        TState next;
        AppStateVersion expected;
        lock (this.sync)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (source.Count == 0) { return Task.CompletedTask; }

            expected = this.Version;
            next = source[^1];
        }

        this.Commit(next, operation, expected: expected, historySource: source, historyDestination: destination);
        return Task.CompletedTask;
    }

    private void PushHistory(List<TState> history, TState value)
    {
        history.Add(value);
        if (history.Count > this.options.MaxHistoryItems) { history.RemoveAt(0); }
    }

    private void Notify(TState old, TState value, string operation, string reason, AppStateVersion version)
    {
        foreach (var handler in this.StateChanged?.GetInvocationList() ?? [])
        {
            if (!this.IsGenerationCurrent(version)) { return; }

            try
            {
                var observed = this.CloneState(value);
                if (!this.IsGenerationCurrent(version)) { return; }

                ((Action<object>)handler)(observed);
            }
            catch (Exception ex) { this.ObserverFailure(ex); }
        }

        if (!this.IsGenerationCurrent(version)) { return; }

        if (operation == "ContextReset") { old = null; }

        var metadata = new StateChangeMetadata<TState>
        {
            StateId = this.StateId, Version = version, Timestamp = this.clock.GetUtcNow(), Operation = operation,
            OldValue = old, NewValue = value, Reason = reason
        };
        foreach (var handler in this.StateChangeWithMetadata?.GetInvocationList() ?? [])
        {
            if (!this.IsGenerationCurrent(version)) { return; }

            try
            {
                var observed = metadata with { OldValue = this.CloneState(old), NewValue = this.CloneState(value) };
                if (!this.IsGenerationCurrent(version)) { return; }

                ((Action<IStateChangeMetadata>)handler)(observed);
            }
            catch (Exception ex) { this.ObserverFailure(ex); }
        }

        if (this.debugger?.IsEnabled == true && this.IsGenerationCurrent(version))
        {
            var observed = metadata with
            {
                OldValue = this.debugger.CapturesValues ? this.CloneState(old) : null,
                NewValue = this.debugger.CapturesValues ? this.CloneState(value) : null
            };
            if (this.IsGenerationCurrent(version)) { this.Observe(this.debugger.TrackStateChangeAsync(observed)); }
        }
    }

    private void CancelOwned(CancellationTokenSource source)
    {
        try { source?.Cancel(); }
        catch (Exception ex) { this.ObserverFailure(ex); }
    }

    private void ObserverFailure(Exception ex) => this.logger.LogWarning("AppState observer failed ({Outcome})", ex.GetType().Name);
}

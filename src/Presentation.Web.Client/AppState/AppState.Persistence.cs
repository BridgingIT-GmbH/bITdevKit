// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

public abstract partial class AppState<TState>
{
    private readonly SemaphoreSlim ioGate = new(1, 1);
    private readonly HashSet<Task> pending = [];
    private CancellationTokenSource saveDelay;
    private Task loadTask;
    private Task resetBarrier = Task.CompletedTask;
    private bool loaded;
    private string loadedKey;
    private string loadedPartition;

    /// <inheritdoc/>
    public Task Save() => this.Save(CancellationToken.None);

    /// <summary>Writes an independent current snapshot immediately, without debounce.</summary>
    /// <param name="cancellationToken">Cancels admission or the caller's wait.</param>
    /// <returns>The write acknowledgment; errors do not roll back memory state.</returns>
    /// <remarks>A cancelled write restores the preceding settled status only while its context remains current.</remarks>
    /// <example>
    /// <code><![CDATA[
    /// await state.Load();
    /// await state.Save(CancellationToken.None);
    /// ]]></code>
    /// </example>
    public Task Save(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.CancelPendingSave();
        if (!this.HasPersistence) { return Task.CompletedTask; }

        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (!this.loaded) { throw new InvalidOperationException("Load the current storage partition before saving."); }

        var version = this.Version;
        var payload = this.TakeSnapshot();
        var task = SaveCapturedAsync();
        this.Observe(task);
        return task.WaitAsync(cancellationToken);

        async Task SaveCapturedAsync()
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.lifetime.Token, this.generation.Token);
            await this.SaveCoreAsync(version, linked.Token, payload);
        }
    }

    /// <inheritdoc/>
    public virtual Task Load()
    {
        lock (this.sync)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (!this.HasPersistence) { return Task.CompletedTask; }

            if (this.loaded) { return Task.CompletedTask; }

            if (this.loadTask == null || this.loadTask.IsCompleted)
            {
                this.loadTask = this.LoadCoreAsync(this.Version, this.resetBarrier);
                this.Observe(this.loadTask);
            }

            return this.loadTask;
        }
    }

    /// <summary>Waits for the shared load through the existing virtual entry point.</summary>
    /// <param name="cancellationToken">Cancels only this wait.</param>
    /// <returns>The owner-local load attempt.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await state.Load(CancellationToken.None);
    /// ]]></code>
    /// </example>
    public Task Load(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return this.Load().WaitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public virtual Task Reset() => this.InvalidateAsync(this.context, "Reset");

    /// <summary>Waits for reset through its existing virtual override slot.</summary>
    /// <param name="cancellationToken">Cancels only this wait after admission.</param>
    /// <returns>The reset cleanup.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await state.Reset(CancellationToken.None);
    /// ]]></code>
    /// </example>
    public Task Reset(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return this.Reset().WaitAsync(cancellationToken);
    }

    /// <summary>Invalidates this state instance for an explicit identity and workspace context.</summary>
    /// <param name="context">The host-established opaque partition.</param>
    /// <returns>Cleanup of the old captured storage partition.</returns>
    /// <remarks>Use AppStateManager.ChangeContext for changes affecting all registered instances.</remarks>
    /// <example>
    /// <code><![CDATA[
    /// await using var owned = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
    /// await owned.ChangeContext(new AppStateContext(confirmedPrincipalKey, confirmedWorkspaceKey));
    /// ]]></code>
    /// </example>
    public Task ChangeContext(AppStateContext context) => this.InvalidateAsync(context, "ContextReset");

    /// <inheritdoc/>
    Task IAppStateRuntime.ChangeContext(AppStateContext context) => this.InvalidateAsync(context, "ContextReset", fromOwner: true);

    private Task InvalidateAsync(AppStateContext next, string operation, bool fromOwner = false)
    {
        string key;
        CancellationTokenSource previousGeneration;
        CancellationTokenSource previousDelay;
        Task previousReset;
        AppStateVersion version;
        TaskCompletionSource completion;
        lock (this.sync)
        {
            if (fromOwner && this.owner != null && !ReferenceEquals(next, this.owner.CurrentContext)) { return Task.CompletedTask; }

            ObjectDisposedException.ThrowIf(this.disposed, this);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            key = this.loadedKey;
            previousGeneration = this.generation;
            previousDelay = this.saveDelay;
            this.saveDelay = null;
            this.generation = new();
            this.context = next ?? new();
            if (fromOwner) { this.ownerContext = next; }

            this.contextRevision++;
            this.hasHandoff = false;
            this.lastEditRevision = 0;
            this.loaded = false;
            this.loadedKey = null;
            this.loadedPartition = null;
            this.loadTask = null;
            previousReset = this.resetBarrier;
            this.resetBarrier = completion.Task;
            this.undo.Clear();
            this.redo.Clear();
            this.PersistenceStatus = this.HasPersistence ? AppStatePersistenceStatus.NotLoaded : AppStatePersistenceStatus.NotConfigured;
            version = this.Version;
        }

        this.debugger?.Clear();
        Task cleanup;
        try { this.Commit(this.CreateDefaultState(), operation, expected: version); }
        finally
        {
            this.CancelOwned(previousDelay);
            this.CancelOwned(previousGeneration);
            cleanup = this.CompleteResetAsync(key, version, previousReset, completion);
            this.Observe(cleanup);
        }

        return cleanup;
    }

    private async Task CompleteResetAsync(string key, AppStateVersion version, Task previousReset, TaskCompletionSource completion)
    {
        try
        {
            // Nested resets must finish in admission order before a new-generation load can read.
            await previousReset;
            await this.ClearCapturedAsync(key, version);
        }
        finally { completion.TrySetResult(); }
    }

    private async Task ClearCapturedAsync(string key, AppStateVersion version)
    {
        if (!this.HasPersistence || key == null) { return; }

        await this.ioGate.WaitAsync(this.lifetime.Token);
        try
        {
            await this.storageProvider.ClearAsync(key, "v1", this.lifetime.Token);
            this.SetStatus(version, AppStatePersistenceStatus.NotLoaded);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            this.SetStatus(version, AppStatePersistenceStatus.Failed);
            this.StorageFailure(ex);
            throw;
        }
        finally { this.ioGate.Release(); }
    }

    private async Task LoadCoreAsync(AppStateVersion version, Task reset)
    {
        var generationToken = this.generation.Token;
        await Task.Yield();
        await reset.WaitAsync(this.lifetime.Token);
        if (!this.IsGenerationCurrent(version)) { return; }

        this.SetStatus(version, AppStatePersistenceStatus.Loading);
        await this.ioGate.WaitAsync(this.lifetime.Token);
        try
        {
            var key = await this.ResolveKeyAsync(version);
            if (key == null || !this.IsGenerationCurrent(version)) { return; }

            lock (this.sync)
            {
                if (!this.IsGenerationCurrent(version)) { return; }

                this.loadedKey = key;
            }

            TState value;
            var corrupt = false;
            try
            {
                value = await this.ReadAsync(key, "v1", generationToken);
            }
            catch (JsonException)
            {
                this.logger.LogWarning("AppState optional preferences were corrupt");
                value = null;
                corrupt = true;
            }

            if (!corrupt && value == null && this.options.LegacyStateId != null && this.options.LegacyUserContextId != null
                && this.LegacyPartitionMatches())
            {
                try
                {
                    value = await this.ReadAsync(this.options.LegacyStateId, this.options.LegacyUserContextId, generationToken);
                    if (value != null && this.IsCurrent(version))
                    {
                        await this.WriteAsync(key, "v1", this.CloneState(value), generationToken);
                        if (this.IsCurrent(version))
                        {
                            await this.storageProvider.ClearAsync(this.options.LegacyStateId, this.options.LegacyUserContextId, this.lifetime.Token);
                        }
                    }
                }
                catch (JsonException) { this.logger.LogWarning("AppState optional legacy preferences were corrupt"); }
            }

            if (!this.IsGenerationCurrent(version)) { return; }

            if (value != null) { this.Commit(value, "Load", expected: version); }

            lock (this.sync)
            {
                if (!this.IsGenerationCurrent(version)) { return; }

                this.loaded = true;
            }

            this.SetStatus(version, AppStatePersistenceStatus.Ready);
            if (Volatile.Read(ref this.lastEditRevision) > version.Revision)
            {
                this.ScheduleSave();
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
        {
            this.SetStatus(version, AppStatePersistenceStatus.Deferred);
            this.StorageFailure(ex);
        }
        catch (Exception ex)
        {
            this.SetStatus(version, AppStatePersistenceStatus.Failed);
            this.StorageFailure(ex);
            throw;
        }
        finally { this.ioGate.Release(); }
    }

    private async Task SaveCoreAsync(AppStateVersion version, CancellationToken cancellationToken, TState capturedPayload = null)
    {
        if (!this.HasPersistence) { return; }

        if (!this.IsGenerationCurrent(version)) { throw new OperationCanceledException("The state context changed before save."); }

        await this.ioGate.WaitAsync(cancellationToken);
        var previousStatus = this.PersistenceStatus;
        try
        {
            if (!this.IsGenerationCurrent(version)) { throw new OperationCanceledException("The state context changed before save."); }

            var key = await this.ResolveKeyAsync(version);
            if (key == null || !this.IsGenerationCurrent(version)) { throw new OperationCanceledException("The state context changed before save."); }

            if (!this.loaded)
            {
                this.SetStatus(version, AppStatePersistenceStatus.NotLoaded);
                throw new InvalidOperationException("Load the current storage partition before saving.");
            }

            var payload = capturedPayload ?? this.TakeSnapshot();
            this.SetStatus(version, AppStatePersistenceStatus.Saving);
            await this.WriteAsync(key, "v1", payload, cancellationToken);
            this.SetStatus(version, AppStatePersistenceStatus.Ready);
        }
        catch (OperationCanceledException)
        {
            this.SetStatus(version, previousStatus);
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
        {
            this.SetStatus(version, AppStatePersistenceStatus.Deferred);
            this.StorageFailure(ex);
            throw;
        }
        catch (Exception ex)
        {
            this.SetStatus(version, AppStatePersistenceStatus.Failed);
            this.StorageFailure(ex);
            throw;
        }
        finally { this.ioGate.Release(); }
    }

    private async Task<string> ResolveKeyAsync(AppStateVersion version)
    {
        var principal = this.context.PrincipalKey;
        var partition = principal != null ? "user:" + principal
            : await (this.userContextProvider?.GetUserContextId() ?? Task.FromResult("anonymous:runtime"));
        if (!partition.StartsWith("user:", StringComparison.Ordinal) && !partition.StartsWith("anonymous:", StringComparison.Ordinal))
        {
            partition = "user:" + partition;
        }

        if (!this.IsGenerationCurrent(version)) { return null; }

        if (this.userContextProvider is LocalStorageUserContextProvider provider && !provider.IsAvailable
            && this.options.PersistenceType == AppStatePersistenceType.LocalStorage
            && partition.StartsWith("anonymous:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Browser storage is not available; retry after interactive activation.");
        }

        if (this.loadedPartition != null && this.loadedPartition != partition)
        {
            // Release the I/O gate before serialized cleanup; clear values synchronously now.
            this.Observe(this.InvalidateAsync(this.context, "ContextReset"));
            this.PersistenceStatus = AppStatePersistenceStatus.Deferred;
            return null;
        }

        this.loadedPartition = partition;
        return AppStateStorageKey.Create(this.options.ApplicationId, this.options.ModuleId,
            this.options.StateName ?? this.StateId, this.options.InstanceKey, partition,
            this.context.WorkspaceKey != null ? "workspace:" + this.context.WorkspaceKey : "none", this.options.SchemaVersion);
    }

    private bool LegacyPartitionMatches() => (this.loadedPartition == this.options.LegacyUserContextId
        || this.loadedPartition == "user:" + this.options.LegacyUserContextId)
        && this.context.WorkspaceKey == this.options.LegacyWorkspaceKey;

    private Task<TState> ReadAsync(string key, string partition, CancellationToken token) =>
        this.options.JsonTypeInfo is JsonTypeInfo<TState> info
            ? this.storageProvider.LoadAsync(key, partition, info, token)
            : this.storageProvider.LoadAsync<TState>(key, partition, token);

    private Task WriteAsync(string key, string partition, TState value, CancellationToken token) =>
        this.options.JsonTypeInfo is JsonTypeInfo<TState> info
            ? this.storageProvider.SaveAsync(key, partition, value, info, token)
            : this.storageProvider.SaveAsync(key, partition, value, token);

    private void ScheduleSave()
    {
        if (!this.HasPersistence || !this.loaded || this.disposed) { return; }

        this.CancelPendingSave();
        var delay = CancellationTokenSource.CreateLinkedTokenSource(this.lifetime.Token, this.generation.Token);
        this.saveDelay = delay;
        this.Observe(this.SaveAfterDelayAsync(this.Version, delay));
    }

    private async Task SaveAfterDelayAsync(AppStateVersion version, CancellationTokenSource delay)
    {
        try
        {
            await Task.Delay(this.options.DebounceDelay, this.clock, delay.Token);
            await this.SaveCoreAsync(version, delay.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { this.StorageFailure(ex); }
        finally
        {
            lock (this.sync)
            {
                if (ReferenceEquals(this.saveDelay, delay)) { this.saveDelay = null; }
            }

            delay.Dispose();
        }
    }

    private void CancelPendingSave()
    {
        CancellationTokenSource delay;
        lock (this.sync)
        {
            delay = this.saveDelay;
            this.saveDelay = null;
        }

        this.CancelOwned(delay);
    }

    private bool IsGenerationCurrent(AppStateVersion version)
    {
        lock (this.sync) { return !this.disposed && this.IsOwnerContextCurrent() && this.contextRevision == version.ContextRevision; }
    }

    private bool IsOwnerContextCurrent() => this.owner == null || ReferenceEquals(this.ownerContext, this.owner.CurrentContext);

    private void SetStatus(AppStateVersion version, AppStatePersistenceStatus status)
    {
        lock (this.sync)
        {
            if (this.IsGenerationCurrent(version)) { this.PersistenceStatus = status; }
        }
    }

    private void StorageFailure(Exception ex) => this.logger.LogWarning("AppState persistence did not complete ({Outcome})", ex.GetType().Name);

    private void Observe(Task task)
    {
        if (task.IsCompletedSuccessfully) { return; }

        lock (this.sync) { this.pending.Add(task); }

        _ = this.ObserveCoreAsync(task);
    }

    private async Task ObserveCoreAsync(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception ex) { this.ObserverFailure(ex); }
        finally { lock (this.sync) { this.pending.Remove(task); } }
    }

    private Task[] GetPendingTasks() { lock (this.sync) { return this.pending.ToArray(); } }
}

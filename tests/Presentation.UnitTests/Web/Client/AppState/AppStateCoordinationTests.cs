// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies notification privacy and atomic coordination across competing operations.</summary>
public class AppStateCoordinationTests
{
    /// <summary>Reentrant invalidation prevents later observers and diagnostics from receiving obsolete values.</summary>
    /// <param name="metadataObserver">Whether the invalidation starts in a metadata observer.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoreSnapshot_ObserverChangesContext_StopsObsoleteNotifications(bool metadataObserver)
    {
        // Arrange
        var debugger = new AppStateDebugger(NullLogger<AppStateDebugger>.Instance, null,
            new AppStateDebugOptions { StateChangesTracked = true, ValueProjection = value => (value as CatalogViewModel)?.Search });
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions(), debugger: debugger);
        var observed = new List<string>();
        var metadata = new List<IStateChangeMetadata>();
        Task changing = null;
        if (metadataObserver)
        {
            sut.StateChangeWithMetadata += change =>
            {
                if (change.Operation == "Update") { changing = sut.ChangeContext(new("next-user", "north")); }
            };
        }
        else
        {
            sut.StateChanged += value =>
            {
                if (((CatalogViewModel)value).Search.Length != 0) { changing = sut.ChangeContext(new("next-user", "north")); }
            };
        }

        sut.StateChanged += value => observed.Add(((CatalogViewModel)value).Search);
        sut.StateChangeWithMetadata += metadata.Add;

        // Act
        await sut.RestoreSnapshot(new CatalogViewModel { Search = "previous-user-private" });
        await changing;

        // Assert
        sut.CurrentState.Search.ShouldBeEmpty();
        observed.Last().ShouldBeEmpty();
        metadata.ShouldHaveSingleItem().Operation.ShouldBe("ContextReset");
        metadata.Single().OldValue.ShouldBeNull();
        var tracked = debugger.GetStateChanges().ShouldHaveSingleItem();
        tracked.Operation.ShouldBe("ContextReset");
        tracked.NewValue.ShouldBe(string.Empty);
    }

    /// <summary>A conflicting edit leaves history unchanged when a prepared undo or redo is rejected.</summary>
    /// <param name="redo">Whether the interrupted operation moves the redo stack.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UndoRedo_ConcurrentEdit_DoesNotMoveHistoryForRejectedCommit(bool redo)
    {
        // Arrange
        using var gate = new ManualResetEventSlim();
        var copying = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sut = new CoordinatedState(new AppStateOptions { HistoryEnabled = true, MaxHistoryItems = 10 });
        await sut.SetPageAsync(1);
        await sut.SetPageAsync(2);
        if (redo) { await sut.Undo(); }

        sut.Copying = value =>
        {
            if (value?.Page != (redo ? 2 : 1)) { return; }

            sut.Copying = null;
            copying.TrySetResult();
            if (!gate.Wait(TimeSpan.FromSeconds(10))) { throw new TimeoutException("History copy was not released."); }
        };

        // Act
        var moving = Task.Run(() => redo ? sut.Redo() : sut.Undo());
        try
        {
            await copying.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await sut.SetPageAsync(3);
        }
        finally { gate.Set(); }

        await moving;

        // Assert
        sut.CurrentState.Page.ShouldBe(3);
        sut.UndoHistory.Select(value => value.Page).ShouldBe(redo ? [1, 0] : [2, 1, 0]);
        sut.CanRedo.ShouldBeFalse();
        await sut.Undo();
        sut.CurrentState.Page.ShouldBe(redo ? 1 : 2);
        await sut.Undo();
        sut.CurrentState.Page.ShouldBe(redo ? 0 : 1);
    }

    /// <summary>A load requested during reset notifications waits until captured-key cleanup finishes.</summary>
    [Fact]
    public async Task Reset_LoadDuringNotification_ReadsOnlyAfterCleanup()
    {
        // Arrange
        var store = new SessionScopedStateStoreProvider();
        using var gate = new ManualResetEventSlim();
        var notifying = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sut = new CoordinatedState(new AppStateOptions
        {
            PersistenceType = AppStatePersistenceType.SessionScoped,
            DebounceDelay = TimeSpan.FromHours(1)
        }, store);
        await sut.Load();
        await sut.SetPageAsync(8);
        await sut.Save();
        sut.StateChanged += static _ => { };
        sut.Copying = value =>
        {
            if (value?.Page != 0 || sut.CurrentState.Page != 0) { return; }

            sut.Copying = null;
            notifying.TrySetResult();
            if (!gate.Wait(TimeSpan.FromSeconds(10))) { throw new TimeoutException("Reset notification was not released."); }
        };

        // Act
        var resetting = Task.Run(() => sut.Reset());
        Task loading;
        try
        {
            await notifying.Task.WaitAsync(TimeSpan.FromSeconds(5));
            loading = StartLoadContinuation(sut);
        }
        finally { gate.Set(); }

        await Task.WhenAll(resetting, loading).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        sut.CurrentState.Page.ShouldBe(0);
        sut.CanUndo.ShouldBeFalse();
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.Ready);
        await sut.Load();
        sut.CurrentState.Page.ShouldBe(0);
    }

    /// <summary>Nested reset notifications cannot let a reload overtake an earlier cleanup.</summary>
    [Fact]
    public async Task Reset_ObserverResetsAgain_ReloadWaitsForAllCleanup()
    {
        // Arrange
        using var sut = new CoordinatedState(new AppStateOptions
        {
            PersistenceType = AppStatePersistenceType.SessionScoped,
            DebounceDelay = TimeSpan.FromHours(1)
        }, new SessionScopedStateStoreProvider());
        await sut.Load();
        await sut.SetPageAsync(8);
        await sut.Save();
        Task nestedReset = null;
        Task loading = null;
        var nested = false;
        sut.StateChangeWithMetadata += change =>
        {
            if (change.Operation != "Reset" || nested) { return; }

            nested = true;
            nestedReset = sut.Reset();
            loading = StartLoadContinuation(sut);
        };

        // Act
        var resetting = sut.Reset();
        await Task.WhenAll(resetting, nestedReset, loading).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        sut.CurrentState.Page.ShouldBe(0);
        sut.CanUndo.ShouldBeFalse();
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.Ready);
    }

    /// <summary>An invalidated transition cannot schedule a save after an observer resets and reloads.</summary>
    [Fact]
    public async Task RestoreSnapshot_ObserverResetsAndLoads_DoesNotSaveDefaults()
    {
        // Arrange
        var store = new CancellableStore();
        store.Write.TrySetResult();
        using var sut = new CoordinatedState(new AppStateOptions
        {
            PersistenceType = AppStatePersistenceType.CustomStorage,
            DebounceDelay = TimeSpan.Zero
        }, store);
        await sut.Load();
        Task resetting = null;
        Task loading = null;
        sut.StateChanged += value =>
        {
            if (((CatalogViewModel)value).Page != 2) { return; }

            resetting = sut.Reset();
            loading = StartLoadContinuation(sut);
        };

        // Act
        await sut.SetPageAsync(2);
        await Task.WhenAll(resetting, loading).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        sut.CurrentState.Page.ShouldBe(0);
        store.Writes.ShouldBe(0);
    }

    /// <summary>A cancelled write settles its status before completing and allows an explicit retry.</summary>
    [Fact]
    public async Task Save_ProviderCancels_RestoresSettledStatusAndAllowsRetry()
    {
        // Arrange
        var store = new CancellableStore();
        using var sut = new CatalogViewState(NullLogger<CatalogViewState>.Instance,
            new AppStateOptions { PersistenceType = AppStatePersistenceType.CustomStorage }, store);
        await sut.Load();

        // Act
        var saving = sut.Save();
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.Saving);
        store.Write.TrySetCanceled();
        await Should.ThrowAsync<OperationCanceledException>(saving);

        // Assert
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.Ready);
        store.Write = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Write.TrySetResult();
        await sut.Save();
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.Ready);
    }

    private static Task StartLoadContinuation(CoordinatedState state)
    {
        // Run the initial yielded continuation before releasing reset, without timing sleeps.
        var original = SynchronizationContext.Current;
        var context = new CapturedContinuation();
        Task loading;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            loading = state.Load();
        }
        finally { SynchronizationContext.SetSynchronizationContext(original); }

        context.Run();
        return loading;
    }

    private sealed class CapturedContinuation : SynchronizationContext
    {
        private SendOrPostCallback continuation;
        private object argument;

        /// <inheritdoc/>
        public override void Post(SendOrPostCallback callback, object state)
        {
            this.continuation = callback;
            this.argument = state;
        }

        /// <summary>Runs the captured initial load continuation.</summary>
        public void Run() => this.continuation(this.argument);
    }

    private sealed class CoordinatedState(AppStateOptions options, IAppStateStoreProvider store = null)
        : CatalogViewState(NullLogger<CatalogViewState>.Instance, options, store)
    {
        /// <summary>Gets or sets a bounded copy gate for deterministic operation interleaving.</summary>
        public Action<CatalogViewModel> Copying { get; set; }

        /// <inheritdoc/>
        protected override CatalogViewModel CloneState(CatalogViewModel value)
        {
            this.Copying?.Invoke(value);
            return base.CloneState(value);
        }
    }

    private sealed class CancellableStore : IAppStateStoreProvider
    {
        /// <summary>Gets or sets completion of the current provider write.</summary>
        public TaskCompletionSource Write { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the number of writes admitted to the provider.</summary>
        public int Writes { get; private set; }

        /// <inheritdoc/>
        public Task SaveAsync<T>(string stateId, string userContextId, T state)
        {
            this.Writes++;
            return this.Write.Task;
        }

        /// <inheritdoc/>
        public Task<T> LoadAsync<T>(string stateId, string userContextId) => Task.FromResult(default(T));

        /// <inheritdoc/>
        public Task ClearAsync(string stateId, string userContextId) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task ClearAllAsync() => Task.CompletedTask;
    }
}

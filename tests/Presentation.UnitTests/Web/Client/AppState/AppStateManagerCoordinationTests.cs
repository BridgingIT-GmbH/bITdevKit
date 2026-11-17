// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies latest-context propagation and recoverable lease cleanup.</summary>
public class AppStateManagerCoordinationTests
{
    /// <summary>A nested transition supersedes propagation to later instances, even when a context object is reused.</summary>
    /// <param name="reuseContext">Whether the newest transition reuses the original context object.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangeContext_ReentrantTransition_PreservesLatestContextAndEdits(bool reuseContext)
    {
        // Arrange
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AppStateManager>();
        var factory = scope.ServiceProvider.GetRequiredService<AppStateFactory>();
        using var first = factory.Create<CoordinatedState>("left");
        using var second = factory.Create<CoordinatedState>("right");
        var original = new AppStateContext("first-user", "first-workspace");
        var other = new AppStateContext("next-user", "next-workspace");
        var latest = reuseContext ? original : other;
        var nested = false;
        var transitions = new List<Task>();
        first.State.StateChanged += _ =>
        {
            if (nested) { return; }

            nested = true;
            transitions.Add(sut.ChangeContext(other));
            if (reuseContext) { transitions.Add(sut.ChangeContext(original)); }

            transitions.Add(second.State.SetPageAsync(9));
        };

        // Act
        transitions.Add(sut.ChangeContext(original));
        await Task.WhenAll(transitions).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        sut.CurrentContext.ShouldBe(latest);
        second.State.CurrentState.Page.ShouldBe(9);
        await AssertPartitionsAsync(scope.ServiceProvider, first.State, second.State, latest);
    }

    /// <summary>An older transition paused before its default commit cannot overwrite newer-context edits.</summary>
    [Fact]
    public async Task ChangeContext_ConcurrentTransition_RejectsObsoleteDefaultCommit()
    {
        // Arrange
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AppStateManager>();
        var factory = scope.ServiceProvider.GetRequiredService<AppStateFactory>();
        using var first = factory.Create<CoordinatedState>("left");
        using var second = factory.Create<CoordinatedState>("right");
        using var release = new ManualResetEventSlim();
        var preparing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invalidated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var latest = new AppStateContext("next-user", "next-workspace");
        first.State.PreparingDefaults = () =>
        {
            preparing.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) { throw new TimeoutException("Context preparation was not released."); }
        };
        second.State.StateChanged += _ =>
        {
            if (sut.CurrentContext == latest) { invalidated.TrySetResult(); }
        };

        // Act
        var older = Task.Run(() => sut.ChangeContext(new("first-user", "first-workspace")));
        Task newer = null;
        try
        {
            await preparing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            newer = Task.Run(() => sut.ChangeContext(latest));
            await invalidated.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await first.State.SetPageAsync(8);
            await second.State.SetPageAsync(9);
        }
        finally { release.Set(); }

        await Task.WhenAll(older, newer).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        sut.CurrentContext.ShouldBe(latest);
        first.State.CurrentState.Page.ShouldBe(8);
        second.State.CurrentState.Page.ShouldBe(9);
        await AssertPartitionsAsync(scope.ServiceProvider, first.State, second.State, latest);
    }

    /// <summary>A delayed owner call cannot reset an instance after its accepted context was superseded.</summary>
    [Fact]
    public async Task ChangeContext_ObsoleteOwnerCall_PreservesCurrentValuesAndPartition()
    {
        // Arrange
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AppStateManager>();
        var factory = scope.ServiceProvider.GetRequiredService<AppStateFactory>();
        using var first = factory.Create<CoordinatedState>("left");
        using var second = factory.Create<CoordinatedState>("right");
        await sut.ChangeContext(new("first-user", "first-workspace"));
        var obsolete = sut.CurrentContext;
        var latest = new AppStateContext("next-user", "next-workspace");
        await sut.ChangeContext(latest);
        await first.State.SetPageAsync(8);
        await second.State.SetPageAsync(9);

        // Act
        await ((IAppStateRuntime)first.State).ChangeContext(obsolete);

        // Assert
        first.State.CurrentState.Page.ShouldBe(8);
        second.State.CurrentState.Page.ShouldBe(9);
        await AssertPartitionsAsync(scope.ServiceProvider, first.State, second.State, latest);
    }

    /// <summary>Accepting a context invalidates tokens of later instances before notifying the first instance.</summary>
    [Fact]
    public async Task ChangeContext_BeforeLaterInstanceNotification_RejectsItsOldVersion()
    {
        // Arrange
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AppStateManager>();
        var factory = scope.ServiceProvider.GetRequiredService<AppStateFactory>();
        using var first = factory.Create<CoordinatedState>("left");
        using var second = factory.Create<CoordinatedState>("right");
        var previous = second.State.Version;
        var wasCurrent = true;
        Task<bool> restored = null;
        first.State.StateChanged += _ =>
        {
            wasCurrent = second.State.IsCurrent(previous);
            restored = second.State.TryRestoreSnapshot(new CatalogViewModel { Page = 88 }, previous);
        };

        // Act
        await sut.ChangeContext(new("next-user", "next-workspace"));

        // Assert
        wasCurrent.ShouldBeFalse();
        (await restored).ShouldBeFalse();
        second.State.CurrentState.Page.ShouldBe(0);
    }

    /// <summary>A rejected synchronous disposal retains ownership until asynchronous cleanup completes once.</summary>
    [Fact]
    public async Task Dispose_AsyncOnlyState_RejectsSyncAndPreservesAsyncCleanup()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging();
        services.AddAppState().AddState<AsyncOnlyState>().Done();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<AppStateManager>();
        var sut = scope.ServiceProvider.GetRequiredService<AppStateFactory>().Create<AsyncOnlyState>();

        // Act
        var error = Should.Throw<InvalidOperationException>(sut.Dispose);

        // Assert
        error.Message.ShouldContain(nameof(IAsyncDisposable.DisposeAsync));
        manager.GetAllStates().ShouldHaveSingleItem().ShouldBeSameAs(sut.State);
        sut.State.Disposals.ShouldBe(0);
        await sut.DisposeAsync();
        await sut.DisposeAsync();
        sut.Dispose();
        sut.State.Disposals.ShouldBe(1);
        manager.GetAllStates().ShouldBeEmpty();
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAppState().WithApplicationId("catalog-tests").AddState<CoordinatedState>()
            .WithIdentity("catalog", "filters").WithPersistence<RecordingStore>()
            .WithDebounceDelay(TimeSpan.FromHours(1)).Done();
        return services.BuildServiceProvider();
    }

    private static async Task AssertPartitionsAsync(IServiceProvider services, CoordinatedState first, CoordinatedState second,
        AppStateContext expected)
    {
        await first.Load();
        await second.Load();
        services.GetRequiredService<RecordingStore>().LoadedKeys.ShouldBe(new[] { "left", "right" }
            .Select(instance => AppStateStorageKey.Create("catalog-tests", "catalog", "filters", instance,
                "user:" + expected.PrincipalKey, "workspace:" + expected.WorkspaceKey, 1)));
    }

    private sealed class CoordinatedState : CatalogViewState
    {
        private Action preparingDefaults;

        /// <summary>Creates a state using the scope's selected store.</summary>
        /// <param name="options">The copied instance options.</param>
        /// <param name="store">The shared scope-owned storage provider.</param>
        public CoordinatedState(AppStateOptions options, IAppStateStoreProvider store)
            : base(NullLogger<CatalogViewState>.Instance, options, store) { }

        /// <summary>Sets a one-shot gate for preparing default values.</summary>
        public Action PreparingDefaults { set => this.preparingDefaults = value; }

        /// <inheritdoc/>
        protected override CatalogViewModel CreateDefaultState()
        {
            Interlocked.Exchange(ref this.preparingDefaults, null)?.Invoke();
            return base.CreateDefaultState();
        }
    }

    private sealed class RecordingStore : IAppStateStoreProvider
    {
        private readonly SessionScopedStateStoreProvider storage = new();

        /// <summary>Creates a provider whose operations record exact physical keys.</summary>
        public RecordingStore() { }

        /// <summary>Gets the keys requested by the two state instances.</summary>
        public List<string> LoadedKeys { get; } = [];

        /// <inheritdoc/>
        public Task SaveAsync<T>(string stateId, string userContextId, T state) => this.storage.SaveAsync(stateId, userContextId, state);

        /// <inheritdoc/>
        public Task<T> LoadAsync<T>(string stateId, string userContextId)
        {
            this.LoadedKeys.Add(stateId);
            return this.storage.LoadAsync<T>(stateId, userContextId);
        }

        /// <inheritdoc/>
        public Task ClearAsync(string stateId, string userContextId) => this.storage.ClearAsync(stateId, userContextId);

        /// <inheritdoc/>
        public Task ClearAllAsync() => throw new InvalidOperationException("Owner cleanup must not clear the shared provider.");
    }

    private sealed class AsyncOnlyState(AppStateOptions options) : IAppState, IAsyncDisposable
    {
        /// <summary>Gets the completed asynchronous disposal count.</summary>
        public int Disposals { get; private set; }

        /// <inheritdoc/>
        public string StateId => "async-only:" + options.InstanceKey;

        /// <inheritdoc/>
        public object CurrentState => new CatalogViewModel();

        /// <inheritdoc/>
        public bool CanUndo => false;

        /// <inheritdoc/>
        public bool CanRedo => false;

        /// <inheritdoc/>
        public event Action<object> StateChanged { add { } remove { } }

        /// <inheritdoc/>
        public event Action<IStateChangeMetadata> StateChangeWithMetadata { add { } remove { } }

        /// <inheritdoc/>
        public Task Save() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task Load() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task Reset() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task Undo() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task Redo() => Task.CompletedTask;

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            this.Disposals++;
            return ValueTask.CompletedTask;
        }
    }
}

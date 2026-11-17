// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises state leases, runtime inventories, and safe conditional writes.</summary>
public class AppStateOwnershipTests
{
    /// <summary>Independent leases cannot overwrite or unregister a sibling state.</summary>
    [Fact]
    public async Task Create_TwoInstances_IsolatesValuesAndDisposal()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAppState().AddState<CatalogViewState>().Done();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<AppStateFactory>();
        var manager = scope.ServiceProvider.GetRequiredService<AppStateManager>();
        using var first = factory.Create<CatalogViewState>();
        using var second = factory.Create<CatalogViewState>();
        await first.State.SetPageAsync(8);
        second.State.CurrentState.Page.ShouldBe(0);
        first.InstanceId.ShouldNotBe(second.InstanceId);
        manager.GetInstances().Count.ShouldBe(2);
        first.Dispose();
        manager.GetInstances().Count.ShouldBe(1);
        await second.State.SetPageAsync(9);
        second.State.CurrentState.Page.ShouldBe(9);
    }

    /// <summary>Each resolving runtime owns its own inventory and reset operation.</summary>
    [Fact]
    public async Task ResetAll_TwoScopes_DoesNotResetOtherRuntime()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAppState().AddState<CatalogViewState>().Done();
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var sut = first.ServiceProvider.GetRequiredService<CatalogViewState>();
        var other = second.ServiceProvider.GetRequiredService<CatalogViewState>();
        await sut.SetPageAsync(4);
        await other.SetPageAsync(7);
        await first.ServiceProvider.GetRequiredService<AppStateManager>().ResetAll();
        sut.CurrentState.Page.ShouldBe(0);
        other.CurrentState.Page.ShouldBe(7);
        first.ServiceProvider.GetRequiredService<AppStateManager>().GetAllStates().Single().ShouldBeSameAs(sut);
    }

    /// <summary>Old asynchronous results cannot overwrite a newer edit.</summary>
    [Fact]
    public async Task TryRestoreSnapshot_NewerEdit_RejectsOldResult()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAppState().AddState<CatalogViewState>().Done();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        var version = sut.Version;
        var captured = sut.TakeSnapshot();
        captured.Page = 99;
        await sut.SetPageAsync(2);
        (await sut.TryRestoreSnapshot(captured, version)).ShouldBeFalse();
        sut.CurrentState.Page.ShouldBe(2);
    }

    /// <summary>Forced context changes invalidate tokens and private values immediately.</summary>
    [Fact]
    public async Task ChangeContext_PrivateState_ResetsAndInvalidatesVersion()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAppState().AddState<CatalogViewState>().Done();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        await sut.SetPageAsync(5);
        var old = sut.Version;
        await scope.ServiceProvider.GetRequiredService<AppStateManager>().ChangeContext(new AppStateContext("user-b", "workspace-b"));
        sut.CurrentState.Page.ShouldBe(0);
        sut.IsCurrent(old).ShouldBeFalse();
    }

    /// <summary>A failed legacy reset is reported and removed from the usable manager inventory.</summary>
    [Fact]
    public async Task ChangeContext_FailedLegacyReset_DetachesUntilOwnerRecovers()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAppState();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AppStateManager>();
        var state = new LegacyState();
        sut.RegisterState(state);
        await Should.ThrowAsync<AggregateException>(() => sut.ChangeContext(new("new-principal")));
        sut.GetAllStates().ShouldBeEmpty();
        state.FailReset = false;
        await state.Reset();
        sut.RegisterState(state);
        sut.GetAllStates().Single().ShouldBeSameAs(state);
    }

    /// <summary>A component lease never disposes a provider owned by the resolving DI scope.</summary>
    [Fact]
    public void Dispose_Lease_RetainsSharedProviderUntilScopeEnds()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAppState().AddState<CatalogViewState>().WithPersistence<OwnedStore>().Done();
        using var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<OwnedStore>();
        var lease = scope.ServiceProvider.GetRequiredService<AppStateFactory>().Create<CatalogViewState>("left");
        lease.Dispose();
        store.IsDisposed.ShouldBeFalse();
        scope.Dispose();
        store.IsDisposed.ShouldBeTrue();
    }

    private sealed class OwnedStore : SessionScopedStateStoreProvider, IDisposable
    {
        /// <summary>Creates a provider whose disposal belongs to its DI scope.</summary>
        public OwnedStore() { }

        /// <summary>Gets whether the scoped provider has been disposed.</summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public void Dispose() => this.IsDisposed = true;
    }

    private sealed class LegacyState : IAppState
    {
        /// <summary>Gets or sets whether this legacy reset should fail.</summary>
        public bool FailReset { get; set; } = true;

        /// <inheritdoc/>
        public string StateId => "legacy";

        /// <inheritdoc/>
        public object CurrentState => new object();

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
        public Task Undo() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task Redo() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task Reset() => this.FailReset ? Task.FromException(new InvalidOperationException("Cannot reset")) : Task.CompletedTask;
    }
}

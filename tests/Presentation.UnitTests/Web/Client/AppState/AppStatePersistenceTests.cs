// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using System.Text.Json;
using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies real persistence acknowledgment, stale-load rejection, and serialized cleanup.</summary>
public class AppStatePersistenceTests
{
    /// <summary>A delayed load cannot overwrite an accepted edit.</summary>
    [Fact]
    public async Task Load_NewerEdit_PreservesInputAndSharesAttempt()
    {
        var store = new ControlledStore { LoadValue = new CatalogViewModel { Page = 8 }, HoldLoad = true };
        using var provider = CreateProvider(store);
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        var first = sut.Load();
        await store.LoadStarted.Task;
        var second = sut.Load();
        await sut.SetPageAsync(2);
        store.LoadRelease.TrySetResult();
        await Task.WhenAll(first, second);
        sut.CurrentState.Page.ShouldBe(2);
        store.Loads.ShouldBe(1);
        sut.CanUndo.ShouldBeTrue();
    }

    /// <summary>Explicit save waits for acknowledgment and reset clears after an uncooperative write.</summary>
    [Fact]
    public async Task Reset_SaveInFlight_ClearsAfterWriteAndDoesNotResaveDefaults()
    {
        var store = new ControlledStore();
        using var provider = CreateProvider(store);
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        await sut.Load();
        await sut.SetPageAsync(4);
        store.HoldSave = true;
        var saving = sut.Save();
        await store.SaveStarted.Task;
        saving.IsCompleted.ShouldBeFalse();
        var reset = sut.Reset();
        sut.CurrentState.Page.ShouldBe(0);
        store.Clears.ShouldBe(0);
        store.SaveRelease.TrySetResult();
        await Task.WhenAll(saving, reset);
        store.Clears.ShouldBe(1);
        store.Stored.ShouldBeNull();
        store.Saves.ShouldBe(1);
        sut.CanUndo.ShouldBeFalse();
    }

    /// <summary>A caller can stop waiting without cancelling another caller's shared load.</summary>
    [Fact]
    public async Task Load_CallerCancels_DoesNotCancelOtherWaiter()
    {
        var store = new ControlledStore { HoldLoad = true };
        using var provider = CreateProvider(store);
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        using var cancellation = new CancellationTokenSource();
        var waiting = sut.Load(cancellation.Token);
        await store.LoadStarted.Task;
        var other = sut.Load();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(waiting);
        other.IsCompleted.ShouldBeFalse();
        store.LoadRelease.TrySetResult();
        await other;
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.Ready);
    }

    /// <summary>Coalesced automatic saves use virtual time and write only the newest values.</summary>
    [Fact]
    public async Task Save_RapidEdits_CoalescesWithoutDelayingTransitions()
    {
        var store = new ControlledStore();
        var clock = new FakeTimeProvider();
        using var provider = CreateProvider(store, clock, TimeSpan.FromSeconds(1));
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        await sut.Load();
        for (var page = 1; page <= 10; page++) { await sut.SetPageAsync(page); }

        store.Saves.ShouldBe(0);
        clock.Advance(TimeSpan.FromSeconds(1));
        await store.SaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        store.Saves.ShouldBe(1);
        JsonSerializer.Deserialize<CatalogViewModel>(store.Stored).Page.ShouldBe(10);
    }

    /// <summary>Context invalidation immediately clears private values while old reads remain in flight.</summary>
    [Fact]
    public async Task ChangeContext_LoadInFlight_DiscardsOldPayload()
    {
        var store = new ControlledStore { HoldLoad = true, LoadValue = new CatalogViewModel { Page = 99 } };
        using var provider = CreateProvider(store);
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        var loading = sut.Load();
        await store.LoadStarted.Task;
        var changing = scope.ServiceProvider.GetRequiredService<AppStateManager>().ChangeContext(new("other", "workspace"));
        sut.CurrentState.Page.ShouldBe(0);
        store.LoadRelease.TrySetResult();
        await Task.WhenAll(loading, changing);
        sut.CurrentState.Page.ShouldBe(0);
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.NotLoaded);
    }

    /// <summary>Manual failures remain observable without undoing an accepted in-memory edit.</summary>
    [Fact]
    public async Task Save_ProviderFails_RetainsAcceptedStateAndReportsFailure()
    {
        var store = new ControlledStore();
        using var provider = CreateProvider(store);
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        await sut.Load();
        await sut.SetPageAsync(7);
        store.FailSave = true;
        await Should.ThrowAsync<IOException>(sut.Save());
        sut.CurrentState.Page.ShouldBe(7);
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.Failed);
    }

    private static ServiceProvider CreateProvider(ControlledStore store, TimeProvider clock = null, TimeSpan? debounce = null)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<TimeProvider>(clock ?? TimeProvider.System);
        services.AddScoped(_ => store);
        services.AddAppState().WithApplicationId("catalog-tests").AddState<CatalogViewState>()
            .WithPersistence<ControlledStore>().WithHistory(3).WithDebounceDelay(debounce ?? TimeSpan.FromHours(1)).Done();
        return services.BuildServiceProvider();
    }

    /// <summary>Implements only the old provider contract and deliberately ignores cancellation.</summary>
    public sealed class ControlledStore : IAppStateStoreProvider
    {
        /// <summary>Gets or sets the initial loaded value.</summary>
        public CatalogViewModel LoadValue { get; set; }

        /// <summary>Gets or sets whether the read is held.</summary>
        public bool HoldLoad { get; set; }

        /// <summary>Gets or sets whether the write is held.</summary>
        public bool HoldSave { get; set; }

        /// <summary>Gets or sets whether writes fail.</summary>
        public bool FailSave { get; set; }

        /// <summary>Gets the read-start signal.</summary>
        public TaskCompletionSource LoadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the read-release control.</summary>
        public TaskCompletionSource LoadRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the write-start signal.</summary>
        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the write-release control.</summary>
        public TaskCompletionSource SaveRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the completed read count.</summary>
        public int Loads { get; private set; }

        /// <summary>Gets the accepted write count.</summary>
        public int Saves { get; private set; }

        /// <summary>Gets the clear count.</summary>
        public int Clears { get; private set; }

        /// <summary>Gets the serialized persisted payload.</summary>
        public string Stored { get; private set; }

        /// <inheritdoc/>
        public async Task<T> LoadAsync<T>(string stateId, string userContextId)
        {
            this.Loads++;
            this.LoadStarted.TrySetResult();
            if (this.HoldLoad) { await this.LoadRelease.Task; }

            return this.LoadValue == null ? default : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(this.LoadValue));
        }

        /// <inheritdoc/>
        public async Task SaveAsync<T>(string stateId, string userContextId, T state)
        {
            if (this.FailSave) { throw new IOException("controlled storage failure"); }

            this.Saves++;
            this.Stored = JsonSerializer.Serialize(state);
            this.SaveStarted.TrySetResult();
            if (this.HoldSave) { await this.SaveRelease.Task; }
        }

        /// <inheritdoc/>
        public Task ClearAsync(string stateId, string userContextId)
        {
            this.Clears++;
            this.Stored = null;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task ClearAllAsync() => throw new InvalidOperationException("Manager must not clear provider-wide storage.");
    }
}

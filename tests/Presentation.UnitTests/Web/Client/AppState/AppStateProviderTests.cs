// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

/// <summary>Verifies stable anonymous keys, safe imports, and copied provider values.</summary>
public class AppStateProviderTests
{
    /// <summary>Anonymous partition identity survives repeated calls and provider reconstruction.</summary>
    [Fact]
    public async Task AnonymousPartition_RepeatedCallsAndReload_UsesStableApplicationKey()
    {
        var js = new StorageJsRuntime();
        var sut = new LocalStorageUserContextProvider(js, null, "catalog");
        var original = await sut.GetUserContextId();
        (await sut.GetUserContextId()).ShouldBe(original);
        (await new LocalStorageUserContextProvider(js, null, "catalog").GetUserContextId()).ShouldBe(original);
        (await new LocalStorageUserContextProvider(js, null, "other").GetUserContextId()).ShouldNotBe(original);
    }

    /// <summary>Memory providers copy both submitted and loaded mutable values.</summary>
    [Fact]
    public async Task MemoryStorage_MutableValues_DoesNotRetainLiveReferences()
    {
        var sut = new SessionScopedStateStoreProvider();
        var model = new CatalogViewModel { Selected = [1] };
        await sut.SaveAsync("view", "user", model);
        model.Selected.Add(2);
        var loaded = await sut.LoadAsync<CatalogViewModel>("view", "user");
        loaded.Selected.ShouldBe([1]);
        loaded.Selected.Add(3);
        (await sut.LoadAsync<CatalogViewModel>("view", "user")).Selected.ShouldBe([1]);
    }

    /// <summary>Canonical dimensions cannot collide through delimiters or Unicode.</summary>
    [Fact]
    public void StorageKey_AmbiguousDelimiters_ProducesDistinctKeys()
    {
        var first = AppStateStorageKey.Create("a:b", "c", "catalog", "left", "user:one", "none", 1);
        var second = AppStateStorageKey.Create("a", "b:c", "catalog", "left", "user:one", "none", 1);
        first.ShouldNotBe(second);
        AppStateStorageKey.Resolve(first, "v1").ShouldBe(first);
        AppStateStorageKey.Create("日本語", "c", "catalog", "left", "user:one", "none", 1).ShouldNotBe(first);
    }

    /// <summary>Only a declared same-partition key is imported and cleared after success.</summary>
    [Fact]
    public async Task Load_ExplicitLegacyKey_ImportsWithoutScanningOtherPartitions()
    {
        var storage = new SessionScopedStateStoreProvider();
        await storage.SaveAsync("old", "fixture", new CatalogViewModel { Page = 21 });
        await storage.SaveAsync("other", "someone-else", new CatalogViewModel { Page = 99 });
        var services = new ServiceCollection().AddLogging();
        services.AddScoped(_ => storage);
        services.AddScoped<IUserContextProvider>(_ => new FixedUser());
        services.AddAppState().AddState<CatalogViewState>().AsInMemoryScoped().WithLegacyStorageKey("old", "fixture").Done();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        await sut.Load();
        sut.CurrentState.Page.ShouldBe(21);
        (await storage.LoadAsync<CatalogViewModel>("old", "fixture")).ShouldBeNull();
        (await storage.LoadAsync<CatalogViewModel>("other", "someone-else")).Page.ShouldBe(99);
    }

    /// <summary>Unavailable JS remains retryable instead of masquerading as a completed load.</summary>
    [Fact]
    public async Task Load_BrowserUnavailable_RetriesAfterActivation()
    {
        var js = new StorageJsRuntime { Unavailable = true };
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IJSRuntime>(_ => js);
        services.AddAppState().AddState<CatalogViewState>().AsLocalStorageScoped().Done();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        await sut.Load();
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.Deferred);
        js.Unavailable = false;
        await sut.Load();
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.Ready);
    }

    /// <summary>A declared legacy key cannot populate another workspace.</summary>
    [Fact]
    public async Task LegacyImport_DifferentWorkspace_DoesNotReadOrRemoveOldValue()
    {
        var storage = new SessionScopedStateStoreProvider();
        await storage.SaveAsync("old", "fixture", new CatalogViewModel { Page = 21 });
        var services = new ServiceCollection().AddLogging();
        services.AddScoped(_ => storage);
        services.AddAppState().AddState<CatalogViewState>().AsInMemoryScoped()
            .WithLegacyStorageKey("old", "fixture", "original-workspace").Done();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppStateManager>().ChangeContext(new("fixture", "other-workspace"));
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        await sut.Load();
        sut.CurrentState.Page.ShouldBe(0);
        (await storage.LoadAsync<CatalogViewModel>("old", "fixture")).Page.ShouldBe(21);
    }

    /// <summary>A corrupt canonical value falls back to defaults instead of reviving an old preference.</summary>
    [Fact]
    public async Task Load_CorruptCanonical_DoesNotImportLegacyValue()
    {
        var js = new StorageJsRuntime();
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IJSRuntime>(_ => js);
        services.AddAppState().AddState<CatalogViewState>().AsLocalStorageScoped().WithLegacyStorageKey("old", "fixture").Done();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppStateManager>().ChangeContext(new("fixture"));
        var sut = scope.ServiceProvider.GetRequiredService<CatalogViewState>();
        var key = AppStateStorageKey.Create("default", "host", sut.StateId, "default", "user:fixture", "none", 1);
        js.Values[key] = "{invalid";
        js.Values["old_fixture"] = "{\"Page\":21}";
        await sut.Load();
        sut.CurrentState.Page.ShouldBe(0);
        sut.PersistenceStatus.ShouldBe(AppStatePersistenceStatus.Ready);
        js.Values.ContainsKey("old_fixture").ShouldBeTrue();
    }

    private sealed class FixedUser : IUserContextProvider
    {
        /// <inheritdoc/>
        public Task<string> GetUserContextId() => Task.FromResult("fixture");

        /// <inheritdoc/>
        public Task Clear() => Task.CompletedTask;
    }

    /// <summary>Implements a deterministic local-storage browser boundary for tests.</summary>
    public sealed class StorageJsRuntime : IJSRuntime
    {
        /// <summary>Gets or sets whether browser operations are currently unavailable.</summary>
        public bool Unavailable { get; set; }

        /// <summary>Gets the browser-owned persistent key/value fixture.</summary>
        public Dictionary<string, string> Values { get; } = [];

        /// <inheritdoc/>
        public ValueTask<T> InvokeAsync<T>(string identifier, object[] args) => this.InvokeAsync<T>(identifier, CancellationToken.None, args);

        /// <inheritdoc/>
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object[] args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this.Unavailable) { throw new InvalidOperationException("prerender"); }

            object result = null;
            var key = (string)args[0];
            if (identifier == "localStorage.getItem") { this.Values.TryGetValue(key, out var value); result = value; }
            else if (identifier == "localStorage.setItem") { this.Values[key] = (string)args[1]; }
            else if (identifier == "localStorage.removeItem") { this.Values.Remove(key); }

            return ValueTask.FromResult(result is T typed ? typed : default);
        }
    }
}

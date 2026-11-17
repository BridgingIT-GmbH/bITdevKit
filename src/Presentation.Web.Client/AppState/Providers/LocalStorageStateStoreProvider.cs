// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.JSInterop;

/// <summary>Stores explicitly selected safe preferences in browser local storage.</summary>
/// <example>
/// <code><![CDATA[
/// // jsRuntime comes from the interactive browser/circuit after interactivity is ready.
/// var storage = new LocalStorageStateStoreProvider(jsRuntime, debugger);
/// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel { Page = 2 },
///     CatalogJson.Default.CatalogViewModel);
/// ]]></code>
/// </example>
public class LocalStorageStateStoreProvider : IAppStateStoreProvider
{
    private readonly IJSRuntime jsRuntime;
    private readonly ConcurrentDictionary<string, byte> keys = new(StringComparer.Ordinal);

    /// <summary>Creates the provider without performing browser I/O.</summary>
    /// <param name="jsRuntime">The originating interactive runtime; null defers browser operations.</param>
    /// <param name="debugger">The optional observer, retained for constructor compatibility.</param>
    /// <example>
    /// <code><![CDATA[
    /// var storage = new LocalStorageStateStoreProvider(jsRuntime, debugger);
    /// ]]></code>
    /// </example>
    public LocalStorageStateStoreProvider(IJSRuntime jsRuntime, AppStateDebugger debugger)
    {
        this.jsRuntime = jsRuntime;
        _ = debugger;
    }

    /// <inheritdoc/>
    public Task SaveAsync<T>(string stateId, string userContextId, T state) =>
        this.SaveAsync(stateId, userContextId, state, null, CancellationToken.None);

    /// <inheritdoc/>
    public Task SaveAsync<T>(string stateId, string userContextId, T state, CancellationToken cancellationToken) =>
        this.SaveAsync(stateId, userContextId, state, null, cancellationToken);

    /// <inheritdoc/>
    public async Task SaveAsync<T>(string stateId, string userContextId, T state, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        var key = AppStateStorageKey.Resolve(stateId, userContextId);
        var json = typeInfo != null ? JsonSerializer.Serialize(state, typeInfo) : JsonSerializer.Serialize(state, (JsonTypeInfo<T>)JsonSerializerOptions.Default.GetTypeInfo(typeof(T)));
        await this.Runtime.InvokeVoidAsync("localStorage.setItem", cancellationToken, key, json);
        this.keys[key] = 0;
    }

    /// <inheritdoc/>
    public Task<T> LoadAsync<T>(string stateId, string userContextId) =>
        this.LoadAsync<T>(stateId, userContextId, null, CancellationToken.None);

    /// <inheritdoc/>
    public Task<T> LoadAsync<T>(string stateId, string userContextId, CancellationToken cancellationToken) =>
        this.LoadAsync<T>(stateId, userContextId, null, cancellationToken);

    /// <inheritdoc/>
    public async Task<T> LoadAsync<T>(string stateId, string userContextId, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        var key = AppStateStorageKey.Resolve(stateId, userContextId);
        var json = await this.Runtime.InvokeAsync<string>("localStorage.getItem", cancellationToken, key);
        if (json == null) { return default; }

        this.keys[key] = 0;
        return typeInfo != null ? JsonSerializer.Deserialize(json, typeInfo) : JsonSerializer.Deserialize(json, (JsonTypeInfo<T>)JsonSerializerOptions.Default.GetTypeInfo(typeof(T)));
    }

    /// <inheritdoc/>
    public Task ClearAsync(string stateId, string userContextId) => this.ClearAsync(stateId, userContextId, CancellationToken.None);

    /// <inheritdoc/>
    public async Task ClearAsync(string stateId, string userContextId, CancellationToken cancellationToken)
    {
        var key = AppStateStorageKey.Resolve(stateId, userContextId);
        await this.Runtime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, key);
        this.keys.TryRemove(key, out _);
    }

    /// <inheritdoc/>
    public async Task ClearAllAsync()
    {
        foreach (var key in this.keys.Keys)
        {
            await this.Runtime.InvokeVoidAsync("localStorage.removeItem", key);
            this.keys.TryRemove(key, out _);
        }
    }

    private IJSRuntime Runtime => this.jsRuntime ?? throw new InvalidOperationException("Browser storage requires interactive JS.");
}

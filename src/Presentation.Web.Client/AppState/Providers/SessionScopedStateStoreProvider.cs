// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.JSInterop;

/// <summary>Stores independent serialized preferences only within the resolving runtime scope.</summary>
/// <example>
/// <code><![CDATA[
/// var storage = new SessionScopedStateStoreProvider();
/// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel { Page = 2 });
/// var restored = await storage.LoadAsync<CatalogViewModel>("catalog-list", "anonymous:preview");
/// Debug.Assert(restored.Page == 2);
/// ]]></code>
/// </example>
public class SessionScopedStateStoreProvider : IAppStateStoreProvider
{
    private readonly ConcurrentDictionary<string, byte[]> values = new(StringComparer.Ordinal);

    /// <summary>Creates an in-memory provider without requiring browser services.</summary>
    /// <example>
    /// <code><![CDATA[
    /// var storage = new SessionScopedStateStoreProvider();
    /// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel());
    /// ]]></code>
    /// </example>
    public SessionScopedStateStoreProvider() { }

    /// <summary>Preserves the historical constructor; diagnostic dependencies remain DI-owned.</summary>
    /// <param name="debugger">The optional observer.</param>
    /// <param name="jsRuntime">The optional browser runtime, unused for memory storage.</param>
    /// <example>
    /// <code><![CDATA[
    /// var storage = new SessionScopedStateStoreProvider(debugger, jsRuntime);
    /// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel());
    /// ]]></code>
    /// </example>
    public SessionScopedStateStoreProvider(AppStateDebugger debugger, IJSRuntime jsRuntime) { }

    /// <inheritdoc/>
    public Task SaveAsync<T>(string stateId, string userContextId, T state) =>
        this.SaveAsync(stateId, userContextId, state, null, CancellationToken.None);

    /// <inheritdoc/>
    public Task SaveAsync<T>(string stateId, string userContextId, T state, CancellationToken cancellationToken) =>
        this.SaveAsync(stateId, userContextId, state, null, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync<T>(string stateId, string userContextId, T state, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.values[AppStateStorageKey.Resolve(stateId, userContextId)] = typeInfo != null
            ? JsonSerializer.SerializeToUtf8Bytes(state, typeInfo) : JsonSerializer.SerializeToUtf8Bytes(state, (JsonTypeInfo<T>)JsonSerializerOptions.Default.GetTypeInfo(typeof(T)));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<T> LoadAsync<T>(string stateId, string userContextId) =>
        this.LoadAsync<T>(stateId, userContextId, null, CancellationToken.None);

    /// <inheritdoc/>
    public Task<T> LoadAsync<T>(string stateId, string userContextId, CancellationToken cancellationToken) =>
        this.LoadAsync<T>(stateId, userContextId, null, cancellationToken);

    /// <inheritdoc/>
    public Task<T> LoadAsync<T>(string stateId, string userContextId, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(this.values.TryGetValue(AppStateStorageKey.Resolve(stateId, userContextId), out var bytes)
            ? typeInfo != null ? JsonSerializer.Deserialize(bytes, typeInfo) : JsonSerializer.Deserialize(bytes, (JsonTypeInfo<T>)JsonSerializerOptions.Default.GetTypeInfo(typeof(T))) : default);
    }

    /// <inheritdoc/>
    public Task ClearAsync(string stateId, string userContextId) => this.ClearAsync(stateId, userContextId, CancellationToken.None);

    /// <inheritdoc/>
    public Task ClearAsync(string stateId, string userContextId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.values.TryRemove(AppStateStorageKey.Resolve(stateId, userContextId), out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAllAsync()
    {
        this.values.Clear();
        return Task.CompletedTask;
    }
}

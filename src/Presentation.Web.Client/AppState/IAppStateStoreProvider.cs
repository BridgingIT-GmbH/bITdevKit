// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Defines the contract for state storage providers that persist and retrieve state data.
/// </summary>
/// <example>
/// <code><![CDATA[
/// IAppStateStoreProvider storage = new SessionScopedStateStoreProvider();
/// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel { Page = 2 });
/// var restored = await storage.LoadAsync<CatalogViewModel>("catalog-list", "anonymous:preview");
/// Debug.Assert(restored.Page == 2);
/// ]]></code>
/// </example>
public interface IAppStateStoreProvider
{
    /// <summary>
    /// Saves the state to the underlying storage.
    /// </summary>
    /// <typeparam name="T">The type of the state object.</typeparam>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context, ensuring isolation.</param>
    /// <param name="state">The state object to save.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel { Page = 2 });
    /// ]]></code>
    /// </example>
    Task SaveAsync<T>(string stateId, string userContextId, T state);

    /// <summary>
    /// Loads the state from the underlying storage.
    /// </summary>
    /// <typeparam name="T">The type of the state object.</typeparam>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context.</param>
    /// <returns>A task that returns the loaded state, or default if not found.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var restored = await storage.LoadAsync<CatalogViewModel>("catalog-list", "anonymous:preview");
    /// var current = restored ?? new CatalogViewModel();
    /// ]]></code>
    /// </example>
    Task<T> LoadAsync<T>(string stateId, string userContextId);

    /// <summary>
    /// Asynchronously clears all state data for the given state and user context from the underlying storage.
    /// </summary>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context.</param>
    /// <returns>A task representing the asynchronous clear operation.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await storage.ClearAsync("catalog-list", "anonymous:preview");
    /// ]]></code>
    /// </example>
    Task ClearAsync(string stateId, string userContextId);

    /// <summary>
    /// Asynchronously clears all state data from the underlying storage.
    /// </summary>
    /// <example>
    /// <code><![CDATA[
    /// await storage.ClearAllAsync(); // Explicit provider-local maintenance only.
    /// ]]></code>
    /// </example>
    Task ClearAllAsync();

    /// <summary>Saves with cancellation; providers without cancellation support can complete after cancellation.</summary>
    /// <typeparam name="T">The stored model type.</typeparam>
    /// <param name="stateId">The canonical or legacy state key.</param>
    /// <param name="userContextId">The legacy partition.</param>
    /// <param name="state">The independently captured value.</param>
    /// <param name="cancellationToken">Cancels admission or cooperative provider I/O.</param>
    /// <returns>The save operation.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel { Page = 2 }, CancellationToken.None);
    /// ]]></code>
    /// </example>
    async Task SaveAsync<T>(string stateId, string userContextId, T state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await this.SaveAsync(stateId, userContextId, state);
    }

    /// <summary>Loads with cancellation through the provider's basic overload.</summary>
    /// <typeparam name="T">The stored model type.</typeparam>
    /// <param name="stateId">The canonical or legacy state key.</param>
    /// <param name="userContextId">The legacy partition.</param>
    /// <param name="cancellationToken">Cancels admission or cooperative provider I/O.</param>
    /// <returns>The stored value or default.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var restored = await storage.LoadAsync<CatalogViewModel>("catalog-list", "anonymous:preview", CancellationToken.None);
    /// var current = restored ?? new CatalogViewModel();
    /// ]]></code>
    /// </example>
    async Task<T> LoadAsync<T>(string stateId, string userContextId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await this.LoadAsync<T>(stateId, userContextId);
    }

    /// <summary>Clears one captured key with cancellation.</summary>
    /// <param name="stateId">The state key.</param>
    /// <param name="userContextId">The legacy partition.</param>
    /// <param name="cancellationToken">Cancels admission or cooperative provider I/O.</param>
    /// <returns>The clear operation.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await storage.ClearAsync("catalog-list", "anonymous:preview", CancellationToken.None);
    /// ]]></code>
    /// </example>
    async Task ClearAsync(string stateId, string userContextId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await this.ClearAsync(stateId, userContextId);
    }

    /// <summary>Saves using generated JSON metadata when supported by the provider.</summary>
    /// <typeparam name="T">The stored type.</typeparam>
    /// <param name="stateId">The state key.</param>
    /// <param name="userContextId">The legacy partition.</param>
    /// <param name="state">The captured payload.</param>
    /// <param name="typeInfo">The generated serialization metadata.</param>
    /// <param name="cancellationToken">Cancels cooperative work.</param>
    /// <returns>The save operation.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel { Page = 2 }, CatalogJson.Default.CatalogViewModel, CancellationToken.None);
    /// ]]></code>
    /// </example>
    Task SaveAsync<T>(string stateId, string userContextId, T state,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default) =>
        this.SaveAsync(stateId, userContextId, state, cancellationToken);

    /// <summary>Loads using generated JSON metadata when supported by the provider.</summary>
    /// <typeparam name="T">The stored type.</typeparam>
    /// <param name="stateId">The state key.</param>
    /// <param name="userContextId">The legacy partition.</param>
    /// <param name="typeInfo">The generated serialization metadata.</param>
    /// <param name="cancellationToken">Cancels cooperative work.</param>
    /// <returns>The stored value or default.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var restored = await storage.LoadAsync<CatalogViewModel>("catalog-list", "anonymous:preview", CatalogJson.Default.CatalogViewModel, CancellationToken.None);
    /// var current = restored ?? new CatalogViewModel();
    /// ]]></code>
    /// </example>
    Task<T> LoadAsync<T>(string stateId, string userContextId,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default) =>
        this.LoadAsync<T>(stateId, userContextId, cancellationToken);
}

// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// A state store provider that does not persist state, simulating ComponentScoped behavior where state is lost on navigation or refresh.
/// </summary>
/// <example>
/// <code><![CDATA[
/// var storage = new ComponentScopedStateStoreProvider();
/// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel());
/// var restored = await storage.LoadAsync<CatalogViewModel>("catalog-list", "anonymous:preview");
/// Debug.Assert(restored is null); // This provider deliberately retains no values.
/// ]]></code>
/// </example>
public class ComponentScopedStateStoreProvider : IAppStateStoreProvider
{
    /// <summary>
    /// Asynchronously saves the state, but does nothing as ComponentScoped state is not persisted.
    /// </summary>
    /// <typeparam name="T">The type of the state object.</typeparam>
    /// <param name="stateId">A unique identifier for the state (ignored in this implementation).</param>
    /// <param name="userContextId">A unique identifier for the user context (ignored in this implementation).</param>
    /// <param name="state">The state object to save (ignored in this implementation).</param>
    /// <returns>A completed task, as no actual saving occurs.</returns>
    /// <example>
    /// <code><![CDATA[
    /// IAppStateStoreProvider storage = new ComponentScopedStateStoreProvider();
    /// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel { Page = 2 });
    /// ]]></code>
    /// </example>
    public Task SaveAsync<T>(string stateId, string userContextId, T state)
    {
        // No-op: ComponentScoped state is transient and not saved
        return Task.CompletedTask;
    }

    /// <summary>
    /// Asynchronously loads the state, always returning default as ComponentScoped state is not persisted.
    /// </summary>
    /// <typeparam name="T">The type of the state object.</typeparam>
    /// <param name="stateId">A unique identifier for the state (ignored in this implementation).</param>
    /// <param name="userContextId">A unique identifier for the user context (ignored in this implementation).</param>
    /// <returns>A task returning the default value of T, as no state is stored.</returns>
    /// <example>
    /// <code><![CDATA[
    /// IAppStateStoreProvider storage = new ComponentScopedStateStoreProvider();
    /// var restored = await storage.LoadAsync<CatalogViewModel>("catalog-list", "anonymous:preview");
    /// var current = restored ?? new CatalogViewModel();
    /// ]]></code>
    /// </example>
    public Task<T> LoadAsync<T>(string stateId, string userContextId)
    {
        // No-op: ComponentScoped state is not retained
        return Task.FromResult(default(T));
    }

    /// <summary>
    /// Executes the clear operation.
    /// </summary>
    /// <param name="stateId">The state id used by the operation.</param>
    /// <param name="userContextId">The user context id used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <example>
    /// <code><![CDATA[
    /// IAppStateStoreProvider storage = new ComponentScopedStateStoreProvider();
    /// await storage.ClearAsync("catalog-list", "anonymous:preview");
    /// ]]></code>
    /// </example>
    public Task ClearAsync(string stateId, string userContextId)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the clear all operation.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <example>
    /// <code><![CDATA[
    /// IAppStateStoreProvider storage = new ComponentScopedStateStoreProvider();
    /// await storage.ClearAllAsync(); // Explicit provider-local maintenance only.
    /// ]]></code>
    /// </example>
    public Task ClearAllAsync()
    {
        return Task.CompletedTask;
    }
}

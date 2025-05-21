namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// A state store provider that does not persist state, simulating ComponentScoped behavior where state is lost on navigation or refresh.
/// </summary>
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
    public Task SaveStateAsync<T>(string stateId, string userContextId, T state)
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
    public Task<T> LoadStateAsync<T>(string stateId, string userContextId)
    {
        // No-op: ComponentScoped state is not retained
        return Task.FromResult(default(T));
    }

    public Task ClearAsync(string stateId, string userContextId)
    {
        return Task.CompletedTask;
    }

    public Task ClearAllAsync()
    {
        return Task.CompletedTask;
    }
}
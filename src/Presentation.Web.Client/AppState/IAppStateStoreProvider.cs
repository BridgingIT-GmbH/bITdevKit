namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Defines the contract for state storage providers that persist and retrieve state data.
/// </summary>
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
    Task SaveStateAsync<T>(string stateId, string userContextId, T state);

    /// <summary>
    /// Loads the state from the underlying storage.
    /// </summary>
    /// <typeparam name="T">The type of the state object.</typeparam>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context.</param>
    /// <returns>A task that returns the loaded state, or default if not found.</returns>
    Task<T> LoadStateAsync<T>(string stateId, string userContextId);

    /// <summary>
    /// Asynchronously clears all state data for the given state and user context from the underlying storage.
    /// </summary>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context.</param>
    /// <returns>A task representing the asynchronous clear operation.</returns>
    Task ClearAsync(string stateId, string userContextId);

    /// <summary>
    /// Asynchronously clears all state data from the underlying storage.
    /// </summary>
    Task ClearAllAsync();
}
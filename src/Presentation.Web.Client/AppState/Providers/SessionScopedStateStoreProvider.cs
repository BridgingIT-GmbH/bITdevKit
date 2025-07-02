namespace BridgingIT.DevKit.Presentation.Web.Client;

using Microsoft.JSInterop;

/// <summary>
/// A state store provider that persists state in memory for the duration of a user session.
/// </summary>
/// <remarks>
/// Initializes a new instance of the SessionScopedStateStoreProvider.
/// </remarks>
/// <param name="debugger">The state debugger to determine if logging is enabled.</param>
/// <param name="jsRuntime">The JavaScript runtime for logging to the browser console.</param>
public class SessionScopedStateStoreProvider(AppStateDebugger debugger, IJSRuntime jsRuntime) : IAppStateStoreProvider
{
    private readonly Dictionary<string, object> stateStore = [];
    private readonly AppStateDebugger debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));

    /// <summary>
    /// Asynchronously saves the state in memory, scoped to the session.
    /// </summary>
    /// <typeparam name="T">The type of the state object.</typeparam>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context (ignored as session scope handles isolation).</param>
    /// <param name="state">The state object to save.</param>
    /// <returns>A completed task representing the save operation.</returns>
    public async Task SaveStateAsync<T>(string stateId, string userContextId, T state)
    {
        try
        {
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", $"State saving to session storage for {stateId}", state);
            }
            var key = $"{stateId}_{userContextId}";
            this.stateStore[key] = state;
        }
        catch (InvalidOperationException)
        {
            // ignore for now due to jsruntime not being available
        }
        catch (Exception ex)
        {
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to save state to session storage for {stateId}: {ex.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Asynchronously loads the state from memory, scoped to the session.
    /// </summary>
    /// <typeparam name="T">The type of the state object.</typeparam>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context (ignored as session scope handles isolation).</param>
    /// <returns>A task returning the loaded state, or default if not found.</returns>
    public async Task<T> LoadStateAsync<T>(string stateId, string userContextId)
    {
        try
        {
            var key = $"{stateId}_{userContextId}";
            if (this.stateStore.TryGetValue(key, out var state) && state is T typedState)
            {
                if (this.debugger.IsEnabled)
                {
                    await this.jsRuntime.InvokeVoidAsync("console.log", $"State loaded from session storage for {stateId}", typedState);
                }
                return typedState;
            }
            return default;
        }
        catch (Exception ex)
        {
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to load state from session storage for {stateId}: {ex.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Asynchronously clears the state from memory for the given state ID.
    /// </summary>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context (ignored as session scope handles isolation).</param>
    /// <returns>A task representing the asynchronous clear operation.</returns>
    public async Task ClearAsync(string stateId, string userContextId)
    {
        try
        {
            var key = $"{stateId}_{userContextId}";
            this.stateStore.Remove(key);
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", $"Cleared state from session storage for {stateId}");
            }
        }
        catch (InvalidOperationException)
        {
            // ignore for now due to jsruntime not being available
        }
        catch (Exception ex)
        {
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to clear state from session storage for {stateId}: {ex.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Asynchronously clears all state data from memory.
    /// </summary>
    /// <returns>A task representing the asynchronous clear operation.</returns>
    public async Task ClearAllAsync()
    {
        try
        {
            foreach (var key in this.stateStore.Keys.ToList())
            {
                this.stateStore.Remove(key);
                if (this.debugger.IsEnabled)
                {
                    await this.jsRuntime.InvokeVoidAsync("console.log", $"Cleared state from session storage for {key}");
                }
            }
            this.stateStore.Clear();
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", "Cleared all tracked states from session storage");
            }
        }
        catch (InvalidOperationException)
        {
            // ignore for now due to jsruntime not being available
        }
        catch (Exception ex)
        {
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to clear all state from session storage: {ex.Message}");
            }
            throw;
        }
    }
}
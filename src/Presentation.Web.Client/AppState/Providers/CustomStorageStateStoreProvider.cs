namespace BridgingIT.DevKit.Presentation.Web.Client;

using Microsoft.JSInterop;

/// <summary>
/// A sample custom state store provider that persists state in memory with user context, serving as a placeholder for server-side implementations.
/// </summary>
/// <remarks>
/// This can be extended to use a database, distributed cache, or other durable storage by replacing the in-memory dictionary.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the CustomStorageStateStoreProvider.
/// </remarks>
/// <param name="debugger">The state debugger to determine if logging is enabled.</param>
/// <param name="jsRuntime">The JavaScript runtime for logging to the browser console.</param>
public class CustomStorageStateStoreProvider(AppStateDebugger debugger, IJSRuntime jsRuntime) : IAppStateStoreProvider
{
    private readonly Dictionary<string, object> stateStore = [];
    private readonly AppStateDebugger debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));

    /// <summary>
    /// Asynchronously saves the state in memory, keyed by stateId and userContextId for isolation.
    /// </summary>
    /// <typeparam name="T">The type of the state object.</typeparam>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context.</param>
    /// <param name="state">The state object to save.</param>
    /// <returns>A completed task representing the save operation.</returns>
    public async Task SaveStateAsync<T>(string stateId, string userContextId, T state)
    {
        try
        {
            if (string.IsNullOrEmpty(userContextId))
            {
                throw new ArgumentNullException(nameof(userContextId), "User context ID is required for CustomStorage.");
            }

            var key = $"{stateId}_{userContextId}";
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", $"State saving to custom storage for {stateId}", state);
            }
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
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to save state to custom storage for {stateId}: {ex.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Asynchronously loads the state from memory, keyed by stateId and userContextId.
    /// </summary>
    /// <typeparam name="T">The type of the state object.</typeparam>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context.</param>
    /// <returns>A task returning the loaded state, or default if not found.</returns>
    public async Task<T> LoadStateAsync<T>(string stateId, string userContextId)
    {
        try
        {
            if (string.IsNullOrEmpty(userContextId))
            {
                throw new ArgumentNullException(nameof(userContextId), "User context ID is required for CustomStorage.");
            }

            var key = $"{stateId}_{userContextId}";
            if (this.stateStore.TryGetValue(key, out var state) && state is T typedState)
            {
                if (this.debugger.IsEnabled)
                {
                    await this.jsRuntime.InvokeVoidAsync("console.log", $"State loaded from custom storage for {stateId}", typedState);
                }
                return typedState;
            }
            return default;
        }
        catch (Exception ex)
        {
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to load state from custom storage for {stateId}: {ex.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Asynchronously clears the state from memory for the given state ID and user context.
    /// </summary>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context.</param>
    /// <returns>A task representing the asynchronous clear operation.</returns>
    public async Task ClearAsync(string stateId, string userContextId)
    {
        try
        {
            if (string.IsNullOrEmpty(userContextId))
            {
                throw new ArgumentNullException(nameof(userContextId), "User context ID is required for CustomStorage.");
            }

            var key = $"{stateId}_{userContextId}";
            this.stateStore.Remove(key);
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", $"Cleared state from custom storage for {stateId}");
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
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to clear state from custom storage for {stateId}: {ex.Message}");
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
                    // Extract stateId from key (format: userContextId_stateId)
                    var stateId = key.Substring(key.IndexOf('_') + 1);
                    await this.jsRuntime.InvokeVoidAsync("console.log", $"Cleared state from custom storage for {stateId}");
                }
            }
            this.stateStore.Clear();
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", "Cleared all tracked states from custom storage");
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
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to clear all state from custom storage: {ex.Message}");
            }
            throw;
        }
    }
}
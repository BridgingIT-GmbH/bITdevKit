namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Text.Json;
using Microsoft.JSInterop;

/// <summary>
/// A state store provider that persists state in the browser's localStorage, surviving page refreshes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the LocalStorageStateStoreProvider.
/// </remarks>
/// <param name="jsRuntime">The JavaScript runtime for interacting with localStorage.</param>
/// <param name="debugger">The state debugger to determine if logging is enabled.</param>
/// <exception cref="ArgumentNullException">Thrown if jsRuntime or debugger is null.</exception>
public class LocalStorageStateStoreProvider(IJSRuntime jsRuntime, AppStateDebugger debugger) : IAppStateStoreProvider
{
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly AppStateDebugger debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));
    private readonly HashSet<string> stateIds = [];

    /// <summary>
    /// Asynchronously saves the state to localStorage as JSON.
    /// </summary>
    /// <typeparam name="T">The type of the state object.</typeparam>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context (ignored as localStorage is client-specific).</param>
    /// <param name="state">The state object to save.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    public async Task SaveStateAsync<T>(string stateId, string userContextId, T state)
    {
        try
        {
            var key = $"{stateId}_{userContextId}";
            var json = JsonSerializer.Serialize(state);
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", $"State saving to localStorage for {stateId}", state);
            }
            await this.jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
            this.stateIds.Add(key); // Track the state ID
        }
        catch (InvalidOperationException)
        {
            // ignore for now due to jsruntime not being available
        }
        catch (Exception ex)
        {
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to save state to localStorage for {stateId}: {ex.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Asynchronously loads the state from localStorage, deserializing from JSON.
    /// </summary>
    /// <typeparam name="T">The type of the state object.</typeparam>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context (ignored as localStorage is client-specific).</param>
    /// <returns>A task returning the loaded state, or default if not found.</returns>
    public async Task<T> LoadStateAsync<T>(string stateId, string userContextId)
    {
        try
        {
            var key = $"{stateId}_{userContextId}";
            var json = await this.jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
            var data = json != null ? JsonSerializer.Deserialize<T>(json) : default;
            if (this.debugger.IsEnabled && json != null)
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", $"State loaded from localStorage for {stateId}", data);
            }
            if (json != null)
            {
                this.stateIds.Add(key); // Track the state ID if it exists
            }
            return data;
        }
        catch (Exception ex)
        {
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to load state from localStorage for {stateId}: {ex.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Asynchronously clears the state from localStorage for the given state ID.
    /// </summary>
    /// <param name="stateId">A unique identifier for the state.</param>
    /// <param name="userContextId">A unique identifier for the user context (ignored as localStorage is client-specific).</param>
    /// <returns>A task representing the asynchronous clear operation.</returns>
    public async Task ClearAsync(string stateId, string userContextId)
    {
        try
        {
            var key = $"{stateId}_{userContextId}";
            await this.jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", $"Cleared state from localStorage for {stateId}");
            }
            this.stateIds.Remove(key);
        }
        catch (InvalidOperationException)
        {
            // ignore for now due to jsruntime not being available
        }
        catch (Exception ex)
        {
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to clear state from localStorage for {stateId}: {ex.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Asynchronously clears all state data from localStorage that was saved by this provider.
    /// </summary>
    /// <returns>A task representing the asynchronous clear operation.</returns>
    public async Task ClearAllAsync()
    {
        try
        {
            foreach (var stateId in this.stateIds.ToList())
            {
                await this.jsRuntime.InvokeVoidAsync("localStorage.removeItem", stateId);
                if (this.debugger.IsEnabled)
                {
                    await this.jsRuntime.InvokeVoidAsync("console.log", $"Cleared state from localStorage for {stateId}");
                }
            }
            this.stateIds.Clear();
            if (this.debugger.IsEnabled)
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", "Cleared all tracked states from localStorage");
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
                await this.jsRuntime.InvokeVoidAsync("console.error", $"Failed to clear all state from localStorage: {ex.Message}");
            }
            throw;
        }
    }
}
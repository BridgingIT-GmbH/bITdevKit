namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Collections.Concurrent;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

/// <summary>
/// Manages all registered managed states and provides global operations like resetting all states.
/// </summary>
/// <remarks>
/// Initializes a new instance of the StateManager.
/// </remarks>
/// <param name="serviceProvider">The service provider to resolve dependencies.</param>
/// <param name="debugger">The state debugger to determine if logging is enabled.</param>
public class AppStateManager(IServiceProvider serviceProvider, AppStateDebugger debugger)
{
    private readonly ConcurrentDictionary<string, IAppState> states = [];
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly AppStateDebugger debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));

    /// <summary>
    /// Logs a message to the browser console if debugging is enabled.
    /// </summary>
    /// <param name="jsRuntime">The JavaScript runtime for logging.</param>
    /// <param name="message">The message to log.</param>
    private Task LogAsync(IJSRuntime jsRuntime, string message)
    {
        if (this.debugger.IsEnabled)
        {
            //await jsRuntime.InvokeVoidAsync("console.log", message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs an error to the browser console if debugging is enabled.
    /// </summary>
    /// <param name="jsRuntime">The JavaScript runtime for logging.</param>
    /// <param name="message">The error message to log.</param>
    private Task LogErrorAsync(IJSRuntime jsRuntime, string message)
    {
        if (this.debugger.IsEnabled)
        {
            //await jsRuntime.InvokeVoidAsync("console.error", message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers a managed state with the manager.
    /// </summary>
    /// <param name="state">The managed state to register.</param>
    public void RegisterState(IAppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        this.states[state.StateId] = state;
        // Removed logging to avoid JavaScript interop during static rendering
    }

    /// <summary>
    /// Asynchronously resets all registered states and clears their storage.
    /// </summary>
    /// <returns>A task representing the asynchronous reset operation.</returns>
    public async Task ResetAllAsync()
    {
        using var scope = this.serviceProvider.CreateScope();
        var jsRuntime = scope.ServiceProvider.GetRequiredService<IJSRuntime>();
        var storageProviders = scope.ServiceProvider.GetServices<IAppStateStoreProvider>();
        var userContextProvider = scope.ServiceProvider.GetService<IUserContextProvider>();

        try
        {
            await this.LogAsync(jsRuntime, "State reset for all states (Global)");

            // Clear all storage providers first to ensure persisted states are removed
            foreach (var storageProvider in storageProviders.SafeNull())
            {
                if (storageProvider != null)
                {
                    try
                    {
                        await this.LogAsync(jsRuntime, $"Clearing storage for provider {storageProvider.GetType().Name}");
                        await storageProvider.ClearAllAsync();
                    }
                    catch (Exception ex)
                    {
                        await this.LogErrorAsync(jsRuntime, $"Failed to clear storage for provider {storageProvider.GetType().Name}: {ex.Message}");
                    }
                }
            }

            // Clear the user context
            if (userContextProvider != null)
            {
                try
                {
                    await this.LogAsync(jsRuntime, "Clearing user context");
                    await userContextProvider.ClearAsync();
                }
                catch (Exception ex)
                {
                    await this.LogErrorAsync(jsRuntime, $"Failed to clear user context: {ex.Message}");
                }
            }

            // Reset all registered states
            foreach (var state in this.states.Values)
            {
                try
                {
                    await this.LogAsync(jsRuntime, $"State reset for {state.StateId}");
                    await state.ResetAsync();
                }
                catch (Exception ex)
                {
                    await this.LogErrorAsync(jsRuntime, $"Failed to reset state {state.StateId}: {ex.Message}");
                }
            }

            // Clear the in-memory states to ensure no stale data remains
            this.states.Clear();

            await this.LogAsync(jsRuntime, "State reset completed (Global)");
        }
        catch (Exception ex)
        {
            await this.LogErrorAsync(jsRuntime, $"Global reset failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets all registered states.
    /// </summary>
    /// <returns>A read-only list of registered states.</returns>
    public IReadOnlyList<IAppState> GetAllStates()
    {
        return this.states.Values.ToList().AsReadOnly();
    }
}
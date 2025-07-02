namespace BridgingIT.DevKit.Presentation.Web.Client;

using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>
/// Provides debugging capabilities for the managed state system, logging changes to the browser console and in-memory storage.
/// </summary>
/// <remarks>
/// Initializes a new instance of the StateDebugger class.
/// </remarks>
/// <param name="logger">The logger instance for writing debug messages.</param>
/// <param name="jsRuntime">The JavaScript runtime for browser console logging.</param>
/// <param name="options">The debugging configuration options.</param>
public class AppStateDebugger(ILogger<AppStateDebugger> logger, IJSRuntime jsRuntime, AppStateDebugOptions options)
{
    private readonly ILogger<AppStateDebugger> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly AppStateDebugOptions options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly List<IStateChangeMetadata> stateChanges = [];

    /// <summary>
    /// Gets a value indicating whether debugging is enabled based on the configuration options.
    /// </summary>
    public bool IsEnabled => this.options.LoggingEnabled || this.options.StateChangesTracked;

    /// <summary>
    /// Tracks a state change, logging it to the console and storing it in memory if configured.
    /// </summary>
    /// <param name="change">The metadata describing the state change.</param>
    public async void TrackStateChange(IStateChangeMetadata change) // INFO: needs to be void otherwhise blocks UI
    {
        if (!this.IsEnabled)
        {
            return;
        }

        if (this.options.LoggingEnabled)
        {
            var message = $"State changed {change.StateId}: {change.Reason}";
            //this.logger.LogInformation(message);
            //await this.ConsoleLogAsync(message);
            try
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", message, change);
            }
            catch (JSDisconnectedException)
            {
                // do nothing, jsruntime not available
            }
            catch (InvalidOperationException)
            {
                // do nothing, jsruntime not available
            }
        }
        if (this.options.StateChangesTracked)
        {
            this.stateChanges.Add(change);
        }
    }

    public async Task ConsoleLogAsync(params object[] args)
    {
        if (this.options.LoggingEnabled)
        {
            try
            {
                await this.jsRuntime.InvokeVoidAsync("console.log", args);
            }
            catch (JSDisconnectedException)
            {
                // do nothing, jsruntime not available
            }
            catch (InvalidOperationException)
            {
                // do nothing, jsruntime not available
            }
        }
    }

    public async Task ConsoleInfoAsync(params object[] args)
    {
        if (this.options.LoggingEnabled)
        {
            try
            {
                await this.jsRuntime.InvokeVoidAsync("console.info", args);
            }
            catch (JSDisconnectedException)
            {
                // do nothing, jsruntime not available
            }
            catch (InvalidOperationException)
            {
                // do nothing, jsruntime not available
            }
        }
    }

    public async Task ConsoleWarnAsync(params object[] args)
    {
        if (this.options.LoggingEnabled)
        {
            try
            {
                await this.jsRuntime.InvokeVoidAsync("console.warn", args);
            }
            catch (JSDisconnectedException)
            {
                // do nothing, jsruntime not available
            }
            catch (InvalidOperationException)
            {
                // do nothing, jsruntime not available
            }
        }
    }

    public async Task ConsoleErrorAsync(params object[] args)
    {
        try
        {
            await this.jsRuntime.InvokeVoidAsync("console.error", args);
        }
        catch (JSDisconnectedException)
        {
            // do nothing, jsruntime not available
        }
        catch (InvalidOperationException)
        {
            // do nothing, jsruntime not available
        }
    }

    /// <summary>
    /// Retrieves the list of tracked state changes.
    /// </summary>
    /// <returns>A read-only list of state change metadata.</returns>
    public IReadOnlyList<IStateChangeMetadata> GetStateChanges() => this.stateChanges.AsReadOnly();
}
namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Text.Json;
using BridgingIT.DevKit.Common.Utilities;
using Microsoft.Extensions.Logging;

/// <summary>
/// Abstract base class for managing state with persistence, history, and change notification.
/// </summary>
/// <typeparam name="TState">The type of the state object managed by this instance.</typeparam>
public abstract class AppState<TState> : IAppState, IDisposable
    where TState : class, new()
{
    private readonly ILogger logger;
    private readonly AppStateOptions options;
    private readonly IAppStateStoreProvider storageProvider;
    private readonly IUserContextProvider userContextProvider;
    private readonly AppStateDebugger debugger;
    private readonly Stack<TState> undoStack;
    private readonly Stack<TState> redoStack;
    private readonly Debouncer saveDebouncer;
    private bool isLoaded;

    /// <summary>
    /// Initializes a new instance of the ManagedState class.
    /// </summary>
    /// <param name="logger">The logger instance for debugging.</param>
    /// <param name="options">The configuration options for this state.</param>
    /// <param name="storageProvider">The optional storage provider for persistence.</param>
    /// <param name="userContextProvider">The provider for user context IDs.</param>
    /// <param name="debugger">The debugger for tracking state changes.</param>
    protected AppState(
        ILogger logger,
        AppStateOptions options,
        IAppStateStoreProvider storageProvider = null,
        IUserContextProvider userContextProvider = null,
        AppStateDebugger debugger = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.storageProvider = storageProvider;
        this.userContextProvider = userContextProvider;
        this.debugger = debugger;
        this.undoStack = options.HistoryEnabled ? new Stack<TState>(options.MaxHistoryItems) : [];
        this.redoStack = options.HistoryEnabled ? new Stack<TState>(options.MaxHistoryItems) : [];
        this.saveDebouncer = new Debouncer(options.DebounceDelay, this.SaveStateAsyncInternal);

        if (this.debugger?.IsEnabled == true)
        {
            //this.debugger.ConsoleLogAsync($"State initialized for {this.StateId}", options).Wait();
        }
    }

    /// <summary>
    /// Gets the unique identifier for this state instance, to be implemented by derived classes.
    /// </summary>
    public virtual string StateId { get; } = typeof(TState).FullName.Replace(".", "_").ToLower();

    /// <summary>
    /// Gets the current state object.
    /// </summary>
    public TState CurrentState => this.GetCurrentState();

    object IAppState.CurrentState => this.CurrentState;

    /// <summary>
    /// Gets a value indicating whether an undo operation is possible.
    /// </summary>
    public bool CanUndo => this.undoStack.Count > 0;

    /// <summary>
    /// Gets a value indicating whether a redo operation is possible.
    /// </summary>
    public bool CanRedo => this.redoStack.Count > 0;

    /// <summary>
    /// Gets the history of undoable states.
    /// </summary>
    public IReadOnlyList<TState> UndoHistory => this.undoStack.ToList().AsReadOnly();

    /// <summary>
    /// Gets the history of redoable states.
    /// </summary>
    public IReadOnlyList<TState> RedoHistory => this.redoStack.ToList().AsReadOnly();

    /// <summary>
    /// Event raised when the state changes, providing the new state object.
    /// </summary>
    public event Action<object> StateChanged;

    /// <summary>
    /// Event raised when the state changes, providing detailed metadata about the change.
    /// </summary>
    public event Action<IStateChangeMetadata> StateChangeWithMetadata;

    /// <summary>
    /// Retrieves the current state object, to be implemented by derived classes.
    /// </summary>
    /// <returns>The current state object.</returns>
    protected abstract TState GetCurrentState();

    /// <summary>
    /// Updates the state object, to be implemented by derived classes.
    /// </summary>
    /// <param name="state">The new state object.</param>
    protected abstract void UpdateState(TState state);

    /// <summary>
    /// Creates a default state instance, to be implemented by derived classes.
    /// </summary>
    /// <returns>A new instance of the default state.</returns>
    protected abstract TState CreateDefaultState();

    /// <summary>
    /// Takes a snapshot of the current state for manipulation or history.
    /// </summary>
    /// <returns>A deep copy of the current state using JSON serialization.</returns>
    public TState TakeSnapshot()
    {
        var current = this.GetCurrentState();
        if (current == null)
        {
            //this.logger.LogWarning("Current state is null for {StateId}", this.StateId);
            return default;
        }

        if (!this.options.Enabled)
        {
            return current;
        }

        // Create a deep copy using JSON serialization
        var json = JsonSerializer.Serialize(current);
        var copy = JsonSerializer.Deserialize<TState>(json);

        if (this.debugger?.IsEnabled == true)
        {
            //this.debugger.ConsoleLogAsync($"State snapshot created for {this.StateId}", copy).Wait();
        }

        return copy;
    }

    /// <summary>
    /// Restores the state to a previous snapshot, triggering change events and a debounced save.
    /// </summary>
    /// <param name="snapshot">The snapshot to restore.</param>
    /// <param name="reason">The reason for the restoration.</param>
    public async Task RestoreSnapshot(TState snapshot, string reason = null)
    {
        if (snapshot == null || !this.options.Enabled)
        {
            return;
        }

        await this.SetCurrentState(snapshot, reason);
    }

    /// <summary>
    /// Asynchronously saves the current state to the configured storage provider.
    /// </summary>
    /// <remarks>
    /// This method is a no-op as saving is handled via debouncing in SetCurrentState.
    /// </remarks>
    public Task SaveStateAsync()
    {
        // No-op: Saving is handled via debouncing in SetCurrentState
        //this.logger.LogInformation("SaveStateAsync called for {StateId}, but saving is handled via debouncing in SetCurrentState", this.StateId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Asynchronously loads the state from the configured storage provider.
    /// </summary>
    public virtual async Task LoadStateAsync()
    {
        if (this.options.PersistenceType == AppStatePersistenceType.ComponentScoped || this.options.PersistenceType == AppStatePersistenceType.SessionScoped)
        {
            //this.logger.LogInformation("Skipping load for {StateId} due to persistence level: {PersistenceLevel}", this.StateId, this.options.PersistenceLevel);
            return;
        }

        if (this.storageProvider == null)
        {
            //this.logger.LogWarning("LoadState called for {StateId} but no storage provider is registered for persistence level {Level}", this.StateId, this.options.PersistenceLevel);
            return;
        }

#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception
        try
        {
            if (this.options.Enabled)
            {
                var userContextId = /*this.options.PersistenceLevel == StatePersistenceLevel.LocalStorage &&*/ this.userContextProvider != null
                    ? await this.userContextProvider.GetUserContextId()
                    : null;
                var state = await this.storageProvider.LoadStateAsync<TState>(this.StateId, userContextId);
                if (state != null)
                {
                    await this.SetCurrentState(state, "Loaded from storage");
                }
            }
            this.isLoaded = true;
            //this.logger.LogInformation("Successfully loaded state for {StateId}", this.StateId);
        }
        catch (Exception/* ex*/) // Catch all exceptions to prevent crashing the application when f5 is used as we then load too early from localstorage
        {
            //this.logger.LogError(ex, "Failed to load state for {StateId}", this.StateId);
        }
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception
    }

    /// <summary>
    /// Resets the state to its initial value and clears the associated storage.
    /// </summary>
    /// <returns>A task representing the asynchronous reset operation.</returns>
    public virtual async Task ResetAsync()
    {
        // Create a new default state
        var defaultState = this.CreateDefaultState();
        if (defaultState == null)
        {
            //this.logger.LogWarning("Default state is null for {StateId}", this.StateId);
            return;
        }

        // Reset the state
        await this.SetCurrentState(defaultState, "State reset"); // needed for undo/redo

        // Clear history
        this.undoStack.Clear();
        this.redoStack.Clear();

        // Clear the storage
        if (this.storageProvider != null)
        {
            try
            {
                //this.debugger?.ConsoleLogAsync($"State reset for {this.StateId}", defaultState).Wait();
                var userContextId = this.userContextProvider != null
                    ? await this.userContextProvider.GetUserContextId()
                    : null;
                await this.storageProvider.ClearAsync(this.StateId, userContextId);
                //await this.storageProvider.SaveStateAsync(this.StateId, userContextId, defaultState);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to clear storage for {StateId}", this.StateId);
            }
        }

        // Reset the loaded flag
        //this.isLoaded = false;
    }

    /// <summary>
    /// Performs an undo operation, reverting to the previous state in the history.
    /// </summary>
    public async Task UndoAsync()
    {
        if (!this.options.HistoryEnabled || !this.CanUndo || !this.options.Enabled)
        {
            //this.logger.LogInformation("Undo skipped for {StateId}. HistoryEnabled: {HistoryEnabled}, CanUndo: {CanUndo}", this.StateId, this.options.HistoryEnabled, this.CanUndo);
            return;
        }

        var current = this.GetCurrentState();
        if (this.debugger?.IsEnabled == true)
        {
            //this.logger.LogInformation("State changed for {StateId}. Old: {OldState}, New: {NewState}", this.StateId, JsonSerializer.Serialize(oldState), JsonSerializer.Serialize(newState));
            //this.debugger.ConsoleLogAsync($"State undo for {this.StateId}", current).Wait();
        }
        this.redoStack.Push(current);
        if (this.redoStack.Count > this.options.MaxHistoryItems)
        {
            this.redoStack.TryPop(out _); // Remove oldest if exceeding limit
        }

        var previous = this.undoStack.Pop();
        await this.SetCurrentState(previous, "Undo operation");
    }

    /// <summary>
    /// Performs a redo operation, advancing to the next state in the history.
    /// </summary>
    public async Task RedoAsync()
    {
        if (!this.options.HistoryEnabled || !this.CanRedo || !this.options.Enabled)
        {
            //this.logger.LogInformation("Redo skipped for {StateId}. HistoryEnabled: {HistoryEnabled}, CanRedo: {CanRedo}", this.StateId, this.options.HistoryEnabled, this.CanRedo);
            return;
        }

        var current = this.GetCurrentState();
        if (this.debugger?.IsEnabled == true)
        {
            //this.logger.LogInformation("State changed for {StateId}. Old: {OldState}, New: {NewState}", this.StateId, JsonSerializer.Serialize(oldState), JsonSerializer.Serialize(newState));
            //this.debugger.ConsoleLogAsync($"State redo for {this.StateId}", current).Wait();
        }
        this.undoStack.Push(current);
        if (this.undoStack.Count > this.options.MaxHistoryItems)
        {
            this.undoStack.TryPop(out _); // Remove oldest if exceeding limit
        }

        var next = this.redoStack.Pop();
        await this.SetCurrentState(next, "Redo operation");
    }

    /// <summary>
    /// Sets the current state, updating history, raising change events, and triggering a debounced save.
    /// </summary>
    /// <param name="newState">The new state to set.</param>
    /// <param name="reason">The reason for the state change.</param>
    protected virtual async Task SetCurrentState(TState newState, string reason = null)
    {
        var oldState = this.GetCurrentState();
        //this.logger.LogInformation("SetCurrentState called for {StateId} with reason: {Reason}", this.StateId, reason);
        if (!Equals(oldState, newState))
        {
            if (this.debugger?.IsEnabled == true)
            {
                //this.logger.LogInformation("State changed for {StateId}. Old: {OldState}, New: {NewState}", this.StateId, JsonSerializer.Serialize(oldState), JsonSerializer.Serialize(newState));
                //this.debugger.ConsoleLogAsync($"State changed for {this.StateId} with reason: {reason}", oldState, newState).Wait();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                this.debugger.ConsoleLogAsync($"State changed for {this.StateId} with reason: {reason}", oldState, newState);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }

            if (this.options.HistoryEnabled)
            {
                this.undoStack.Push(oldState);
                if (this.undoStack.Count > this.options.MaxHistoryItems)
                {
                    this.undoStack.TryPop(out _); // Remove oldest if exceeding limit
                }

                this.redoStack.Clear(); // Clear redo stack on new change
            }

            this.UpdateState(newState);

            var metadata = new StateChangeMetadata<TState>
            {
                StateId = this.StateId,
                Timestamp = DateTimeOffset.UtcNow,
                Operation = "Update",
                OldValue = oldState,
                NewValue = newState,
                Reason = reason
            };

            this.StateChanged?.Invoke(newState);
            this.StateChangeWithMetadata?.Invoke(metadata);

            this.debugger.TrackStateChange(metadata);

            // Trigger a debounced save if persistence is enabled
            if (this.options.PersistenceType != AppStatePersistenceType.ComponentScoped && this.options.PersistenceType != AppStatePersistenceType.SessionScoped)
            {
                //this.logger.LogInformation("Triggering debounced save for {StateId}", this.StateId);
                //Task.Run(() => this.SaveStateAsyncInternal());

                await this.saveDebouncer.DebounceAsync(CancellationToken.None);
            }
            else
            {
                //this.logger.LogInformation("Skipping save for {StateId} due to persistence level: {PersistenceLevel}", this.StateId, this.options.PersistenceLevel);
            }
        }
        else
        {
            //this.logger.LogInformation("State unchanged for {StateId}. Skipping update.", this.StateId);
        }
    }

    /// <summary>
    /// Asynchronously saves the current state to the configured storage provider.
    /// </summary>
    /// <remarks>
    /// This method is internal and called via debouncing in SetCurrentState.
    /// </remarks>
    private async Task SaveStateAsyncInternal()
    {
        if (!this.isLoaded)
        {
            //this.logger.LogInformation("Skipping save for {StateId} because state has not been loaded yet", this.StateId);
            return;
        }

        if (this.options.PersistenceType == AppStatePersistenceType.ComponentScoped || this.options.PersistenceType == AppStatePersistenceType.SessionScoped)
        {
            //this.logger.LogInformation("Skipping save for {StateId} due to persistence level: {PersistenceLevel}", this.StateId, this.options.PersistenceLevel);
            return;
        }

        if (this.storageProvider == null || !this.options.Enabled)
        {
            //this.logger.LogWarning("SaveState called for {StateId} but no storage provider is registered for persistence level {Level}", this.StateId, this.options.PersistenceLevel);
            return;
        }

        try
        {
            var userContextId = /*this.options.PersistenceLevel == StatePersistenceLevel.LocalStorage &&*/ this.userContextProvider != null
                ? await this.userContextProvider.GetUserContextId()
                : null;
            await this.storageProvider.SaveStateAsync(this.StateId, userContextId, this.GetCurrentState());
            //this.logger.LogInformation("Successfully saved state for {StateId}", this.StateId);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to save state for {StateId}", this.StateId);
        }
    }

    /// <summary>
    /// Disposes of the ManagedState instance, cleaning up resources.
    /// </summary>
    public void Dispose()
    {
        this.saveDebouncer?.Dispose();
    }
}
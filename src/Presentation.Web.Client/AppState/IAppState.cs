namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Defines the contract for a managed state instance, providing state management functionality.
/// </summary>
public interface IAppState
{
    /// <summary>
    /// Gets the unique identifier for this state instance.
    /// </summary>
    string StateId { get; }

    /// <summary>
    /// Gets the current state object.
    /// </summary>
    object CurrentState { get; }

    /// <summary>
    /// Gets a value indicating whether an undo operation is possible.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Gets a value indicating whether a redo operation is possible.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Asynchronously saves the current state to the configured storage provider.
    /// </summary>
    /// <returns>A task representing the asynchronous save operation.</returns>
    Task SaveStateAsync();

    /// <summary>
    /// Asynchronously loads the state from the configured storage provider.
    /// </summary>
    /// <returns>A task representing the asynchronous load operation.</returns>
    Task LoadStateAsync();

    /// <summary>
    /// Performs an undo operation, reverting to the previous state in the history.
    /// </summary>
    Task UndoAsync();

    /// <summary>
    /// Performs a redo operation, advancing to the next state in the history.
    /// </summary>
    Task RedoAsync();

    /// <summary>
    /// Resets the state to its initial value and clears the associated storage.
    /// </summary>
    Task ResetAsync();

    /// <summary>
    /// Event raised when the state changes, providing the new state object.
    /// </summary>
    event Action<object> StateChanged;

    /// <summary>
    /// Event raised when the state changes, providing detailed metadata about the change.
    /// </summary>
    event Action<IStateChangeMetadata> StateChangeWithMetadata;
}

/// <summary>
/// Provides metadata about a state change for debugging and notification purposes.
/// </summary>
public interface IStateChangeMetadata
{
    /// <summary>
    /// Gets the unique identifier of the state that changed.
    /// </summary>
    string StateId { get; }

    /// <summary>
    /// Gets the timestamp when the state change occurred.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the operation that caused the state change (e.g., "Update").
    /// </summary>
    string Operation { get; }

    /// <summary>
    /// Gets the previous state value before the change.
    /// </summary>
    object OldValue { get; }

    /// <summary>
    /// Gets the new state value after the change.
    /// </summary>
    object NewValue { get; }

    /// <summary>
    /// Gets the reason for the state change, providing context.
    /// </summary>
    string Reason { get; }
}

/// <summary>
/// Represents metadata about a state change, implementing IStateChangeMetadata with generic type safety.
/// </summary>
/// <typeparam name="T">The type of the state object.</typeparam>
public record StateChangeMetadata<T> : IStateChangeMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier of the state that changed.
    /// </summary>
    public string StateId { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the state change occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the operation that caused the state change.
    /// </summary>
    public string Operation { get; init; }

    /// <summary>
    /// Gets or sets the previous state value before the change.
    /// </summary>
    public T OldValue { get; init; }

    /// <summary>
    /// Gets or sets the new state value after the change.
    /// </summary>
    public T NewValue { get; init; }

    /// <summary>
    /// Gets or sets the reason for the state change.
    /// </summary>
    public string Reason { get; init; }

    // Explicit interface implementations for IStateChangeMetadata
    object IStateChangeMetadata.OldValue => this.OldValue;

    object IStateChangeMetadata.NewValue => this.NewValue;
}

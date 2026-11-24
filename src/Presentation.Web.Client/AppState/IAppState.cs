// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Defines the contract for a managed state instance, providing state management functionality.
/// </summary>
/// <example>
/// <code><![CDATA[
/// IAppState managed = state;
/// await managed.Load();
/// await managed.Save();
/// ]]></code>
/// </example>
public interface IAppState
{
    /// <summary>
    /// Gets the logical state identifier, shared by instances of the same state definition.
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
    /// <example>
    /// <code><![CDATA[
    /// IAppState managed = state;
    /// await managed.Load();
    /// await managed.Save();
    /// ]]></code>
    /// </example>
    Task Save();

    /// <summary>Saves state while allowing cancellation of this caller's wait.</summary>
    /// <param name="cancellationToken">Cancels admission or waiting; old providers can continue I/O.</param>
    /// <returns>The save operation.</returns>
    /// <example>
    /// <code><![CDATA[
    /// IAppState managed = state;
    /// await managed.Load();
    /// await managed.Save(CancellationToken.None);
    /// ]]></code>
    /// </example>
    async Task Save(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await this.Save().WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously loads the state from the configured storage provider.
    /// </summary>
    /// <returns>A task representing the asynchronous load operation.</returns>
    /// <example>
    /// <code><![CDATA[
    /// IAppState managed = state;
    /// await managed.Load();
    /// ]]></code>
    /// </example>
    Task Load();

    /// <summary>Waits for the owner-local load without cancelling other callers.</summary>
    /// <param name="cancellationToken">Cancels this caller's wait.</param>
    /// <returns>The shared load attempt.</returns>
    /// <example>
    /// <code><![CDATA[
    /// IAppState managed = state;
    /// await managed.Load(CancellationToken.None);
    /// ]]></code>
    /// </example>
    async Task Load(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await this.Load().WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Performs an undo operation, reverting to the previous state in the history.
    /// </summary>
    /// <example>
    /// <code><![CDATA[
    /// IAppState managed = state;
    /// var next = state.TakeSnapshot();
    /// next.Page = 2;
    /// await state.RestoreSnapshot(next);
    /// await managed.Undo();
    /// ]]></code>
    /// </example>
    Task Undo();

    /// <summary>
    /// Performs a redo operation, advancing to the next state in the history.
    /// </summary>
    /// <example>
    /// <code><![CDATA[
    /// IAppState managed = state;
    /// var next = state.TakeSnapshot();
    /// next.Page = 2;
    /// await state.RestoreSnapshot(next);
    /// await managed.Undo();
    /// await managed.Redo();
    /// ]]></code>
    /// </example>
    Task Redo();

    /// <summary>
    /// Resets the state to its initial value and clears the associated storage.
    /// </summary>
    /// <example>
    /// <code><![CDATA[
    /// IAppState managed = state;
    /// await managed.Reset();
    /// ]]></code>
    /// </example>
    Task Reset();

    /// <summary>Waits for reset without reversing an accepted privacy invalidation.</summary>
    /// <param name="cancellationToken">Cancels this caller's wait.</param>
    /// <returns>The reset operation.</returns>
    /// <example>
    /// <code><![CDATA[
    /// IAppState managed = state;
    /// await managed.Reset(CancellationToken.None);
    /// ]]></code>
    /// </example>
    async Task Reset(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await this.Reset().WaitAsync(cancellationToken);
    }

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
/// <example>
/// <code><![CDATA[
/// IStateChangeMetadata change = metadata;
/// Console.WriteLine($"{change.Operation}: revision {change.Version.Revision}");
/// ]]></code>
/// </example>
public interface IStateChangeMetadata
{
    /// <summary>
    /// Gets the unique identifier of the state that changed.
    /// </summary>
    string StateId { get; }

    /// <summary>Gets the originating revision when supplied by a managed state.</summary>
    AppStateVersion Version => default;

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
/// <example>
/// <code><![CDATA[
/// var change = new StateChangeMetadata<CatalogViewModel>
/// {
///     StateId = state.StateId, Version = state.Version, Operation = "Update",
///     Timestamp = TimeProvider.System.GetUtcNow(),
///     OldValue = state.TakeSnapshot(), NewValue = state.TakeSnapshot()
/// };
/// debugger.TrackStateChange(change);
/// ]]></code>
/// </example>
public record StateChangeMetadata<T> : IStateChangeMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier of the state that changed.
    /// </summary>
    public string StateId { get; init; }

    /// <summary>Gets the accepted state and context revision.</summary>
    public AppStateVersion Version { get; init; }

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

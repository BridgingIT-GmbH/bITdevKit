// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Tracks the readiness or fault state of one or more named databases.
/// </summary>
/// <remarks>
/// Consumers may treat this contract as optional. When it is not registered, features that support
/// database-readiness coordination should continue without waiting.
/// </remarks>
/// <example>
/// <code>
/// if (databaseReadyService is not null)
/// {
///     await databaseReadyService.WaitForReadyAsync("AppDbContext", cancellationToken: cancellationToken);
/// }
/// </code>
/// </example>
public interface IDatabaseReadyService
{
    /// <summary>Returns whether the named database is ready.</summary>
    /// <param name="name">
    /// The database name, or <see langword="null"/> to require every tracked database to be ready.
    /// </param>
    /// <returns><see langword="true"/> when the requested readiness condition is satisfied.</returns>
    /// <example><code>var ready = service.IsReady("AppDbContext");</code></example>
    bool IsReady(string name = null);

    /// <summary>Returns whether the named database is faulted.</summary>
    /// <param name="name">
    /// The database name, or <see langword="null"/> to inspect every tracked database.
    /// </param>
    /// <returns><see langword="true"/> when a requested database is faulted.</returns>
    /// <example><code>var faulted = service.IsFaulted("AppDbContext");</code></example>
    bool IsFaulted(string name = null);

    /// <summary>Gets the fault message for the named database.</summary>
    /// <param name="name">
    /// The database name, or <see langword="null"/> to return the first tracked fault.
    /// </param>
    /// <returns>The fault message, or <see langword="null"/> when no fault is recorded.</returns>
    /// <example><code>var message = service.FaultMessage("AppDbContext");</code></example>
    string FaultMessage(string name = null);

    /// <summary>Marks the named database as ready.</summary>
    /// <param name="name">The database name, or <see langword="null"/> for the default entry.</param>
    /// <example><code>service.SetReady("AppDbContext");</code></example>
    void SetReady(string name = null);

    /// <summary>Marks the named database as faulted.</summary>
    /// <param name="name">The database name, or <see langword="null"/> for the default entry.</param>
    /// <param name="message">An optional diagnostic message.</param>
    /// <example><code>service.SetFaulted("AppDbContext", exception.Message);</code></example>
    void SetFaulted(string name = null, string message = null);

    /// <summary>Waits asynchronously for the named database readiness condition.</summary>
    /// <param name="name">
    /// The database name, or <see langword="null"/> to wait for every tracked database.
    /// </param>
    /// <param name="pollInterval">The polling interval, defaulting to 200 milliseconds.</param>
    /// <param name="timeout">The maximum wait, defaulting to 30 seconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when a requested database is faulted.</exception>
    /// <exception cref="TimeoutException">Thrown when readiness is not reached before the timeout.</exception>
    /// <example>
    /// <code>
    /// await service.WaitForReadyAsync(
    ///     "AppDbContext",
    ///     timeout: TimeSpan.FromMinutes(2),
    ///     cancellationToken: cancellationToken);
    /// </code>
    /// </example>
    Task WaitForReadyAsync(
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>Invokes an asynchronous callback after the named database becomes ready or faulted.</summary>
    /// <typeparam name="TResult">The callback result type.</typeparam>
    /// <param name="onReady">The callback invoked when the database is ready.</param>
    /// <param name="onFaulted">The optional callback invoked when the database is faulted.</param>
    /// <param name="name">The database name, or <see langword="null"/> for the default entry.</param>
    /// <param name="pollInterval">The polling interval.</param>
    /// <param name="timeout">The maximum wait.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result returned by the selected callback.</returns>
    /// <example><code>var value = await service.OnReadyAsync(LoadAsync, name: "AppDbContext");</code></example>
    Task<TResult> OnReadyAsync<TResult>(
        Func<Task<TResult>> onReady,
        Func<Task<TResult>> onFaulted = null,
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>Invokes a synchronous callback after the named database becomes ready or faulted.</summary>
    /// <typeparam name="TResult">The callback result type.</typeparam>
    /// <param name="onReady">The callback invoked when the database is ready.</param>
    /// <param name="onFaulted">The optional callback invoked when the database is faulted.</param>
    /// <param name="name">The database name, or <see langword="null"/> for the default entry.</param>
    /// <param name="pollInterval">The polling interval.</param>
    /// <param name="timeout">The maximum wait.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result returned by the selected callback.</returns>
    /// <example><code>var value = await service.OnReadyAsync(Load, name: "AppDbContext");</code></example>
    Task<TResult> OnReadyAsync<TResult>(
        Func<TResult> onReady,
        Func<TResult> onFaulted = null,
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>Invokes an action after the named database becomes ready or faulted.</summary>
    /// <param name="onReady">The action invoked when the database is ready.</param>
    /// <param name="onFaulted">The optional action invoked when the database is faulted.</param>
    /// <param name="name">The database name, or <see langword="null"/> for the default entry.</param>
    /// <param name="pollInterval">The polling interval.</param>
    /// <param name="timeout">The maximum wait.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the wait and callback invocation.</returns>
    /// <example><code>await service.OnReadyAsync(StartWorker, name: "AppDbContext");</code></example>
    Task OnReadyAsync(
        Action onReady,
        Action onFaulted = null,
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
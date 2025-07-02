// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides a thread-safe service for tracking the readiness state of one or more named databases.
/// Allows modules to signal when a database is ready or faulted,
/// and consumers to wait asynchronously for readiness or be notified of failure.
/// </summary>
public interface IDatabaseReadyService
{
    /// <summary>
    /// Returns true if the database with the specified name is ready.
    /// Uses the default database entry if name is not provided or empty.
    /// </summary>
    /// <param name="name">The database name (optional, defaults to the default entry if empty).</param>
    bool IsReady(string name = null);

    /// <summary>
    /// Returns true if the database with the specified name is faulted.
    /// Uses the default database entry if name is not provided or empty.
    /// </summary>
    /// <param name="name">The database name (optional, defaults to the default entry if empty).</param>
    bool IsFaulted(string name = null);

    /// <summary>
    /// Gets the fault message if the database with the specified name is faulted.
    /// Uses the default database entry if name is not provided or empty.
    /// </summary>
    /// <param name="name">The database name (optional, defaults to the default entry if empty).</param>
    /// <returns>The fault message, or null if none is set.</returns>
    string FaultMessage(string name = null);

    /// <summary>
    /// Marks the database with the specified name as ready.
    /// Uses the default database entry if name is not provided or empty.
    /// </summary>
    /// <param name="name">The database name (optional, defaults to the default entry if empty).</param>
    void SetReady(string name = null);

    /// <summary>
    /// Marks the database with the specified name as faulted, with an optional message.
    /// Uses the default database entry if name is not provided or empty.
    /// </summary>
    /// <param name="name">The database name (optional, defaults to the default entry if empty).</param>
    /// <param name="message">An optional fault message.</param>
    void SetFaulted(string name = null, string message = null);

    /// <summary>
    /// Asynchronously waits for the database with the specified name to become ready,
    /// polling periodically until ready, faulted, or the timeout expires.
    /// Throws if the database is faulted or the timeout is exceeded.
    /// </summary>
    /// <param name="name">The database name (optional, defaults to the default entry if empty).</param>
    /// <param name="pollInterval">The polling interval (optional, defaults to 200ms).</param>
    /// <param name="timeout">The maximum time to wait (optional, defaults to 10s).</param>
    /// <param name="cancellationToken">The cancellation token (optional).</param>
    /// <exception cref="InvalidOperationException">If the database enters a faulted state while waiting.</exception>
    /// <exception cref="TimeoutException">If readiness is not reached within the timeout.</exception>
    Task WaitForReadyAsync(
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits until ready or faulted, then calls either onReady or onFaulted async delegate (returning a result).
    /// If the database is ready, onReady is called; if faulted, onFaulted is called (if provided), otherwise returns default.
    /// Throws on timeout.
    /// </summary>
    Task<TResult> OnReadyAsync<TResult>(
        Func<Task<TResult>> onReady,
        Func<Task<TResult>> onFaulted = null,
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits until ready or faulted, then calls either onReady or onFaulted delegate (returning a result).
    /// If the database is ready, onReady is called; if faulted, onFaulted is called (if provided), otherwise returns default.
    /// Throws on timeout.
    /// </summary>
    Task<TResult> OnReadyAsync<TResult>(
        Func<TResult> onReady,
        Func<TResult> onFaulted = null,
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits until ready or faulted, then calls either onReady or onFaulted action.
    /// If the database is ready, onReady is called; if faulted, onFaulted is called (if provided).
    /// Throws on timeout.
    /// </summary>
    Task OnReadyAsync(
        Action onReady,
        Action onFaulted = null,
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}

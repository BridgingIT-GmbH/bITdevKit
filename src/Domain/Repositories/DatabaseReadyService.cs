// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Thread-safe implementation of <see cref="IDatabaseReadyService"/> for tracking readiness and fault state
/// of multiple named databases. Allows modules to mark databases as ready or faulted,
/// and consumers to query or await readiness.
/// </summary>
public class DatabaseReadyService : IDatabaseReadyService
{
    /// <summary>
    /// The key used for the default database entry when no name is provided.
    /// </summary>
    private const string DefaultName = "__default__";

    /// <summary>
    /// Immutable state for a single database entry.
    /// </summary>
    private record State(bool IsReady, bool IsFaulted, string FaultMessage);

    private readonly ILogger<DatabaseReadyService> logger;
    private readonly ConcurrentDictionary<string, State> states = new(StringComparer.OrdinalIgnoreCase);

    public DatabaseReadyService(ILoggerFactory loggerFactory = null)
    {
        this.logger = loggerFactory?.CreateLogger<DatabaseReadyService>() ??
            NullLoggerFactory.Instance.CreateLogger<DatabaseReadyService>();
    }

    /// <summary>
    /// Returns the effective name (uses a special default if <paramref name="name"/> is null or empty).
    /// </summary>
    private string GetName(string name) => string.IsNullOrEmpty(name) ? DefaultName : name;

    /// <inheritdoc />
    public bool IsReady(string name = null)
    {
        if (name == null)
        {
            // If no name, all states must be ready and none faulted (if any exist)
            return !this.states.IsEmpty &&
                   this.states.All(x => x.Value.IsReady && !x.Value.IsFaulted);
        }
        return this.states.TryGetValue(this.GetName(name), out var state) && state.IsReady;
    }

    /// <inheritdoc />
    public bool IsFaulted(string name = null)
    {
        if (name == null)
        {
            // If any state is faulted, report faulted (if any exist)
            return this.states.Any(x => x.Value.IsFaulted);
        }
        return this.states.TryGetValue(this.GetName(name), out var state) && state.IsFaulted;
    }

    /// <inheritdoc />
    public string FaultMessage(string name = null)
    {
        if (name == null)
        {
            // Return the first fault message found (in insertion order, if any)
            var firstFaulted = this.states.Keys
                .OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => this.states[k])
                .FirstOrDefault(v => v.IsFaulted);
            return firstFaulted?.FaultMessage;
        }
        return this.states.TryGetValue(this.GetName(name), out var state) ? state.FaultMessage : null;
    }

    /// <inheritdoc />
    public void SetReady(string name = null)
    {
        var effectiveName = this.GetName(name);
        this.states.AddOrUpdate(
            effectiveName,
            _ => new State(true, false, null),
            (_, _) => new State(true, false, null));

        this.logger.LogDebug("{LogKey} database ready state set (name={DatabaseName})", Constants.LogKey, effectiveName);
    }

    /// <inheritdoc />
    public void SetFaulted(string name = null, string message = null)
    {
        var effectiveName = this.GetName(name);
        this.states.AddOrUpdate(
            effectiveName,
            _ => new State(false, true, message),
            (_, _) => new State(false, true, message));

        this.logger.LogDebug("{LogKey} database faulted state set (name={DatabaseName}, message={FaultMessage})", Constants.LogKey, effectiveName, message ?? "none");
    }

    /// <inheritdoc />
    public async Task WaitForReadyAsync(
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(200);
        timeout ??= TimeSpan.FromSeconds(30);

        var effectiveName = name != null ? this.GetName(name) : "all";
        this.logger.LogDebug("{LogKey} database ready wait started (name={DatabaseName}, pollInterval={PollInterval}ms, timeout={Timeout}s)", Constants.LogKey, effectiveName, pollInterval.Value.TotalMilliseconds, timeout.Value.TotalSeconds);

        var start = DateTime.UtcNow;
        while (true)
        {
            if (name == null)
            {
                if (!this.states.IsEmpty)
                {
                    if (this.states.Any(x => x.Value.IsFaulted))
                    {
                        // Throw on the first faulted state
                        var faulted = this.states.First(x => x.Value.IsFaulted);
                        this.logger.LogDebug("{LogKey} database ready wait detected faulted state (name={DatabaseName}, message={FaultMessage})", Constants.LogKey, faulted.Key, faulted.Value.FaultMessage ?? "Unknown error");

                        throw new InvalidOperationException($"Database '{faulted.Key}' is faulted: {faulted.Value.FaultMessage ?? "Unknown error"}");
                    }
                    if (this.states.All(x => x.Value.IsReady && !x.Value.IsFaulted))
                    {
                        var readyNames = string.Join(", ", this.states.Keys.OrderBy(k => k, StringComparer.Ordinal));
                        this.logger.LogDebug("{LogKey} database ready wait completed, all databases ready (count={DatabaseCount}, names=[{DatabaseNames}], elapsed={ElapsedMs}ms)", Constants.LogKey, this.states.Count, readyNames, (DateTime.UtcNow - start).TotalMilliseconds);

                        return; // All are ready!
                    }

                    this.logger.LogDebug("{LogKey} database ready wait polling (ready={ReadyCount}/{TotalCount}, elapsed={ElapsedMs}ms)", Constants.LogKey, this.states.Count(x => x.Value.IsReady), this.states.Count, (DateTime.UtcNow - start).TotalMilliseconds);
                }
                else
                {
                    this.logger.LogDebug("{LogKey} database ready wait polling, no databases registered yet (elapsed={ElapsedMs}ms)", Constants.LogKey, (DateTime.UtcNow - start).TotalMilliseconds);
                }
            }
            else
            {
                var stateName = this.GetName(name);
                if (this.states.TryGetValue(stateName, out var state))
                {
                    if (state.IsFaulted)
                    {
                        this.logger.LogDebug("{LogKey} database ready wait detected faulted state (name={DatabaseName}, message={FaultMessage})", Constants.LogKey, stateName, state.FaultMessage ?? "Unknown error");

                        throw new InvalidOperationException($"Database '{stateName}' is faulted: {state.FaultMessage ?? "Unknown error"}");
                    }

                    if (state.IsReady)
                    {
                        this.logger.LogDebug("{LogKey} database ready wait completed (name={DatabaseName}, elapsed={ElapsedMs}ms)", Constants.LogKey, stateName, (DateTime.UtcNow - start).TotalMilliseconds);

                        return;
                    }

                    this.logger.LogDebug("{LogKey} database ready wait polling, not ready yet (name={DatabaseName}, elapsed={ElapsedMs}ms)", Constants.LogKey, stateName, (DateTime.UtcNow - start).TotalMilliseconds);
                }
                else
                {
                    this.logger.LogDebug("{LogKey} database ready wait polling, database not registered yet (name={DatabaseName}, elapsed={ElapsedMs}ms)", Constants.LogKey, stateName, (DateTime.UtcNow - start).TotalMilliseconds);
                }
            }

            if (DateTime.UtcNow - start > timeout)
            {
                this.logger.LogDebug("{LogKey} database ready wait timeout (name={DatabaseName}, timeout={Timeout}s)", Constants.LogKey, effectiveName, timeout.Value.TotalSeconds);
                throw new TimeoutException(
                    name == null
                        ? "Not all databases were ready within the timeout period."
                        : $"Database '{name}' was not ready within the timeout period.");
            }

            await Task.Delay(pollInterval.Value, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<TResult> OnReadyAsync<TResult>(
        Func<Task<TResult>> onReady,
        Func<Task<TResult>> onFaulted = null,
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onReady);

        var stateName = this.GetName(name);
        pollInterval ??= TimeSpan.FromMilliseconds(200);
        timeout ??= TimeSpan.FromSeconds(30);

        this.logger.LogDebug("{LogKey} database ready callback wait started (name={DatabaseName}, pollInterval={PollInterval}ms, timeout={Timeout}s)", Constants.LogKey, stateName, pollInterval.Value.TotalMilliseconds, timeout.Value.TotalSeconds);

        var start = DateTime.UtcNow;
        while (true)
        {
            if (this.states.TryGetValue(stateName, out var state))
            {
                if (state.IsFaulted)
                {
                    this.logger.LogDebug("{LogKey} database ready callback executing faulted handler (name={DatabaseName}, message={FaultMessage})", Constants.LogKey, stateName, state.FaultMessage ?? "none");

                    return onFaulted != null ? await onFaulted().ConfigureAwait(false) : default;
                }

                if (state.IsReady)
                {
                    this.logger.LogDebug("{LogKey} database ready callback executing ready handler (name={DatabaseName}, elapsed={ElapsedMs}ms)", Constants.LogKey, stateName, (DateTime.UtcNow - start).TotalMilliseconds);

                    return await onReady().ConfigureAwait(false);
                }
            }

            if (DateTime.UtcNow - start > timeout)
            {
                this.logger.LogDebug("{LogKey} database ready callback timeout (name={DatabaseName}, timeout={Timeout}s)", Constants.LogKey, stateName, timeout.Value.TotalSeconds);

                throw new TimeoutException($"Database '{stateName}' was not ready or faulted within the timeout period.");
            }

            await Task.Delay(pollInterval.Value, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<TResult> OnReadyAsync<TResult>(
        Func<TResult> onReady,
        Func<TResult> onFaulted = null,
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onReady);

        var stateName = this.GetName(name);
        pollInterval ??= TimeSpan.FromMilliseconds(200);
        timeout ??= TimeSpan.FromSeconds(30);

        this.logger.LogDebug("{LogKey} database ready callback wait started (name={DatabaseName}, pollInterval={PollInterval}ms, timeout={Timeout}s)", Constants.LogKey, stateName, pollInterval.Value.TotalMilliseconds, timeout.Value.TotalSeconds);

        var start = DateTime.UtcNow;
        while (true)
        {
            if (this.states.TryGetValue(stateName, out var state))
            {
                if (state.IsFaulted)
                {
                    this.logger.LogDebug("{LogKey} database ready callback executing faulted handler (name={DatabaseName}, message={FaultMessage})", Constants.LogKey, stateName, state.FaultMessage ?? "none");

                    return onFaulted != null ? onFaulted() : default;
                }

                if (state.IsReady)
                {
                    this.logger.LogDebug("{LogKey} database ready callback executing ready handler (name={DatabaseName}, elapsed={ElapsedMs}ms)", Constants.LogKey, stateName, (DateTime.UtcNow - start).TotalMilliseconds);

                    return onReady();
                }
            }

            if (DateTime.UtcNow - start > timeout)
            {
                this.logger.LogDebug("{LogKey} database ready callback timeout (name={DatabaseName}, timeout={Timeout}s)", Constants.LogKey, stateName, timeout.Value.TotalSeconds);

                throw new TimeoutException($"Database '{stateName}' was not ready or faulted within the timeout period.");
            }

            await Task.Delay(pollInterval.Value, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task OnReadyAsync(
        Action onReady,
        Action onFaulted = null,
        string name = null,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onReady);

        var stateName = this.GetName(name);
        pollInterval ??= TimeSpan.FromMilliseconds(200);
        timeout ??= TimeSpan.FromSeconds(30);

        this.logger.LogDebug("{LogKey} database ready callback wait started (name={DatabaseName}, pollInterval={PollInterval}ms, timeout={Timeout}s)", Constants.LogKey, stateName, pollInterval.Value.TotalMilliseconds, timeout.Value.TotalSeconds);

        var start = DateTime.UtcNow;
        while (true)
        {
            if (this.states.TryGetValue(stateName, out var state))
            {
                if (state.IsFaulted)
                {
                    this.logger.LogDebug("{LogKey} database ready callback executing faulted handler (name={DatabaseName}, message={FaultMessage})", Constants.LogKey, stateName, state.FaultMessage ?? "none");
                    onFaulted?.Invoke();

                    return;
                }
                if (state.IsReady)
                {
                    this.logger.LogDebug("{LogKey} database ready callback executing ready handler (name={DatabaseName}, elapsed={ElapsedMs}ms)", Constants.LogKey, stateName, (DateTime.UtcNow - start).TotalMilliseconds);
                    onReady();

                    return;
                }
            }

            if (DateTime.UtcNow - start > timeout)
            {
                this.logger.LogDebug("{LogKey} database ready callback timeout (name={DatabaseName}, timeout={Timeout}s)", Constants.LogKey, stateName, timeout.Value.TotalSeconds);

                throw new TimeoutException($"Database '{stateName}' was not ready or faulted within the timeout period.");
            }

            await Task.Delay(pollInterval.Value, cancellationToken);
        }
    }
}
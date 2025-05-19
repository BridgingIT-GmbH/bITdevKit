// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

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

    private readonly ConcurrentDictionary<string, State> states = new(StringComparer.OrdinalIgnoreCase);

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
        this.states.AddOrUpdate(
            this.GetName(name),
            _ => new State(true, false, null),
            (_, _) => new State(true, false, null));
    }

    /// <inheritdoc />
    public void SetFaulted(string name = null, string message = null)
    {
        this.states.AddOrUpdate(
            this.GetName(name),
            _ => new State(false, true, message),
            (_, _) => new State(false, true, message));
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
                        throw new InvalidOperationException($"Database '{faulted.Key}' is faulted: {faulted.Value.FaultMessage ?? "Unknown error"}");
                    }
                    if (this.states.All(x => x.Value.IsReady && !x.Value.IsFaulted))
                    {
                        // All are ready!
                        return;
                    }
                }
            }
            else
            {
                var stateName = this.GetName(name);
                if (this.states.TryGetValue(stateName, out var state))
                {
                    if (state.IsFaulted)
                    {
                        throw new InvalidOperationException($"Database '{stateName}' is faulted: {state.FaultMessage ?? "Unknown error"}");
                    }

                    if (state.IsReady)
                    {
                        return;
                    }
                }
            }

            if (DateTime.UtcNow - start > timeout)
            {
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
        if (onReady is null)
        {
            throw new ArgumentNullException(nameof(onReady));
        }

        var stateName = this.GetName(name);
        pollInterval ??= TimeSpan.FromMilliseconds(200);
        timeout ??= TimeSpan.FromSeconds(30);

        var start = DateTime.UtcNow;
        while (true)
        {
            if (this.states.TryGetValue(stateName, out var state))
            {
                if (state.IsFaulted)
                {
                    return onFaulted != null ? await onFaulted().ConfigureAwait(false) : default;
                }

                if (state.IsReady)
                {
                    return await onReady().ConfigureAwait(false);
                }
            }

            if (DateTime.UtcNow - start > timeout)
            {
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
        if (onReady is null)
        {
            throw new ArgumentNullException(nameof(onReady));
        }

        var stateName = this.GetName(name);
        pollInterval ??= TimeSpan.FromMilliseconds(200);
        timeout ??= TimeSpan.FromSeconds(30);

        var start = DateTime.UtcNow;
        while (true)
        {
            if (this.states.TryGetValue(stateName, out var state))
            {
                if (state.IsFaulted)
                {
                    return onFaulted != null ? onFaulted() : default;
                }

                if (state.IsReady)
                {
                    return onReady();
                }
            }

            if (DateTime.UtcNow - start > timeout)
            {
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
        if (onReady is null)
        {
            throw new ArgumentNullException(nameof(onReady));
        }

        var stateName = this.GetName(name);
        pollInterval ??= TimeSpan.FromMilliseconds(200);
        timeout ??= TimeSpan.FromSeconds(30);

        var start = DateTime.UtcNow;
        while (true)
        {
            if (this.states.TryGetValue(stateName, out var state))
            {
                if (state.IsFaulted)
                {
                    onFaulted?.Invoke();
                    return;
                }
                if (state.IsReady)
                {
                    onReady();
                    return;
                }
            }

            if (DateTime.UtcNow - start > timeout)
            {
                throw new TimeoutException($"Database '{stateName}' was not ready or faulted within the timeout period.");
            }

            await Task.Delay(pollInterval.Value, cancellationToken);
        }
    }
}
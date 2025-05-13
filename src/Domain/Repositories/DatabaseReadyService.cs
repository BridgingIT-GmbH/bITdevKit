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
        => this.states.TryGetValue(this.GetName(name), out var state) && state.IsReady;

    /// <inheritdoc />
    public bool IsFaulted(string name = null)
        => this.states.TryGetValue(this.GetName(name), out var state) && state.IsFaulted;

    /// <inheritdoc />
    public string FaultMessage(string name = null)
        => this.states.TryGetValue(this.GetName(name), out var state) ? state.FaultMessage : null;

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
        var dbName = this.GetName(name);
        pollInterval ??= TimeSpan.FromMilliseconds(200);
        timeout ??= TimeSpan.FromSeconds(10);

        var start = DateTime.UtcNow;
        while (true)
        {
            if (this.states.TryGetValue(dbName, out var state))
            {
                if (state.IsFaulted)
                    throw new InvalidOperationException($"Database '{dbName}' is faulted: {state.FaultMessage ?? "Unknown error"}");
                if (state.IsReady)
                    return;
            }

            if (DateTime.UtcNow - start > timeout)
                throw new TimeoutException($"Database '{dbName}' was not ready within the timeout period.");

            await Task.Delay(pollInterval.Value, cancellationToken);
        }
    }
}
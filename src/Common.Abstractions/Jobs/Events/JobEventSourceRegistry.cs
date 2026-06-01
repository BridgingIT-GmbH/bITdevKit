// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Tracks which event-source adapters are connected to the scheduler.
/// </summary>
public sealed class JobEventSourceRegistry
{
    private readonly HashSet<string> sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly object syncRoot = new();

    /// <summary>
    /// Registers a logical event source.
    /// </summary>
    public void Register(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        lock (this.syncRoot)
        {
            this.sources.Add(source.Trim());
        }
    }

    /// <summary>
    /// Determines whether a logical event source is registered.
    /// </summary>
    public bool IsRegistered(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        lock (this.syncRoot)
        {
            return this.sources.Contains(source.Trim());
        }
    }
}
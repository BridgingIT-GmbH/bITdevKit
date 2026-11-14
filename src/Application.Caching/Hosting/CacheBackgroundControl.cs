// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Concurrent;

/// <summary>Controls configured background source admission on this node without changing startup policy.</summary>
/// <param name="catalog">The immutable startup collection catalog.</param>
/// <param name="timeProvider">The node clock used for control timestamps.</param>
/// <example><code>var paused = control.Pause("cards", "console");</code></example>
public sealed class CacheBackgroundControl(CacheCatalog catalog, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, CacheBackgroundState> states = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets configured capabilities and the current node-local pause state.</summary>
    /// <param name="collection">The declared collection name.</param>
    /// <returns>The bounded control state, which resets when the host restarts.</returns>
    public CacheBackgroundState GetState(string collection)
    {
        var binding = catalog.Collections[CacheIdentityNormalizer.Name(collection)];
        return this.states.GetValueOrDefault(binding.Name) ?? new(binding.Name,
            catalog.Options.Enabled && binding.Options.Enabled && binding.Options.Refresh.Enabled,
            catalog.Options.Enabled && binding.Options.Enabled && binding.Options.Warmup.Enabled, false, null, null);
    }

    /// <summary>Stops new configured background units at their next admission boundary; already-owned work retains its lifetime.</summary>
    /// <param name="collection">The declared collection name.</param>
    /// <param name="origin">The bounded management adapter name.</param>
    /// <returns>The resulting state or a metadata-validation failure.</returns>
    public Result<CacheBackgroundState> Pause(string collection, string origin) => this.Change(collection, true, origin);

    /// <summary>Releases a node-local pause without enabling capabilities disabled by startup configuration.</summary>
    /// <param name="collection">The declared collection name.</param>
    /// <param name="origin">The bounded management adapter name.</param>
    /// <returns>The resulting state or a metadata-validation failure.</returns>
    public Result<CacheBackgroundState> Resume(string collection, string origin) => this.Change(collection, false, origin);

    private Result<CacheBackgroundState> Change(string collection, bool paused, string origin)
    {
        if (origin?.Length > 128)
        {
            return Result<CacheBackgroundState>.Failure(new CacheError(CacheErrorCode.Validation, "Background control metadata exceeds its bound."));
        }

        var current = this.GetState(collection);
        var changed = current with { Paused = paused, Origin = origin, ChangedAt = timeProvider.GetUtcNow() };
        this.states[current.Collection] = changed;
        return Result<CacheBackgroundState>.Success(changed);
    }
}

/// <summary>Describes startup opt-ins separately from runtime pause state on the selected node.</summary>
/// <param name="Collection">The canonical declared collection name.</param>
/// <param name="RefreshConfigured">Whether proactive refresh is configured and enabled.</param>
/// <param name="WarmupConfigured">Whether warm-up is configured and enabled.</param>
/// <param name="Paused">Whether new automatic source work is paused on this node.</param>
/// <param name="Origin">The last bounded adapter origin.</param>
/// <param name="ChangedAt">The last control change, or null before any change.</param>
/// <example><code>var canRefresh = state.RefreshConfigured &amp;&amp; !state.Paused;</code></example>
public sealed record CacheBackgroundState(string Collection, bool RefreshConfigured, bool WarmupConfigured,
    bool Paused, string Origin, DateTimeOffset? ChangedAt);
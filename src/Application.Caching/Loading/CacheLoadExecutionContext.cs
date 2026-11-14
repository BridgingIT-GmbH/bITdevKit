// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Tracks source callback nesting in the current asynchronous flow so nested source misses can be rejected before admission.</summary>
/// <example><code>if (CacheLoadExecutionContext.IsLoading) { /* reject nested source work */ }</code></example>
public static class CacheLoadExecutionContext
{
    private static readonly AsyncLocal<int> Depth = new();

    /// <summary>Gets whether execution is currently inside an owned cache source callback.</summary>
    public static bool IsLoading => Depth.Value > 0;

    /// <summary>Runs a source callback under the nesting guard and restores the previous depth even when it fails.</summary>
    /// <typeparam name="T">The declared value or operation result type.</typeparam>
    public static async Task<T> InvokeAsync<T>(Func<ValueTask<T>> callback)
    {
        Depth.Value++;
        try { return await callback().ConfigureAwait(false); }
        finally { Depth.Value--; }
    }
}

/// <summary>Contains one owned completion and whether this caller joined pre-existing source work.</summary>
/// <typeparam name="T">The declared value or operation result type.</typeparam>
/// <example><code>var joined = completion.Joined;</code></example>
public readonly record struct CacheFlightCompletion<T>(T Value, bool Joined);

/// <summary>Contains a source value or confirmed absence together with its actual publication outcome and optional acquired version.</summary>
/// <typeparam name="T">The declared value or operation result type.</typeparam>
/// <example><code>var stored = result.StorageOutcome == CacheStorageOutcome.Stored;</code></example>
public sealed record CacheSourceResult<T>(T Value, bool IsNotFound, CacheStorageOutcome StorageOutcome,
    CacheEntryVersion? Version, CacheAccessMode Mode = CacheAccessMode.Normal, bool Acquired = false) : ICacheSourceReceipt;

/// <summary>Exposes source completion facts used by source coordination without retaining a transferable uncached graph across nodes.</summary>
/// <example><code>var published = receipt.Version.HasValue;</code></example>
public interface ICacheSourceReceipt
{
    /// <summary>Gets whether the repeatable source confirmed that the requested value does not exist.</summary>
    bool IsNotFound { get; }

    /// <summary>Gets the actual retention or publication result for this owned source completion.</summary>
    CacheStorageOutcome StorageOutcome { get; }

    /// <summary>Gets the confirmed acquired or published version, or null when no retained transferable version exists.</summary>
    CacheEntryVersion? Version { get; }
}

/// <summary>Records monotonic loss of owned source publication authority across asynchronous source and lease-renewal work.</summary>
/// <example><code>var authority = new CacheOwnedAuthority(); authority.Lose();</code></example>
public sealed class CacheOwnedAuthority
{
    private int lost;

    /// <summary>Gets whether this owned execution has not observed loss of its publication authority.</summary>
    public bool IsConfirmed => Volatile.Read(ref this.lost) == 0;

    /// <summary>Irreversibly marks publication authority as lost for this owned execution.</summary>
    public void Lose() => Interlocked.Exchange(ref this.lost, 1);
}
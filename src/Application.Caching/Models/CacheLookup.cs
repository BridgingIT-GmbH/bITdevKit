// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Reports a typed read-only value, absence, or miss with independent access/storage facts.</summary>
/// <typeparam name="T">The collection's value contract.</typeparam>
/// <example><code>if (result.IsSuccess &amp;&amp; result.Value.Found) { UseReadOnly(result.Value.Value); }</code></example>
public readonly record struct CacheLookup<T>
{
    /// <summary>Creates an outcome; payload hits and confirmed absence are mutually exclusive.</summary>
    public CacheLookup(bool found, T value, CacheLookupOutcome lookupOutcome, CacheStorageOutcome storageOutcome,
        CacheAccessMode accessMode = CacheAccessMode.Normal, bool isNotFound = false, CacheEntryVersion? version = null)
    {
        if (found && isNotFound)
        {
            throw new ArgumentException("A payload value and confirmed absence cannot both be present.");
        }

        if (version is not null && (!(found || isNotFound) ||
            (lookupOutcome != CacheLookupOutcome.Hit && storageOutcome != CacheStorageOutcome.Stored)))
        {
            throw new ArgumentException("Only acquired or confirmed published snapshots carry a version.", nameof(version));
        }

        if (storageOutcome == CacheStorageOutcome.Stored && version is null)
        {
            throw new ArgumentException("Confirmed publication requires its acquired version.", nameof(version));
        }

        this.Found = found;
        this.Value = value;
        this.LookupOutcome = lookupOutcome;
        this.StorageOutcome = storageOutcome;
        this.AccessMode = accessMode;
        this.IsNotFound = isNotFound;
        this.Version = version;
    }

    /// <summary>Gets whether a value is available, including a permitted null/empty value.</summary>
    public bool Found { get; }

    /// <summary>Gets the read-only result. Consumers copy before mutation; reference identity is not guaranteed.</summary>
    public T Value { get; }

    /// <summary>Gets how the result was acquired.</summary>
    public CacheLookupOutcome LookupOutcome { get; }

    /// <summary>Gets actual retention information.</summary>
    public CacheStorageOutcome StorageOutcome { get; }

    /// <summary>Gets the requested operation mode.</summary>
    public CacheAccessMode AccessMode { get; }

    /// <summary>Gets whether the source explicitly confirmed absence.</summary>
    public bool IsNotFound { get; }

    /// <summary>Gets the acquired stored version, absent for misses and unretained/indeterminate outcomes.</summary>
    public CacheEntryVersion? Version { get; }
}

/// <summary>Identifies a read access path independently of its retention outcome.</summary>
public enum CacheLookupOutcome
{
    /// <summary>No valid entry was found and no source was invoked.</summary>
    Miss,

    /// <summary>A valid positive or negative provider entry was acquired.</summary>
    Hit,

    /// <summary>This work owned source execution.</summary>
    Loaded,

    /// <summary>This caller joined existing source work.</summary>
    Joined,

    /// <summary>Explicit disabled/bypass work obtained an unretained result.</summary>
    Bypassed
}

/// <summary>Reports storage acknowledgement independently of source success.</summary>
public enum CacheStorageOutcome
{
    /// <summary>No write was attempted.</summary>
    None,

    /// <summary>A new stored version was confirmed.</summary>
    Stored,

    /// <summary>Policy explicitly suppressed retention.</summary>
    Suppressed,

    /// <summary>Finite admission rejected retention.</summary>
    Rejected,

    /// <summary>A write was confirmed to fail.</summary>
    Failed,

    /// <summary>Retention may have committed, but is not confirmed.</summary>
    Indeterminate,

    /// <summary>A newer version or invalidation prevented publication.</summary>
    Conflict
}

/// <summary>Selects normal lookup or explicit non-storing typed source access.</summary>
public enum CacheAccessMode
{
    /// <summary>Use valid provider entries and ordinary loading policy.</summary>
    Normal,

    /// <summary>Skip lookup and suppress publication of bypass-owned work.</summary>
    Bypass
}

/// <summary>Chooses one bounded shared flight or separate admitted same-key source work.</summary>
public enum CacheLoadingStrategy
{
    /// <summary>Coalesce eligible work within the selected scope.</summary>
    Shared,

    /// <summary>Run separately while retaining resource admission and publication fencing.</summary>
    Independent
}

/// <summary>Defines where source-load ownership is coordinated.</summary>
public enum CacheCoordinationScope
{
    /// <summary>One owner per identity within one process.</summary>
    NodeLocal,

    /// <summary>Use authoritative ownership in the selected complete provider.</summary>
    SharedStore
}
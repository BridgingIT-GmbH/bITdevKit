// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines the advanced feature's finite process policies and immutable startup defaults.</summary>
/// <example><code>var options = new ApplicationCachingOptions();</code></example>
public sealed record ApplicationCachingOptions
{
    /// <summary>Gets whether advanced cache storage is enabled. Disabled typed loads retain bounded source protection.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets application isolation namespace.</summary>
    public string Namespace { get; init; } = "default";

    /// <summary>Gets shared process admission and memory ceilings.</summary>
    public CacheProcessLimits Limits { get; init; } = new();

    /// <summary>Gets global collection defaults before named overrides.</summary>
    public CacheCollectionOptions Defaults { get; init; } = new();

    /// <summary>Gets cache-owned worker timing.</summary>
    public CacheBackgroundServiceOptions BackgroundServices { get; init; } = new();

    /// <summary>Gets shared hot retention sublimit within process resident charges.</summary>
    public CacheHotEntryBudget HotEntryBudget { get; init; } = new();

    /// <summary>Gets bounded metrics and sample retention.</summary>
    public CacheDiagnosticsOptions Diagnostics { get; init; } = new();
}
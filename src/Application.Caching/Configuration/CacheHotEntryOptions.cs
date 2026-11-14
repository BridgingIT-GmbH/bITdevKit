// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Controls default-off second-access local retention with mandatory authoritative validation.</summary>
/// <example><code>var options = new CacheHotEntryOptions();</code></example>
public sealed record CacheHotEntryOptions
{
    /// <summary>Gets whether eligible representations may be retained.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>Gets binary, decoded, or combined retained representation.</summary>
    public CacheHotEntryRepresentation Representation { get; init; } = CacheHotEntryRepresentation.Binary;

    /// <summary>Gets only strict authoritative validation is supported.</summary>
    public CacheHotEntryValidation Validation { get; init; } = CacheHotEntryValidation.Strict;

    /// <summary>Gets collection hot retained-byte ceiling.</summary>
    public long MaxResidentBytes { get; init; } = 64 * CacheDefaults.MiB;

    /// <summary>Gets retained hot entry count.</summary>
    public int MaxEntries { get; init; } = 10000;

    /// <summary>Gets the combined retained representations and metadata/bookkeeping charge for one version.</summary>
    public long MaxEntryBytes { get; init; } = 256 * CacheDefaults.KiB;

    /// <summary>Gets local hot retention idle period.</summary>
    public TimeSpan MaxIdleTime { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets bounded second-successful-access tracking.</summary>
    public CacheHotAdmissionOptions Admission { get; init; } = new();
}
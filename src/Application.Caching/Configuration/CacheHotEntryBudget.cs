// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines the shared hot representation sublimit, within resident accounting.</summary>
/// <example><code>var options = new CacheHotEntryBudget();</code></example>
public sealed record CacheHotEntryBudget
{
    /// <summary>Gets process hot retention capacity.</summary>
    public long MaxResidentBytes { get; init; } = 128 * CacheDefaults.MiB;
}
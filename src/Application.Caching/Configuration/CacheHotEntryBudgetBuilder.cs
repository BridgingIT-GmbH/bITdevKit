// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records the shared hot representation budget within the process resident ceiling.</summary>
/// <example><code>hot.MaxResidentBytes(ByteSize.Megabytes(64));</code></example>
public sealed class CacheHotEntryBudgetBuilder : PatchOptionsBuilder<CacheHotEntryBudget, CacheHotEntryBudgetBuilder>
{
    /// <summary>Sets the shared hot retention byte ceiling without enabling any collection.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>hot.MaxResidentBytes(ByteSize.Megabytes(64));</code></example>
    public CacheHotEntryBudgetBuilder MaxResidentBytes(long value) => this.With(options => options.MaxResidentBytes, value);
}
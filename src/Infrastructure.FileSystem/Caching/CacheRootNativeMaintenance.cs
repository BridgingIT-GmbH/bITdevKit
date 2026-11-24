// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Owns a bounded quota-credit page, its last examined key and the memory charges retained by its decoded records.</summary>
/// <example><code>using var page = await ledger.GetCreditsAsync(after, 16, token);</code></example>
public sealed record CacheNativeCreditPage(IReadOnlyList<CacheSpaceCredit> Credits, CacheIndexKey Continuation, int Examined,
    IReadOnlyList<ResourceCharge> Charges) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() { foreach (var charge in this.Charges) { charge.Dispose(); } }
}
// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Globalization;

/// <summary>
/// Computes stable hashes for logical blob-store listing queries.
/// </summary>
/// <example>
/// <code>
/// var hash = BlobQueryHash.Compute(query);
/// </code>
/// </example>
public static class BlobQueryHash
{
    /// <summary>
    /// Computes a query hash from the logical query shape.
    /// </summary>
    /// <param name="query">The blob query to hash.</param>
    /// <returns>A stable hash for container, prefix, and full-scan approval.</returns>
    /// <example>
    /// <code>
    /// var hash = BlobQueryHash.Compute(query);
    /// </code>
    /// </example>
    public static string Compute(BlobQuery query) =>
        HashHelper.ComputeSha256(string.Join("|",
            query?.Container ?? string.Empty,
            query?.Prefix ?? string.Empty,
            query?.AllowFullScan.ToString(CultureInfo.InvariantCulture) ?? bool.FalseString));
}

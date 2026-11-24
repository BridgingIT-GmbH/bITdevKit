// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Security.Cryptography;
using System.Text;

/// <summary>Builds stable ASCII index identifiers within a relational provider's character limit.</summary>
/// <remarks>Long identifiers retain a twelve-character SHA-256 suffix of their complete ASCII name. Callers provide validated physical identifiers.</remarks>
/// <example><code>var name = RelationalIndexName.Create("__Catalog_Products", "TenantId", "Code");</code></example>
public static class RelationalIndexName
{
    /// <summary>Creates an index identifier within the common 63-character relational limit.</summary>
    /// <param name="table">The physical table name.</param>
    /// <param name="columns">The ordered indexed column names.</param>
    /// <returns>The unchanged short name, or a truncated prefix with a stable hash suffix.</returns>
    public static string Create(string table, params string[] columns) => Create(table, 63, columns);

    /// <summary>Creates an index identifier within an explicit provider character limit.</summary>
    /// <param name="table">The physical table name.</param>
    /// <param name="maximumLength">Maximum identifier length, at least fourteen characters.</param>
    /// <param name="columns">The ordered indexed column names.</param>
    /// <returns>The unchanged short name, or a truncated prefix with a stable hash suffix.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The limit cannot hold a prefix, separator and hash suffix.</exception>
    /// <example><code>var name = RelationalIndexName.Create("Products", 128, "TenantId", "Code");</code></example>
    public static string Create(string table, int maximumLength, params string[] columns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumLength, 14);
        var name = "IX_" + table + "_" + string.Join('_', columns);
        return name.Length <= maximumLength ? name : name[..(maximumLength - 13)] + "_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(name)))[..12];
    }
}
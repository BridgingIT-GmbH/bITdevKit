// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

/// <summary>Builds stable cache index identifiers within the shared relational-engine length limit.</summary>
/// <example><code>var name = CacheIndexName.Create("__Caching_Entries", "CollectionId", "Id");</code></example>
public static class CacheIndexName
{
    /// <summary>Creates an index name, retaining a deterministic hash suffix when truncation is required.</summary>
    /// <param name="table">The validated physical table name.</param>
    /// <param name="columns">The ordered indexed column names.</param>
    /// <returns>A stable identifier of at most 63 characters.</returns>
    public static string Create(string table, params string[] columns) => RelationalIndexName.Create(table, columns);
}
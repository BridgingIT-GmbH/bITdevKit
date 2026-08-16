// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Represents in memory hierarchy query provider.
/// </summary>
public class InMemoryHierarchyQueryProvider : IHierarchyQueryProvider
{
    /// <summary>
    /// Creates path query.
    /// </summary>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="tableName">The table name used by the operation.</param>
    /// <param name="idColumn">The id column used by the operation.</param>
    /// <param name="parentIdColumn">The parent id column used by the operation.</param>
    /// <param name="idType">The id type used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public string CreatePathQuery(string schema, string tableName, string idColumn, string parentIdColumn, Type idType)
    {
        // InMemory provider can't do SQL queries (SqlQueryRaw) -> 'Relational-specific methods can only be used when the context is using a relational database provider.'
        return string.Empty;
    }
}

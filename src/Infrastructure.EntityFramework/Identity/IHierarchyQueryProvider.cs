// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Creates SQL queries for hierarchical entity relationships based on the database provider.
/// </summary>
public interface IHierarchyQueryProvider
{
    /// <summary>
    /// Creates a SQL query to get the hierarchy path for an entity.
    /// </summary>
    string CreatePathQuery(string schema, string tableName, string idColumn, string parentIdColumn);
}
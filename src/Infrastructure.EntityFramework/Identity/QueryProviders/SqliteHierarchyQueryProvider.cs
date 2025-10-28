// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public class SqliteHierarchyQueryProvider : IHierarchyQueryProvider
{
    public string CreatePathQuery(string schema, string tableName, string idColumn, string parentIdColumn, Type idType)
    {
        var paramCast = idType switch
        {
            Type t when t == typeof(Guid) || t == typeof(Guid?) => "CAST(@p0 AS TEXT)", // SQLite stores GUIDs as TEXT
            Type t when t == typeof(int) || t == typeof(int?) => "CAST(@p0 AS INTEGER)",
            Type t when t == typeof(long) || t == typeof(long?) => "CAST(@p0 AS INTEGER)",
            Type t when t == typeof(string) => "@p0",
            _ => "@p0"
        };

        // SQLite doesn't use schemas, so the schema parameter is ignored
        // SQLite does support recursive CTEs (Common Table Expressions) with the WITH RECURSIVE syntax since version 3.8.3
        return $@"
        WITH RECURSIVE Hierarchy AS (
            SELECT {idColumn}, {parentIdColumn}, 0 AS Level
            FROM {tableName}
            WHERE {idColumn} = {paramCast}

            UNION ALL

            SELECT p.{idColumn}, p.{parentIdColumn}, h.Level + 1
            FROM {tableName} p
            INNER JOIN Hierarchy h ON p.{idColumn} = h.{parentIdColumn}
        )
        SELECT {idColumn}
        FROM Hierarchy
        WHERE {idColumn} != {paramCast}
        ORDER BY Level;";
    }
}

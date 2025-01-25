// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.Identity;

public class PostgreSqlHierarchyQueryProvider : IHierarchyQueryProvider
{
    public string CreatePathQuery(string schema, string tableName, string idColumn, string parentIdColumn)
    {
        return $@"
            WITH RECURSIVE Hierarchy AS (
                SELECT ""{idColumn}"", ""{parentIdColumn}"", 0 as Level
                FROM ""{schema}"".""{tableName}""
                WHERE ""{idColumn}"" = @p0

                UNION ALL

                SELECT p.""{idColumn}"", p.""{parentIdColumn}"", h.Level + 1
                FROM ""{schema}"".""{tableName}"" p
                INNER JOIN Hierarchy h ON p.""{idColumn}"" = h.""{parentIdColumn}""
                WHERE p.""{parentIdColumn}"" IS NOT NULL
            )
            SELECT ""{idColumn}""
            FROM Hierarchy
            WHERE ""{idColumn}"" != @p0
            ORDER BY Level;";
    }
}

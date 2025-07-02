﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;
public class SqlServerHierarchyQueryProvider : IHierarchyQueryProvider
{
    public string CreatePathQuery(string schema, string tableName, string idColumn, string parentIdColumn)
    {
        return $@"
        WITH Hierarchy AS (
            SELECT [{idColumn}], [{parentIdColumn}], 0 AS Level
            FROM [{schema}].[{tableName}]
            WHERE [{idColumn}] = @p0
            UNION ALL
            SELECT p.[{idColumn}], p.[{parentIdColumn}], h.Level + 1
            FROM [{schema}].[{tableName}] p
            INNER JOIN Hierarchy h ON p.[{idColumn}] = h.[{parentIdColumn}]
        )
        SELECT [{idColumn}]
        FROM Hierarchy
        WHERE [{idColumn}] != @p0
        ORDER BY Level;";
    }
}

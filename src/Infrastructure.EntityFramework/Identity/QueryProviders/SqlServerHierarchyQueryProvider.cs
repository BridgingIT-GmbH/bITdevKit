// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public class SqlServerHierarchyQueryProvider : IHierarchyQueryProvider
{
    public string CreatePathQuery(string schema, string tableName, string idColumn, string parentIdColumn, Type idType)
    {
        var paramCast = idType switch
        {
            Type t when t == typeof(Guid) || t == typeof(Guid?) => "CAST(@p0 AS uniqueidentifier)",
            Type t when t == typeof(int) || t == typeof(int?) => "CAST(@p0 AS int)",
            Type t when t == typeof(long) || t == typeof(long?) => "CAST(@p0 AS bigint)",
            Type t when t == typeof(string) => "@p0", // No cast needed for nvarchar
            _ => "@p0" // Fallback: no cast
        };

        return $@"
        WITH Hierarchy AS (
            SELECT [{idColumn}], [{parentIdColumn}], 0 AS Level
            FROM [{schema}].[{tableName}]
            WHERE [{idColumn}] = {paramCast}

            UNION ALL

            SELECT p.[{idColumn}], p.[{parentIdColumn}], h.Level + 1
            FROM [{schema}].[{tableName}] p
            INNER JOIN Hierarchy h ON p.[{idColumn}] = h.[{parentIdColumn}]
        )
        SELECT [{idColumn}]
        FROM Hierarchy
        WHERE [{idColumn}] != {paramCast}
        ORDER BY Level;";
    }
}

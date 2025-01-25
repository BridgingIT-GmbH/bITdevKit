// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.Identity;

public class InMemoryHierarchyQueryProvider : IHierarchyQueryProvider
{
    public string CreatePathQuery(
        string schema,
        string tableName,
        string idColumn,
        string parentColumn)
    {
        // InMemory provider cant do sql queries (SqlQueryRaw) -> 'Relational-specific methods can only be used when the context is using a relational database provider.'
        return string.Empty;
    }
}

// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

/// <summary>Selects the complete relational cache provider's native database profile.</summary>
/// <example><code>provider.Engine(CacheDatabaseEngine.SqlServer);</code></example>
public enum CacheDatabaseEngine
{
    /// <summary>Uses SQLite with host-local database access.</summary>
    Sqlite,

    /// <summary>Uses SQL Server with shared database access.</summary>
    SqlServer,

    /// <summary>Uses PostgreSQL with shared database access.</summary>
    PostgreSql
}
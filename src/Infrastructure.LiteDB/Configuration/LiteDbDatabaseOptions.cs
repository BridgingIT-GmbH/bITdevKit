// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

/// <summary>Configures an application-owned, named LiteDB database.</summary>
/// <example><code>services.AddLiteDbDatabase(o => o.ConnectionString = "/data/ledger.db");</code></example>
public sealed class LiteDbDatabaseOptions
{
    /// <summary>Gets or sets the local Direct-mode connection string.</summary>
    public string ConnectionString { get; set; }

    /// <summary>Gets or sets mapping configuration applied before the database opens.</summary>
    public Action<BsonMapper> ConfigureMapper { get; set; }
}

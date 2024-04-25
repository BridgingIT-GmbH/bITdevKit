// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

public class LiteDbContext : ILiteDbContext
{
    public LiteDbContext(string connectionString, BsonMapper bsonMapper = null)
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        this.Database = new LiteDatabase(connectionString, bsonMapper);
    }

    public LiteDatabase Database { get; }

    public void Dispose()
    {
        this.Database?.Dispose();
    }
}
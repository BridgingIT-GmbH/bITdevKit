// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

/// <summary>
/// Represents lite db context.
/// </summary>
public class LiteDbContext : ILiteDbContext
{
    /// <summary>
    /// Initializes a new instance of the <c>LiteDbContext</c> class.
    /// </summary>
    /// <param name="connectionString">The connection string used by the operation.</param>
    /// <param name="bsonMapper">The bson mapper used by the operation.</param>
    public LiteDbContext(string connectionString, BsonMapper bsonMapper = null)
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        this.Database = new LiteDatabase(connectionString, bsonMapper);
    }

    /// <summary>
    /// Gets the database.
    /// </summary>
    public LiteDatabase Database { get; }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        this.Database?.Dispose();
    }
}

// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Text.Json;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;

/// <summary>Stores immutable portable history DTOs with native filter and grouping metadata.</summary>
public static class LiteDbChangeHistoryStorage
{
    /// <summary>Appends a history row and creates the indexes used for bounded entity and change-set reads.</summary>
    /// <param name="database">The database in the originating write transaction.</param>
    /// <param name="collectionName">The selected history collection.</param>
    /// <param name="entityType">The full entity identity token.</param>
    /// <param name="row">The immutable history row.</param>
    public static void Insert(LiteDatabase database, string collectionName, Type entityType, ChangeHistoryRecord row)
    {
        var collection = database.GetCollection<BsonDocument>(collectionName);
        collection.EnsureIndex("entity", "$.EntityKey");
        collection.EnsureIndex("change_set", "$.ChangeSetId");
        collection.EnsureIndex("changed", "$.ChangedDateTicks");
        var document = database.Mapper.ToDocument(row);
        document["_id"] = row.Id;
        document["EntityKey"] = entityType.AssemblyQualifiedNameShort();
        document["Payload"] = JsonSerializer.Serialize(row);
        // Values live only in Payload. Metadata projections cannot accidentally expose them.
        document.Remove(nameof(row.OldValue));
        document.Remove(nameof(row.NewValue));
        collection.Insert(document);
    }

    /// <summary>Restores the immutable DTO and optionally removes serialized values.</summary>
    /// <param name="document">The stored row.</param>
    /// <param name="includeValues">Whether the caller was authorized to receive the values.</param>
    /// <returns>The portable history DTO.</returns>
    public static ChangeHistoryRecord Read(BsonDocument document, bool includeValues = true)
    {
        var row = JsonSerializer.Deserialize<ChangeHistoryRecord>(document["Payload"].AsString)!;
        return includeValues ? row : row with { OldValue = null, NewValue = null };
    }
}

// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

/// <summary>Supplies bounded native filter fields for durable work without indexing serialized payloads.</summary>
public interface ILiteDbIndexedRecord
{
    /// <summary>Writes native scalar selection columns into a stored document.</summary>
    /// <param name="document">The document whose identity, key, and payload must be preserved.</param>
    void WriteIndexes(BsonDocument document);
}

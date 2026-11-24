// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Auditing;
using System.Text.Json.Serialization;

/// <summary>Stores the complete canonical audit graph separately from mutable lifecycle state.</summary>
public sealed class LiteDbAuditRecord : ILiteDbIndexedRecord
{
    private byte[] content;
    private AuditRecord record;

    /// <summary>Gets or sets the original encoded graph, including its hash and integrity envelope.</summary>
    public byte[] Content { get => this.content; set { this.content = value; this.record = null; } }

    /// <summary>Gets or sets its committed publication position.</summary>
    public long Position { get; set; }

    /// <summary>Gets or sets lifecycle membership without modifying original evidence.</summary>
    public bool Archived { get; set; }

    /// <summary>Gets the verified canonical document projection.</summary>
    [JsonIgnore]
    public AuditRecord Record => this.record ??= Decode(this.Content);

    /// <summary>Gets the encoded original document size.</summary>
    [JsonIgnore]
    public int Bytes => this.Content.Length;

    /// <inheritdoc />
    public void WriteIndexes(BsonDocument document)
    {
        document["Archived"] = this.Archived;
        document["CompletedTicks"] = this.Record.CompletedUtc.UtcTicks;
        document["Position"] = this.Position;
        document["CreatedTicks"] = this.Record.StartedUtc.UtcTicks;
    }

    private static AuditRecord Decode(byte[] content)
    {
        var result = AuditRecordCodec.Decode(content);
        if (result.IsFailure) { throw new LiteDbProviderException(result); }

        return result.Value;
    }
}

/// <summary>Persists extraction identity, ordering, lifecycle, and replay rejection boundaries.</summary>
public sealed class LiteDbAuditState : ILiteDbIndexedRecord
{
    /// <summary>Gets or sets this file's source identity.</summary>
    public Guid StoreId { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the operator-controlled position epoch.</summary>
    public Guid StoreEpoch { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the last committed publication position.</summary>
    public long PublishedPosition { get; set; }

    /// <summary>Gets or sets the lifecycle membership generation.</summary>
    public long LifecycleGeneration { get; set; }

    /// <summary>Gets or sets the highest removed publication position.</summary>
    public long RemovedThrough { get; set; }

    /// <summary>Gets or sets the durable rejection cutoff for expired replays.</summary>
    public long RejectCompletedBefore { get; set; }

    /// <summary>Gets or sets the durable cutoff for delayed archived admissions.</summary>
    public long ArchiveCompletedBefore { get; set; }

    /// <inheritdoc />
    public void WriteIndexes(BsonDocument document)
    {
        document["StoreId"] = this.StoreId;
        document["StoreEpoch"] = this.StoreEpoch;
        document["Position"] = this.PublishedPosition;
    }
}

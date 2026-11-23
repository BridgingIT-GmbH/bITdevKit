// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using System.Text;
using System.Globalization;

/// <summary>Stores one immutable monitoring event with deterministic timestamp and arrival ordering.</summary>
public sealed class LiteDbFileEventRecord : ILiteDbIndexedRecord
{
    /// <summary>Gets or sets the complete detached event.</summary>
    public FileEvent Event { get; set; }

    /// <summary>Gets or sets the atomic arrival sequence.</summary>
    public long Sequence { get; set; }

    /// <summary>Gets or sets the publication content hash.</summary>
    public string Hash { get; set; }

    /// <summary>Gets the stable native ordering key.</summary>
    public string SortKey => this.Event.DetectedDate.UtcTicks.ToString("D20", CultureInfo.InvariantCulture) + ":" + this.Sequence.ToString("D20", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public void WriteIndexes(BsonDocument document)
    {
        document["LocationKey"] = HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(this.Event.LocationName));
        document["PathKey"] = HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(this.Event.FilePath));
        document["DetectedTicks"] = this.Event.DetectedDate.UtcTicks;
        document["SortKey"] = this.SortKey;
    }
}

/// <summary>Stores a processing outcome independently of its event and other processors.</summary>
public sealed class LiteDbFileProcessingRecord : ILiteDbIndexedRecord
{
    /// <summary>Gets or sets the complete outcome.</summary>
    public FileProcessingResult Result { get; set; }

    /// <summary>Gets or sets the immutable outcome hash.</summary>
    public string Hash { get; set; }

    /// <inheritdoc />
    public void WriteIndexes(BsonDocument document)
    {
        document["EventId"] = this.Result.FileEventId;
        document["CreatedTicks"] = this.Result.ProcessedAt.UtcTicks;
    }
}

/// <summary>Stores the latest known presence of one location and opaque logical path.</summary>
public sealed class LiteDbFilePresenceRecord : ILiteDbIndexedRecord
{
    /// <summary>Gets or sets the monitoring location.</summary>
    public string Location { get; set; }

    /// <summary>Gets or sets the original logical path.</summary>
    public string Path { get; set; }

    /// <summary>Gets or sets the latest event ordering key.</summary>
    public string SortKey { get; set; }

    /// <summary>Gets or sets whether the latest event indicates presence.</summary>
    public bool Present { get; set; }

    /// <inheritdoc />
    public void WriteIndexes(BsonDocument document)
    {
        document["LocationKey"] = HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(this.Location));
        document["Path"] = this.Path;
        document["Present"] = this.Present;
    }
}

/// <summary>Stores the monotonic event arrival position.</summary>
public sealed class LiteDbFileEventState
{
    /// <summary>Gets or sets the next committed arrival sequence.</summary>
    public long Sequence { get; set; }
}

// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents a file event entity persisted in the database via EF Core.
/// Maps to the "__Storage_FileEvents" table and is distinct from the domain FileEvent.
/// </summary>
[Table("__Storage_FileEvents")]
[Index(nameof(LocationName), Name = "IX_LocationName")]
[Index(nameof(FilePath), Name = "IX_FilePath")]
[Index(nameof(EventType), Name = "IX_EventType")]
[Index(nameof(Checksum), Name = "IX_Checksum")]
public class FileEventEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the file event entity.
    /// Serves as the primary key in the database.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Represents a unique identifier for a scan.
    /// </summary>
    public Guid? ScanId { get; set; }

    /// <summary>
    /// Gets or sets the name of the monitored location (e.g., "Docs").
    /// Indexed for efficient querying by location.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string LocationName { get; set; }

    /// <summary>
    /// Gets or sets the relative file path within the monitored location (e.g., "test/file.txt").
    /// Indexed for fast lookups by file path.
    /// </summary>
    [Required]
    [MaxLength(1024)]
    public string FilePath { get; set; }

    /// <summary>
    /// Gets or sets the type of event (e.g., Added, Changed, Deleted) as an integer.
    /// Stored as int to map to the FileEventType enum, with an index for performance.
    /// </summary>
    [Required]
    public int EventType { get; set; }

    /// <summary>
    /// Gets or sets the size of the file in bytes at the time of detection.
    /// Used for tracking file changes.
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// Gets or sets the SHA256 checksum of the file content at the time of detection.
    /// Indexed for efficient checksum-based queries.
    /// </summary>
    [MaxLength(64)] // SHA256 hash length
    public string Checksum { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the event was detected.
    /// Required for tracking event history.
    /// </summary>
    [Required]
    public DateTimeOffset DetectedDate { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp of the file, if available.
    /// Nullable to handle cases like deletions where metadata may not be present.
    /// </summary>
    public DateTimeOffset? LastModifiedDate { get; set; }
    //public DateTimeOffset? UpdatedDate { get; set; }

    [NotMapped]
    /// <summary>
    /// Gets or sets additional properties associated with the file event.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    [Column("Properties")]
    public string
        PropertiesJson // TODO: .NET8 use new ef core primitive collections here (store as json) https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew#primitive-collections
    {
        get =>
            this.Properties.IsNullOrEmpty()
                ? null
                : JsonSerializer.Serialize(this.Properties, DefaultJsonSerializerOptions.Create());
        set =>
            this.Properties = value.IsNullOrEmpty()
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, object>>(value,
                    DefaultJsonSerializerOptions.Create());
    }

    /// <summary>
    /// Gets or sets the row version for optimistic concurrency control.
    /// Automatically managed by EF Core via the Timestamp attribute.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; }
}
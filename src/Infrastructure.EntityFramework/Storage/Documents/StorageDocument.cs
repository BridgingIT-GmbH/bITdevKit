// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

/// <summary>
/// Represents a persisted document row for the Entity Framework document store provider.
/// </summary>
[Table("__Storage_Documents")]
[Index(nameof(TypeHash), nameof(PartitionKeyHash), nameof(RowKeyHash), IsUnique = true)]
[Index(nameof(TypeHash), nameof(PartitionKeyHash), nameof(RowKey))]
public class StorageDocument
{
    /// <summary>
    /// Defines the fixed maximum length for persisted raw document key values in the EF provider.
    /// </summary>
    public const int MaximumKeyLength = 256;

    /// <summary>
    /// Gets or sets the internal row identifier.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the normalized document type name.
    /// </summary>
    [Required]
    [MaxLength(MaximumKeyLength)]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 lookup hash for <see cref="Type" />.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string TypeHash { get; set; }

    /// <summary>
    /// Gets or sets the partition key.
    /// </summary>
    [Required]
    [MaxLength(MaximumKeyLength)]
    public string PartitionKey { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 lookup hash for <see cref="PartitionKey" />.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string PartitionKeyHash { get; set; }

    /// <summary>
    /// Gets or sets the row key.
    /// </summary>
    [Required]
    [MaxLength(MaximumKeyLength)]
    public string RowKey { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 lookup hash for <see cref="RowKey" />.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string RowKeyHash { get; set; }

    /// <summary>
    /// Gets or sets the serialized document payload.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the content checksum for the serialized payload.
    /// </summary>
    [MaxLength(64)]
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the worker identifier that currently owns the mutation lease.
    /// </summary>
    [MaxLength(256)]
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp until which the current mutation lease remains valid.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral optimistic concurrency token.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the UTC timestamp of the last content update.
    /// </summary>
    public DateTimeOffset? UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the document metadata dictionary.
    /// </summary>
    [NotMapped]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the serialized metadata payload.
    /// </summary>
    [Column("Properties")]
    public string PropertiesJson
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
}

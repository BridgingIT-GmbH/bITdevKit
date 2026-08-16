// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

/// <summary>
/// Represents outbox message.
/// </summary>
[Table("__Outbox_Messages")]
[Index(nameof(Type))]
[Index(nameof(MessageId))]
[Index(nameof(CreatedDate))]
[Index(nameof(ProcessedDate))]
public class OutboxMessage
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the message id.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the content hash.
    /// </summary>
    [MaxLength(64)] // MD5=32, SHA256=64
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the processed date.
    /// </summary>
    public DateTimeOffset? ProcessedDate { get; set; }

    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    [NotMapped]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Stores the properties json.
    /// </summary>
    [Column("Properties")]
    public string PropertiesJson // TODO: .NET8 use new ef core primitive collections here (store as json) https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew#primitive-collections
    {
        get =>
            this.Properties.IsNullOrEmpty()
                ? null
                : JsonSerializer.Serialize(this.Properties, DefaultJsonSerializerOptions.Create());
        set =>
            this.Properties = value.IsNullOrEmpty()
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, object>>(value, DefaultJsonSerializerOptions.Create());
    }

    // [Timestamp]
    // public byte[] RowVersion { get; set; }
}

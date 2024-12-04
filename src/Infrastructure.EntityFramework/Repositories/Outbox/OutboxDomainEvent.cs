﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

[Table("__Outbox_DomainEvents")]
[Index(nameof(Type))]
[Index(nameof(EventId))]
[Index(nameof(CreatedDate))]
[Index(nameof(ProcessedDate))]
public class OutboxDomainEvent
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string EventId { get; set; }

    [Required]
    [MaxLength(2048)]
    public string Type { get; set; }

    public string Content { get; set; }

    [MaxLength(64)] // MD5=32, SHA256=64
    public string ContentHash { get; set; }

    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTimeOffset? ProcessedDate { get; set; }

    [NotMapped]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    [Column("Properties")]
    public string PropertiesJson // TODO: .NET8 use new ef core primitive collections here (store as json) https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew#primitive-collections
    {
        get =>
            this.Properties.IsNullOrEmpty()
                ? null
                : JsonSerializer.Serialize(this.Properties, DefaultSystemTextJsonSerializerOptions.Create());
        set =>
            this.Properties = value.IsNullOrEmpty()
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, object>>(value,
                    DefaultSystemTextJsonSerializerOptions.Create());
    }

    // [Timestamp]
    // public byte[] RowVersion { get; set; }
}
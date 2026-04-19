// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

[Table("__Outbox_DomainEvents")]
[Index(nameof(IsArchived), nameof(ProcessedDate), nameof(LockedUntil), nameof(CreatedDate))]
[Index(nameof(IsArchived), nameof(Type), nameof(ProcessedDate), nameof(CreatedDate))]
[Index(nameof(EventId))]
[Index(nameof(IsArchived), nameof(ArchivedDate))]
/// <summary>
/// Represents a persisted domain event row in the Entity Framework outbox.
/// </summary>
/// <remarks>
/// The entity stores the serialized domain event payload together with worker lease metadata so multiple hosts can
/// safely compete for the same outbox rows without publishing a domain event more than once at a time.
/// </remarks>
public class OutboxDomainEvent
{
    /// <summary>
    /// Gets or sets the primary key for the persisted outbox row.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the logical domain event identifier copied from <see cref="Domain.IDomainEvent.EventId" />.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the persisted CLR type token used to restore the domain event payload.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the serialized domain event payload.
    /// </summary>
    [Required]
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the payload hash for diagnostics and integrity checks.
    /// </summary>
    [MaxLength(64)] // MD5=32, SHA256=64
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the domain event was stored in the outbox.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the provider-neutral optimistic concurrency version used to protect lease and processing updates.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets a value indicating whether the domain event has been archived.
    /// </summary>
    [Required]
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the domain event was archived.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }

    /// <summary>
    /// Gets or sets the worker instance identifier that currently owns the processing lease.
    /// </summary>
    [MaxLength(256)]
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the lease expiration timestamp for this outbox row.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when processing began under the current lease.
    /// </summary>
    public DateTimeOffset? ProcessingStartedDate { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the domain event reached a terminal state.
    /// </summary>
    public DateTimeOffset? ProcessedDate { get; set; }

    /// <summary>
    /// Gets or sets the latest failure summary for the outbox row.
    /// </summary>
    [MaxLength(4000)]
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the strongly typed metadata restored from <see cref="PropertiesJson" />.
    /// </summary>
    [NotMapped]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the JSON persistence column for <see cref="Properties" />.
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
                : JsonSerializer.Deserialize<Dictionary<string, object>>(value,
                    DefaultJsonSerializerOptions.Create());
    }

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion" /> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}

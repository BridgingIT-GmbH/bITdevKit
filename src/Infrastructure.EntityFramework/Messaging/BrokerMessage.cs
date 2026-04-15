// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using BridgingIT.DevKit.Application.Messaging;

[Table("__Messaging_BrokerMessages")]
[Index(nameof(MessageId), IsUnique = true)]
[Index(nameof(IsArchived), nameof(Status), nameof(LockedUntil), nameof(CreatedDate))]
[Index(nameof(IsArchived), nameof(Type), nameof(CreatedDate))]
[Index(nameof(IsArchived), nameof(ProcessedDate))]
[Index(nameof(IsArchived), nameof(ArchivedDate))]
/// <summary>
/// Represents a durable broker message row stored in Entity Framework persistence.
/// </summary>
/// <remarks>
/// The entity keeps the serialized payload and a JSON-backed collection of handler states so the broker
/// can coordinate multi-node leasing and handler-specific retry state without additional tables.
/// </remarks>
public class BrokerMessage
{
    [Key]
    /// <summary>
    /// Gets or sets the primary key for the persisted broker row.
    /// </summary>
    public Guid Id { get; set; }

    [Required]
    [MaxLength(256)]
    /// <summary>
    /// Gets or sets the logical message identifier copied from <see cref="BridgingIT.DevKit.Application.Messaging.IMessage.MessageId"/>.
    /// </summary>
    public string MessageId { get; set; }

    [Required]
    [MaxLength(2048)]
    /// <summary>
    /// Gets or sets the persisted CLR type token used to restore the message payload.
    /// </summary>
    public string Type { get; set; }

    [Required]
    /// <summary>
    /// Gets or sets the serialized message payload.
    /// </summary>
    public string Content { get; set; }

    [MaxLength(64)]
    /// <summary>
    /// Gets or sets the payload hash for diagnostics and integrity checks.
    /// </summary>
    public string ContentHash { get; set; }

    [Required]
    /// <summary>
    /// Gets or sets the timestamp when the message was accepted by the broker transport.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    [ConcurrencyCheck]
    /// <summary>
    /// Gets or sets the provider-neutral optimistic concurrency version used to protect message leasing and state updates.
    /// </summary>
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the optional timestamp after which the broker should stop trying to process the message.
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; set; }

    [Required]
    /// <summary>
    /// Gets or sets the aggregate processing status derived from the handler-state collection.
    /// </summary>
    public BrokerMessageStatus Status { get; set; } = BrokerMessageStatus.Pending;

    [Required]
    /// <summary>
    /// Gets or sets a value indicating whether the message has been moved out of the active working set.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was archived.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }

    [MaxLength(256)]
    /// <summary>
    /// Gets or sets the worker instance identifier that currently owns the lease.
    /// </summary>
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the lease expiration timestamp for this message.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when processing began under the current lease.
    /// </summary>
    public DateTimeOffset? ProcessingStartedDate { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the aggregate message state became terminal.
    /// </summary>
    public DateTimeOffset? ProcessedDate { get; set; }

    [MaxLength(4000)]
    /// <summary>
    /// Gets or sets the latest aggregate failure summary for the message.
    /// </summary>
    public string LastError { get; set; }

    [NotMapped]
    /// <summary>
    /// Gets or sets the strongly typed message properties restored from <see cref="PropertiesJson"/>.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    [Column("Properties")]
    /// <summary>
    /// Gets or sets the JSON persistence column for <see cref="Properties"/>.
    /// </summary>
    public string PropertiesJson
    {
        get => this.Properties.IsNullOrEmpty()
            ? null
            : JsonSerializer.Serialize(this.Properties, DefaultJsonSerializerOptions.Create());
        set => this.Properties = value.IsNullOrEmpty()
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, object>>(value, DefaultJsonSerializerOptions.Create());
    }

    [NotMapped]
    /// <summary>
    /// Gets or sets the strongly typed per-handler execution states restored from <see cref="HandlerStatesJson"/>.
    /// </summary>
    public IList<BrokerMessageHandlerState> HandlerStates { get; set; } = [];

    [Column("HandlerStates")]
    /// <summary>
    /// Gets or sets the JSON persistence column for <see cref="HandlerStates"/>.
    /// </summary>
    public string HandlerStatesJson
    {
        get => this.HandlerStates.IsNullOrEmpty()
            ? null
            : JsonSerializer.Serialize(this.HandlerStates, DefaultJsonSerializerOptions.Create());
        set => this.HandlerStates = value.IsNullOrEmpty()
            ? []
            : JsonSerializer.Deserialize<List<BrokerMessageHandlerState>>(value, DefaultJsonSerializerOptions.Create());
    }

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}
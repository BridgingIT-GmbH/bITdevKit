// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using BridgingIT.DevKit.Application.Queueing;

[Table("__Queueing_BrokerMessages")]
[Index(nameof(MessageId), IsUnique = true)]
[Index(nameof(Status), nameof(LockedUntil), nameof(CreatedDate))]
[Index(nameof(Type), nameof(Status), nameof(CreatedDate))]
[Index(nameof(QueueName), nameof(Status), nameof(CreatedDate))]
/// <summary>
/// Represents a durable queue row stored in Entity Framework persistence.
/// </summary>
public class QueueMessage
{
    /// <summary>
    /// Gets or sets the primary key for the persisted queue row.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the logical message identifier copied from <see cref="IQueueMessage.MessageId"/>.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the logical queue name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the persisted CLR type token used to restore the payload.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the serialized queue message payload.
    /// </summary>
    [Required]
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the payload hash for diagnostics and integrity checks.
    /// </summary>
    [MaxLength(64)]
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was accepted by the broker transport.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the provider-neutral optimistic concurrency version.
    /// </summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the optional timestamp after which the broker should stop trying to process the message.
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the message has been archived.
    /// </summary>
    [Required]
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was archived.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }

    /// <summary>
    /// Gets or sets the queue processing status.
    /// </summary>
    [Required]
    public QueueMessageStatus Status { get; set; } = QueueMessageStatus.Pending;

    /// <summary>
    /// Gets or sets the number of processing attempts performed for this message.
    /// </summary>
    [Required]
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the handler type that last processed or attempted to process the message.
    /// </summary>
    [MaxLength(2048)]
    public string RegisteredHandlerType { get; set; }

    /// <summary>
    /// Gets or sets the worker instance identifier that currently owns the lease.
    /// </summary>
    [MaxLength(256)]
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
    /// Gets or sets the timestamp when the message reached a terminal state.
    /// </summary>
    public DateTimeOffset? ProcessedDate { get; set; }

    /// <summary>
    /// Gets or sets the latest failure summary.
    /// </summary>
    [MaxLength(4000)]
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the strongly typed message properties restored from <see cref="PropertiesJson"/>.
    /// </summary>
    [NotMapped]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the JSON persistence column for <see cref="Properties"/>.
    /// </summary>
    [Column("Properties")]
    public string PropertiesJson
    {
        get => this.Properties.IsNullOrEmpty()
            ? null
            : JsonSerializer.Serialize(this.Properties, DefaultJsonSerializerOptions.Create());
        set => this.Properties = value.IsNullOrEmpty()
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, object>>(value, DefaultJsonSerializerOptions.Create());
    }

    /// <summary>
    /// Regenerates <see cref="ConcurrencyVersion"/> so Entity Framework can detect concurrent updates.
    /// </summary>
    public void AdvanceConcurrencyVersion()
    {
        this.ConcurrencyVersion = Guid.NewGuid();
    }
}
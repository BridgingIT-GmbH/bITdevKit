// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents a durable broker message row stored in native LiteDB persistence.
/// </summary>
/// <remarks>
/// The entity keeps the serialized payload and a JSON-backed collection of handler states so the broker
/// can coordinate multi-node leasing and handler-specific retry state without additional tables.
/// </remarks>
public class LiteDbBrokerMessage : ILiteDbIndexedRecord
{
    /// <summary>
    /// Gets or sets the primary key for the persisted broker row.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the logical message identifier copied from <see cref="IMessage.MessageId"/>.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the persisted CLR type token used to restore the message payload.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the serialized message payload.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the payload hash for diagnostics and integrity checks.
    /// </summary>
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was accepted by the broker transport.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the optional timestamp after which the broker should stop trying to process the message.
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the aggregate processing status derived from the handler-state collection.
    /// </summary>
    public BrokerMessageStatus Status { get; set; } = BrokerMessageStatus.Pending;

    /// <summary>
    /// Gets or sets a value indicating whether the message has been moved out of the active working set.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was archived.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }

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

    /// <summary>
    /// Gets or sets the latest aggregate failure summary for the message.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the strongly typed message properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the strongly typed per-handler execution states.
    /// </summary>
    public IList<LiteDbBrokerMessageHandlerState> HandlerStates { get; set; } = [];

    /// <summary>Gets or sets persisted processing failures outside the normal handler outcome pipeline.</summary>
    public int ProcessingFailureCount { get; set; }

    /// <summary>Gets or sets the unique token fencing this processing claim.</summary>
    public Guid? LeaseToken { get; set; }

    /// <summary>Gets or sets the next retry eligibility time.</summary>
    public DateTimeOffset? NextAttemptUtc { get; set; }

    /// <summary>Gets or sets whether publication is awaiting its first handler registration.</summary>
    public bool WaitingForHandler { get; set; }

    /// <inheritdoc />
    public void WriteIndexes(BsonDocument document)
    {
        document["Status"] = (int)this.Status;
        document["Archived"] = this.IsArchived;
        document["ExpiresTicks"] = this.ExpiresOn?.UtcTicks ?? 0;
        document["Ready"] = !this.IsArchived && !this.WaitingForHandler && this.Status is BrokerMessageStatus.Pending or BrokerMessageStatus.Failed or BrokerMessageStatus.Processing;
        document["Waiting"] = this.WaitingForHandler;
        document["Type"] = this.Type;
        document["MessageId"] = this.MessageId;
        document["CreatedTicks"] = this.CreatedDate.UtcTicks;
        document["LeaseUntilTicks"] = this.LockedUntil?.UtcTicks ?? 0;
        document["NextAttemptTicks"] = this.NextAttemptUtc?.UtcTicks ?? 0;
    }
}

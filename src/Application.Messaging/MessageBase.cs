// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using System.Diagnostics;
using Common;
using FluentValidation.Results;

/// <summary>
///     Initializes a new instance of the <see cref="MessageBase" /> class.
/// </summary>
[DebuggerDisplay("Id={MessageId}")]
public abstract class MessageBase : IMessage, IEquatable<MessageBase>
{
    private int? hashCode;

    [Obsolete("Use the new MessageId from now on")]
    public virtual string Id
    {
        get => this.MessageId;
        set => this.MessageId = value;
    }

    /// <summary>
    ///     Gets the id of this message.
    /// </summary>
    /// <value>
    ///     The message identifier.
    /// </value>
    public virtual string MessageId { get; protected set; } =
        GuidGenerator.CreateSequential().ToString("N"); // TODO: change to GUID like DomainEvent

    /// <summary>
    ///     Gets the timestamp when this message was created.
    /// </summary>
    /// <value>
    ///     The message identifier.
    /// </value>
    public virtual DateTimeOffset Timestamp { get; protected set; } = DateTime.UtcNow;

    /// <summary>
    ///     Hold extra properties for this message.
    /// </summary>
    public virtual IDictionary<string, object> Properties { get; protected set; } = new Dictionary<string, object>();

    /// <summary>
    ///     Validates this message.
    /// </summary>
    public virtual ValidationResult Validate()
    {
        return new ValidationResult();
    }

    public bool Equals(MessageBase other)
    {
        return other is not null && this.MessageId.Equals(other.MessageId);
    }

    public override int GetHashCode()
    {
        return this.hashCode ?? (this.hashCode = this.MessageId.GetHashCode() ^ 31).Value;
    }
}
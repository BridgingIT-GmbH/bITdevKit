// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Diagnostics;
using Common;

/// <summary>
///     Base class for domain events, encapsulating common properties and behavior for events within the domain.
/// </summary>
[DebuggerDisplay("EventId={EventId}")]
public abstract class DomainEventBase : IDomainEvent, IEquatable<DomainEventBase>
{
    private int? hashCode;

    /// <summary>
    ///     Gets the unique identifier for this event. Typically generated
    ///     sequentially to maintain order and uniqueness across events.
    /// </summary>
    /// <remarks>
    ///     This property is usually set when the event is created and is
    ///     intended to uniquely identify each instance of a domain event.
    /// </remarks>
    public virtual Guid EventId { get; protected set; } = GuidGenerator.CreateSequential();

    /// <summary>
    ///     Gets the timestamp indicating when the domain event was created.
    /// </summary>
    /// <remarks>
    ///     This property is set to the current UTC date and time when the event is instantiated.
    /// </remarks>
    public virtual DateTimeOffset Timestamp { get; protected set; } = DateTime.UtcNow;

    /// <summary>
    ///     Compares this instance with another DomainEventBase instance
    ///     to determine if they are equal based on their EventId.
    /// </summary>
    /// <param name="other">The other DomainEventBase instance to compare with.</param>
    /// <returns>True if the EventId properties of both instances are equal, otherwise false.</returns>
    public bool Equals(DomainEventBase other)
    {
        return other is not null && this.EventId.Equals(other.EventId);
    }

    /// <summary>
    ///     Computes the hash code for the current domain event object.
    /// </summary>
    /// <returns>
    ///     An integer representing the hash code of the current domain event object.
    /// </returns>
    public override int GetHashCode()
    {
        // TODO: look at the hascode that should be readonly
        return this.hashCode ?? (this.hashCode = this.EventId.GetHashCode() ^ 31).Value;
    }
}
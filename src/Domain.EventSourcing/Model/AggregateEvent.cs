// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Model;

using MediatR;

/// <summary>
/// Represents the aggregate event domain event.
/// </summary>
public class AggregateEvent : DomainEventWithGuid, IAggregateEvent, INotification
{
    /// <summary>
    /// Initializes a new instance of the <c>AggregateEvent</c> class.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="version">The version used by the operation.</param>
    protected AggregateEvent(Guid id, int version)
        : base(id)
    {
        this.AggregateVersion = version;
    }

    private AggregateEvent() { }

    /// <summary>
    /// Gets or sets the aggregate version.
    /// </summary>
    public int AggregateVersion { get; set; }
}

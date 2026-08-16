// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

/// <summary>
/// Represents domain event with guid.
/// </summary>
public class DomainEventWithGuid : DomainEvent<Guid>, IDomainEventWithGuid
{
    /// <summary>
    /// Initializes a new instance of the <c>DomainEventWithGuid</c> class.
    /// </summary>
    public DomainEventWithGuid()
        : base(Guid.NewGuid()) // TODO: use GuidGenerator.CreateSequential() here
    { }

    /// <summary>
    /// Initializes a new instance of the <c>DomainEventWithGuid</c> class.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    public DomainEventWithGuid(Guid aggregateId)
        : base(aggregateId) { }
}

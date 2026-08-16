// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Model;

/// <summary>
/// Defines operations for i aggregate event.
/// </summary>
public interface IAggregateEvent : IDomainEventWithGuid
{
    /// <summary>
    /// Gets or sets the aggregate version.
    /// </summary>
    int AggregateVersion { get; set; }
}

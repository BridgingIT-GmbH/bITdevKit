// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.Models;

/// <summary>
/// Represents event store configuration.
/// </summary>
public class EventStoreConfiguration : IEventStoreConfiguration
{
    /// <summary>
    /// Gets or sets the default schema.
    /// </summary>
    public string DefaultSchema { get; set; } = "EventStore";
}

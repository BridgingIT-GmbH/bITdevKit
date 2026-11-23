// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Domain.Outbox;

/// <summary>Configures the existing domain-event outbox contract for an owned LiteDB database.</summary>
public sealed class LiteDbDomainEventOutboxOptions : OutboxDomainEventOptions
{
    /// <summary>Gets or sets the isolated outbox collection name.</summary>
    public string CollectionName { get; set; } = "domain_event_outbox";

    /// <summary>Gets or sets the upper bound on one worker invocation, independent of backlog size.</summary>
    public int BatchSize { get; set; } = 128;
}

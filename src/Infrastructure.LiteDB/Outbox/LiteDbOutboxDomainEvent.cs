// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

/// <summary>Contains durable domain-event content and fenced processing state.</summary>
public sealed class LiteDbOutboxDomainEvent
{
    /// <summary>Gets or sets the stable event identity used for deduplication.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the serialized domain-event CLR type.</summary>
    public string Type { get; set; }

    /// <summary>Gets or sets the serialized event payload.</summary>
    public string Content { get; set; }

    /// <summary>Gets or sets the payload integrity hash.</summary>
    public string ContentHash { get; set; }

    /// <summary>Gets or sets the original event creation time in UTC ticks.</summary>
    public long CreatedTicks { get; set; }

    /// <summary>Gets or sets the serialized event properties and tracing context.</summary>
    public string Properties { get; set; }

    /// <summary>Gets or sets the successful completion time in UTC ticks.</summary>
    public long? ProcessedTicks { get; set; }

    /// <summary>Gets or sets the total claimed delivery attempts.</summary>
    public int Attempts { get; set; }

    /// <summary>Gets or sets the last bounded delivery error.</summary>
    public string LastError { get; set; }

    /// <summary>Gets or sets the unique token for the current claim.</summary>
    public Guid? LeaseToken { get; set; }

    /// <summary>Gets or sets the current lease expiration in UTC ticks.</summary>
    public long LeaseUntilTicks { get; set; }

    /// <summary>Gets or sets whether retention has archived this event.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the archive time in UTC ticks.</summary>
    public long? ArchivedTicks { get; set; }
}

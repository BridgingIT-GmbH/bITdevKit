// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents the stored serialized content of a broker message.
/// </summary>
public class BrokerMessageContentInfo
{
    /// <summary>
    /// Gets or sets the broker message primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the logical message identifier.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the persisted message type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the serialized broker payload.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the payload hash.
    /// </summary>
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the message is archived.
    /// </summary>
    public bool IsArchived { get; set; }
}
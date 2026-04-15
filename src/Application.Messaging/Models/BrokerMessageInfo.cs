// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents the operational view of a persisted broker message.
/// </summary>
public class BrokerMessageInfo
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
    /// Gets or sets the aggregate broker status.
    /// </summary>
    public BrokerMessageStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the message is archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the archive timestamp.
    /// </summary>
    public DateTimeOffset? ArchivedDate { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the expiration timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the current lease owner.
    /// </summary>
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the lease expiration timestamp.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the terminal processing timestamp.
    /// </summary>
    public DateTimeOffset? ProcessedDate { get; set; }

    /// <summary>
    /// Gets or sets the sum of all handler attempts.
    /// </summary>
    public int AttemptCountSummary { get; set; }

    /// <summary>
    /// Gets or sets the latest aggregate failure summary.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the broker message properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the handler details for the message.
    /// </summary>
    public IEnumerable<BrokerMessageHandlerInfo> Handlers { get; set; } = [];
}
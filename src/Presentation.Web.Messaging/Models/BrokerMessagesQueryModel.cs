// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Messaging.Models;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents the query parameters used to list persisted broker messages.
/// </summary>
/// <example>
/// <code>
/// GET /api/_system/messaging/messages?status=Pending&amp;includeHandlers=true&amp;take=100
/// </code>
/// </example>
public class BrokerMessagesQueryModel
{
    /// <summary>
    /// Gets or sets the optional aggregate message status filter.
    /// </summary>
    public BrokerMessageStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the optional persisted CLR message type filter.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the optional logical message identifier filter.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the optional lease-owner filter.
    /// </summary>
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the optional archive-state filter. When <c>null</c>, both active and archived messages are included.
    /// </summary>
    public bool? IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the optional lower creation-date filter.
    /// </summary>
    public DateTimeOffset? CreatedAfter { get; set; }

    /// <summary>
    /// Gets or sets the optional upper creation-date filter.
    /// </summary>
    public DateTimeOffset? CreatedBefore { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether handler details should be included.
    /// </summary>
    public bool IncludeHandlers { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum number of results to return.
    /// </summary>
    public int? Take { get; set; }
}

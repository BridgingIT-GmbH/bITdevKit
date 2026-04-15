// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Messaging.Models;

/// <summary>
/// Represents the query parameters used to retrieve aggregate broker message statistics.
/// </summary>
/// <example>
/// <code>
/// GET /api/_system/messaging/messages/stats?isArchived=false
/// </code>
/// </example>
public class BrokerMessageStatsQueryModel
{
    /// <summary>
    /// Gets or sets the optional lower date filter.
    /// </summary>
    public DateTimeOffset? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the optional upper date filter.
    /// </summary>
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the optional archive-state filter. When <c>null</c>, both active and archived messages are included.
    /// </summary>
    public bool? IsArchived { get; set; }
}

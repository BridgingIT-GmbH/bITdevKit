// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Messaging.Models;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents the query parameters used to purge persisted broker messages.
/// </summary>
/// <example>
/// <code>
/// DELETE /api/_system/messaging/messages?olderThan=2026-01-01T00:00:00Z&amp;statuses=Succeeded&amp;statuses=Expired
/// </code>
/// </example>
public class BrokerMessagesPurgeModel
{
    /// <summary>
    /// Gets or sets the optional upper age filter.
    /// </summary>
    public DateTimeOffset? OlderThan { get; set; }

    /// <summary>
    /// Gets or sets the optional statuses to purge.
    /// </summary>
    public BrokerMessageStatus[] Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional archive-state filter.
    /// </summary>
    public bool? IsArchived { get; set; }
}
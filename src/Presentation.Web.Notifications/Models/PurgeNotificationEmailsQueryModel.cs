// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Notifications.Models;

using BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Represents the supported query string filters for purging notification emails.
/// </summary>
public class PurgeNotificationEmailsQueryModel
{
    /// <summary>
    /// Gets or sets the optional age cutoff.
    /// </summary>
    public DateTimeOffset? OlderThan { get; set; }

    /// <summary>
    /// Gets or sets the optional status filters.
    /// </summary>
    public EmailMessageStatus[] Statuses { get; set; }

    /// <summary>
    /// Gets or sets the optional archive-state filter. When <c>null</c>, both active and archived emails are eligible.
    /// </summary>
    public bool? IsArchived { get; set; }
}

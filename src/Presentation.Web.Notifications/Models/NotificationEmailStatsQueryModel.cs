// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Notifications.Models;

/// <summary>
/// Represents the supported query string filters for notification email statistics.
/// </summary>
public class NotificationEmailStatsQueryModel
{
    /// <summary>
    /// Gets or sets the optional lower created/sent timestamp filter.
    /// </summary>
    public DateTimeOffset? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the optional upper created/sent timestamp filter.
    /// </summary>
    public DateTimeOffset? EndDate { get; set; }
}

// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Notifications.Models;

using BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Represents the supported query string filters for listing notification emails.
/// </summary>
public class NotificationEmailsQueryModel
{
    /// <summary>
    /// Gets or sets the optional status filter.
    /// </summary>
    public EmailMessageStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the optional subject substring filter.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the optional lease owner filter.
    /// </summary>
    public string LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the optional archive-state filter. When <c>null</c>, both active and archived emails are included.
    /// </summary>
    public bool? IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the optional lower creation timestamp filter.
    /// </summary>
    public DateTimeOffset? CreatedAfter { get; set; }

    /// <summary>
    /// Gets or sets the optional upper creation timestamp filter.
    /// </summary>
    public DateTimeOffset? CreatedBefore { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum number of rows to return.
    /// </summary>
    public int? Take { get; set; } = 100;
}

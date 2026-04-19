// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Represents aggregate statistics for persisted notification emails.
/// </summary>
public class NotificationEmailStats
{
    /// <summary>
    /// Gets or sets the total number of matching notification emails.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the number of pending notification emails.
    /// </summary>
    public int Pending { get; set; }

    /// <summary>
    /// Gets or sets the number of currently leased notification emails.
    /// </summary>
    public int Locked { get; set; }

    /// <summary>
    /// Gets or sets the number of successfully sent notification emails.
    /// </summary>
    public int Sent { get; set; }

    /// <summary>
    /// Gets or sets the number of failed notification emails.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Gets or sets the number of notification emails with an active lease.
    /// </summary>
    public int Leased { get; set; }
}

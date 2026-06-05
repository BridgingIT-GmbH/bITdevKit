// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Notifications;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the operational notification email endpoint group.
/// </summary>
/// <example>
/// <code>
/// services.AddNotificationEndpoints(options => options
///     .GroupPath("/_bdk/api/notifications/emails")
///     .GroupTag("_bdk.Notifications"));
/// </code>
/// </example>
public class NotificationEmailEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationEmailEndpointsOptions"/> class with notifications-specific defaults.
    /// </summary>
    public NotificationEmailEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/notifications/emails";
        this.GroupTag = "_bdk.Notifications";
    }
}

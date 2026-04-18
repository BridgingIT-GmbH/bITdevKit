// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Messaging;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the operational messaging endpoint group.
/// </summary>
/// <example>
/// <code>
/// services.AddMessagingEndpoints(options => options
///     .GroupPath("/api/_system/messaging/messages")
///     .GroupTag("_System.Messaging"));
/// </code>
/// </example>
public class MessagingEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingEndpointsOptions"/> class with messaging-specific defaults.
    /// </summary>
    public MessagingEndpointsOptions()
    {
        this.GroupPath = "/api/_system/messaging/messages";
        this.GroupTag = "_System.Messaging";
    }
}

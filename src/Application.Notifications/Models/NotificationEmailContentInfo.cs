// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Represents the persisted body content for a notification email.
/// </summary>
public class NotificationEmailContentInfo
{
    /// <summary>
    /// Gets or sets the notification email primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the message subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the persisted message body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the body contains HTML markup.
    /// </summary>
    public bool IsHtml { get; set; }
}

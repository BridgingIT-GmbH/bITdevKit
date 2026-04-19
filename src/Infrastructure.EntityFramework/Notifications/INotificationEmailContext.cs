// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Notifications;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Defines the Entity Framework sets required for notification email persistence.
/// </summary>
public interface INotificationEmailContext
{
    /// <summary>
    /// Gets or sets the persisted notification email rows.
    /// </summary>
    DbSet<EmailMessageEntity> NotificationsEmails { get; set; }

    /// <summary>
    /// Gets or sets the persisted notification email attachment rows.
    /// </summary>
    DbSet<EmailMessageAttachmentEntity> NotificationsEmailAttachments { get; set; }
}

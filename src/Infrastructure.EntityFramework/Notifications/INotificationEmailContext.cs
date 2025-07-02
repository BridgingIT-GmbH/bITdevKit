// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Notifications;

using Microsoft.EntityFrameworkCore;

public interface INotificationEmailContext
{
    DbSet<EmailMessageEntity> NotificationsEmails { get; set; }

    DbSet<EmailMessageAttachmentEntity> NotificationsEmailAttachments { get; set; }
}
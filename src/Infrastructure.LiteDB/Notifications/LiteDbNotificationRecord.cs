// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Notifications;

/// <summary>Stores a notification email, attachments, and its fenced delivery claim together.</summary>
public sealed class LiteDbNotificationRecord : ILiteDbIndexedRecord
{
    /// <summary>Gets or sets the notification and byte-exact attachments.</summary>
    public EmailMessage Message { get; set; }

    /// <summary>Gets or sets the unique current delivery claim.</summary>
    public string LeaseOwner { get; set; }

    /// <summary>Gets or sets its expiration.</summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>Gets or sets whether the notification is archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets its archive date.</summary>
    public DateTimeOffset? ArchivedDate { get; set; }

    /// <summary>Gets or sets the configured retry limit used in native selection.</summary>
    public int RetryLimit { get; set; }

    /// <inheritdoc />
    public void WriteIndexes(BsonDocument document)
    {
        document["MessageId"] = this.Message.Id.ToString("D");
        document["Archived"] = this.IsArchived;
        document["Status"] = (int)this.Message.Status;
        document["Ready"] = !this.IsArchived && (this.Message.Status is EmailMessageStatus.Pending or EmailMessageStatus.Locked || this.Message.Status == EmailMessageStatus.Failed && this.Message.RetryCount < this.RetryLimit);
        document["CreatedTicks"] = this.Message.CreatedAt.UtcTicks;
        document["LeaseUntilTicks"] = this.LockedUntil?.UtcTicks ?? 0;
    }
}
